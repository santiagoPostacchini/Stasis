using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Animations.Rigging;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace IKSuite
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class IKObjectController : MonoBehaviour
    {
        [Header("<color=green>Scriptable Object</color>")]
        public IKObjectPreset preset;

        [Header("<color=red>Se mira y no se toca</color>")]
        // Scene refs (asignar en Inspector)
        public Transform arm;        // Debe tener Animator + RigBuilder (INSTANCIA EN ESCENA)
        public Transform root;       // Padre de la cadena (se instancian bones aquí, bajo arm)
        public Transform rigObject;  // Debe tener Rig + ChainIKConstraint (BAJO arm)
        public Transform sistemas;   // opcional

        // Contenedores de visibilidad por sistema
        public Transform sys_IK_Distance;
        public Transform sys_IK_DistanceInverse;
        public Transform sys_IK_PlatformMovement;
        public Transform sys_IK_PlatformRotation;
        public Transform sys_IK_PlatformRotation_Stairs;

        // Targets por sistema (arrastrar referencias, sin .Find)
        public Transform target_IK_Distance;                 // TipController
        public Transform target_IK_DistanceInverse;          // TipController (inverse)
        public Transform target_IK_PlatformMovement;         // PlatformM
        public Transform target_IK_PlatformRotation;         // Platform
        public Transform target_IK_PlatformRotation_Stairs;  // Platform (stairs)

        // Stasis tip controllers (tus componentes)
        public Component stasisTipCtrl_Distance;
        public Component stasisTipCtrl_Inverse;
        public Component stasisTipCtrl_Movement;
        public Component stasisTipCtrl_Rotation;
        public Component stasisTipCtrl_Stairs;

        // Prefabs (asignar acá)
        public GameObject bonePrefab_Distance;
        public GameObject bonePrefab_PlatformMovement;
        public GameObject bonePrefab_Rotation;
        public GameObject tipPrefab_Distance_MeshOn;
        public GameObject tipPrefab_MeshOff;

        // Opciones de editor
        public bool liveRebuildInEditor = true;

        // Offset de orientación para Distance/Inverse (evita atravesar la plataforma)
        public Vector3 tipEulerOffset_Distance = new Vector3(0f, 180f, 0f);

        // caches
        ChainIKConstraint _chain;
        RigBuilder _rigBuilder;
        Rig _rig;

        readonly List<Transform> _generatedBones = new List<Transform>();
        Transform _generatedTip;

#if UNITY_EDITOR
        [SerializeField, HideInInspector] int _presetHash;
        bool _rebuildScheduled;
#endif

        // =====================================================================
        // Lifecycle
        // =====================================================================
        void OnEnable()
        {
            ApplySystemVisibilityInEditor();
            ApplyTunablesInEditor();
            EnsureRigComponents();

#if UNITY_EDITOR
            if (liveRebuildInEditor) ScheduleRebuild();
#else
            RebuildNow();
#endif
        }

        void OnDisable()
        {
#if UNITY_EDITOR
            // nada pesado
#else
            PurgeGeneratedRuntime();
#endif
        }

        void Update()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && preset && liveRebuildInEditor)
            {
                int h = ComputePresetHash();
                if (h != _presetHash)
                {
                    _presetHash = h;
                    ApplySystemVisibilityInEditor();
                    ApplyTunablesInEditor();
                    ScheduleRebuild();
                }
            }
#endif
        }

        void OnValidate()
        {
            ApplySystemVisibilityInEditor();
            ApplyTunablesInEditor();
#if UNITY_EDITOR
            if (liveRebuildInEditor) ScheduleRebuild();
#endif
        }

        // =====================================================================
        // Rebuild
        // =====================================================================
#if UNITY_EDITOR
        [ContextMenu("Rebuild In Editor Now")]
        void RebuildInEditorNowMenu() { RebuildNow(); }

        void ScheduleRebuild()
        {
            if (_rebuildScheduled) return;
            _rebuildScheduled = true;
            EditorApplication.delayCall += DoRebuildDelayed;
        }

        void DoRebuildDelayed()
        {
            _rebuildScheduled = false;
            if (this == null) return;
            RebuildNow();
        }
#endif

        void RebuildNow()
        {
            if (!ValidateSetup()) return;

#if UNITY_EDITOR
            if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(gameObject.scene);
            PurgeGeneratedEditor();
#else
            PurgeGeneratedRuntime();
#endif
            BuildArm(!Application.isPlaying);

            // Hook para AddElementsToRenderer:
            // En Editor NO reparentamos (evita errores de prefab instance), en Play sí.
            var stasisTC = GetStasisTipControllerForMode(preset.systemType);
            if (Application.isPlaying) CallAddElementsWithTemporaryParent(stasisTC);
            else SafeCallAddElements(stasisTC);

            // En Play, para Distance/Inverse: target en A y solver activo,
            // PERO rig weight lo controla FollowTargetController => rig queda en 0
            if (Application.isPlaying &&
                (preset.systemType == IKSystemType.IK_Distance || preset.systemType == IKSystemType.IK_DistanceInverse))
            {
                ApplyDistanceRuntimeInit();
            }

            // Rebind opcional
            var anim = arm ? arm.GetComponent<Animator>() : null;
            if (anim && Application.isPlaying) anim.Rebind();
        }

        // =====================================================================
        // Build chain
        // =====================================================================
        void BuildArm(bool immediate)
        {
            EnsureRigComponents();

            var bonePrefab = GetBonePrefabForMode(preset.systemType);
            var tipPrefab = GetTipPrefabForMode(preset.systemType);
            var target = GetTargetForMode(preset.systemType);
            var stasisTC = GetStasisTipControllerForMode(preset.systemType);

            Transform firstBone = null;
            Transform prevBone = null;
            Transform prevEnd = null;
            Vector3 firstBoneWorldScale = Vector3.one;

            int count = Mathf.Max(1, preset.boneCount);

            // Bones
            for (int i = 0; i < count; i++)
            {
                var boneT = InstantiateBone(bonePrefab, i == 0 ? root : prevBone, immediate);
                boneT.name = "Bone_" + (i + 1);
                _generatedBones.Add(boneT);

                if (i == 0)
                {
                    boneT.localPosition = Vector3.zero;
                    boneT.localRotation = Quaternion.identity;
                    firstBoneWorldScale = boneT.lossyScale;
                    SetWorldScale(boneT, firstBoneWorldScale);
                    firstBone = boneT;
                }
                else
                {
                    if (!prevEnd) return;
                    boneT.localPosition = prevEnd.localPosition;
                    boneT.localRotation = prevEnd.localRotation;
                    SetWorldScale(boneT, firstBoneWorldScale);
                }

                TryAssignTipControllerToBone(boneT.gameObject, stasisTC);

                var be = boneT.GetComponentInChildren<BoneEnd>(true);
                if (!be || !be.GetEnd()) return;
                prevEnd = be.GetEnd();
                prevBone = boneT;
            }

            // TIP: hijo del PADRE de END, en la pose mundial de END (+ offset para Distance/Inverse)
            Transform tipParent = prevEnd ? prevEnd.parent : prevBone;
            var tipT = InstantiateTip(tipPrefab, tipParent, immediate);
            tipT.name = "TIP";
            if (prevEnd != null)
            {
                tipT.position = prevEnd.position;
                tipT.rotation = (preset.systemType == IKSystemType.IK_Distance || preset.systemType == IKSystemType.IK_DistanceInverse)
                                ? prevEnd.rotation * Quaternion.Euler(tipEulerOffset_Distance)
                                : prevEnd.rotation;
            }
            else
            {
                tipT.localPosition = Vector3.zero;
                tipT.localRotation = Quaternion.identity;
            }
            _generatedTip = tipT;

            // Rig / Constraint: asignar data ANTES de build
            _chain = rigObject.GetComponent<ChainIKConstraint>();
            if (_chain == null) _chain = rigObject.gameObject.AddComponent<ChainIKConstraint>();
            _rig = rigObject.GetComponent<Rig>();
            if (_rig == null) _rig = rigObject.gameObject.AddComponent<Rig>();

            var data = _chain.data;
            data.root = firstBone;
            data.tip = tipT;
            data.target = target;
            _chain.data = data;

            // Pesos:
            // Distance/Inverse: rig siempre 0 (editor y play). ChainIK activo (1).
            // Movement/Rotation: rig 1 como venías usando.
            if (preset.systemType == IKSystemType.IK_Distance || preset.systemType == IKSystemType.IK_DistanceInverse)
            {
                _chain.weight = 1f;
                _rig.weight = 0f;
            }
            else
            {
                _chain.weight = 1f;
                _rig.weight = 1f;
            }

            // Sanear RigBuilder y recién ahí Build
            EnsureRigComponents();
            _rigBuilder.Build();

            // Para Distance modes, asegurar que el FollowTargetController del target apunte a ESTE rig
            if (preset.systemType == IKSystemType.IK_Distance || preset.systemType == IKSystemType.IK_DistanceInverse)
            {
                AssignRigToFollowTargetController(target, _rig);
            }
        }

        // En Play, iniciar Distance/Inverse:
        // - target = pose de TIP (A)
        // - ChainIK = 1
        // - Rig = 0 (peso controlado por FollowTargetController)
        void ApplyDistanceRuntimeInit()
        {
            var target = GetTargetForMode(preset.systemType);
            if (target == null || _generatedTip == null) return;

            target.position = _generatedTip.position;
            target.rotation = _generatedTip.rotation;

            if (_chain != null) _chain.weight = 1f;
            if (_rig != null) _rig.weight = 0f; // *** clave: el weight lo maneja FollowTargetController
        }

        // Vincula el rig al FollowTargetController en el target (si existe)
        static void AssignRigToFollowTargetController(Transform target, Rig rig)
        {
            if (!target || !rig) return;
            var ft = target.GetComponent<FollowTargetController>();
            if (!ft) return;
            ft.rig = rig;
        }

        // =====================================================================
        // Purge
        // =====================================================================
#if UNITY_EDITOR
        void PurgeGeneratedEditor()
        {
            if (!root) return;
            var tags = root.GetComponentsInChildren<IKGeneratedTag>(true);
            foreach (var t in tags) if (t) Undo.DestroyObjectImmediate(t.gameObject);
            _generatedBones.Clear();
            _generatedTip = null;
        }
#endif

        void PurgeGeneratedRuntime()
        {
            if (!root) return;
            var tags = root.GetComponentsInChildren<IKGeneratedTag>(true);
            foreach (var t in tags) if (t) Destroy(t.gameObject);
            _generatedBones.Clear();
            _generatedTip = null;
        }

        // =====================================================================
        // Instantiation helpers (con fix para Prefab Asset parenting)
        // =====================================================================
#if UNITY_EDITOR
        Transform GetSafeParent(Transform desiredParent)
        {
            // Si no hay root o no es de escena, mejor no avanzar
            if (!root || !root.gameObject.scene.IsValid())
                return desiredParent ? desiredParent : null;

            // Tomar el deseado si existe y pertenece a la MISMA escena que root
            if (desiredParent && desiredParent.gameObject.scene == root.gameObject.scene)
                return desiredParent;

            // Caso contrario, usar root
            return root;
        }
#endif

        Transform InstantiateBone(GameObject prefab, Transform parent, bool immediate)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && prefab && PrefabUtility.IsPartOfPrefabAsset(prefab))
            {
                var parentT = GetSafeParent(parent != null ? parent : root);
                // Instanciar con parent DIRECTO para no hacer SetParent luego (evita el error)
                var obj = PrefabUtility.InstantiatePrefab(prefab, parentT) as GameObject;
                if (obj == null) return null;
                obj.AddComponent<IKGeneratedTag>();
                Undo.RegisterCreatedObjectUndo(obj, "Create Bone");
                return obj.transform;
            }
#endif
            var go = Instantiate(prefab, parent ? parent : root);
            go.AddComponent<IKGeneratedTag>();
            return go.transform;
        }

        Transform InstantiateTip(GameObject prefab, Transform parent, bool immediate)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && prefab && PrefabUtility.IsPartOfPrefabAsset(prefab))
            {
                var parentT = GetSafeParent(parent != null ? parent : root);
                var obj = PrefabUtility.InstantiatePrefab(prefab, parentT) as GameObject;
                if (obj == null) return null;
                obj.AddComponent<IKGeneratedTag>();
                Undo.RegisterCreatedObjectUndo(obj, "Create TIP");
                return obj.transform;
            }
#endif
            var go = Instantiate(prefab, parent ? parent : root);
            go.AddComponent<IKGeneratedTag>();
            return go.transform;
        }

        // =====================================================================
        // Hook AddElementsToRenderer
        // =====================================================================
        void CallAddElementsWithTemporaryParent(Component controller)
        {
            if (controller == null || root == null) return;

            // En Play sí podemos reparentar temporalmente
            var ctrlT = controller.transform;
            var origParent = root.parent;
            var wp = root.position; var wr = root.rotation; var ws = root.lossyScale;

            root.SetParent(ctrlT, true);
            SafeCallAddElements(controller);
            root.SetParent(origParent, true);
            root.position = wp; root.rotation = wr; SetWorldScale(root, ws);
        }

        static void SafeCallAddElements(Component controller)
        {
            if (controller == null) return;
            var m = controller.GetType().GetMethod("AddElementsToRenderer",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (m != null && m.GetParameters().Length == 0)
            {
                try { m.Invoke(controller, null); }
                catch (System.SystemException ex) { Debug.LogError("[IKObjectController] AddElementsToRenderer error: " + ex.Message, controller); }
            }
        }

        // =====================================================================
        // Editor helpers (visibilidad y tunables)
        // =====================================================================
        void ApplySystemVisibilityInEditor()
        {
            if (!preset) return;
            Toggle(sys_IK_Distance, preset.systemType == IKSystemType.IK_Distance);
            Toggle(sys_IK_DistanceInverse, preset.systemType == IKSystemType.IK_DistanceInverse);
            Toggle(sys_IK_PlatformMovement, preset.systemType == IKSystemType.IK_PlatformMovement);
            Toggle(sys_IK_PlatformRotation, preset.systemType == IKSystemType.IK_PlatformRotation);
            Toggle(sys_IK_PlatformRotation_Stairs, preset.systemType == IKSystemType.IK_PlatformRotation_Stairs);
        }

        static void Toggle(Transform t, bool on)
        {
            if (!t) return;
            if (t.gameObject.activeSelf != on) t.gameObject.SetActive(on);
        }

        void ApplyTunablesInEditor()
        {
#if UNITY_EDITOR
            if (!preset) return;
            switch (preset.systemType)
            {
                case IKSystemType.IK_Distance:
                case IKSystemType.IK_DistanceInverse:
                    // Distance/Inverse: se configuran desde la jerarquía (no tocamos nada aquí)
                    break;

                case IKSystemType.IK_PlatformMovement:
                    ApplyToPathFollower1(target_IK_PlatformMovement,
                        preset.movement_speed,
                        preset.movement_distanceThreshold);
                    break;

                case IKSystemType.IK_PlatformRotation:
                    ApplyToFollowMultipleTargetController(target_IK_PlatformRotation,
                        preset.rotation_remapLerp,
                        preset.rotation_arcHeight,
                        preset.rotation_moveDelay,
                        preset.rotation_travelTime,
                        preset.rotation_stopDuration);
                    break;

                case IKSystemType.IK_PlatformRotation_Stairs:
                    if (preset.stairs_useRotationValues)
                    {
                        ApplyToFollowMultipleTargetController(target_IK_PlatformRotation_Stairs,
                            preset.rotation_remapLerp,
                            preset.rotation_arcHeight,
                            preset.rotation_moveDelay,
                            preset.rotation_travelTime,
                            preset.rotation_stopDuration);
                    }
                    else
                    {
                        ApplyToFollowMultipleTargetController(target_IK_PlatformRotation_Stairs,
                            preset.stairs_remapLerp,
                            preset.stairs_arcHeight,
                            preset.stairs_moveDelay,
                            preset.stairs_travelTime,
                            preset.stairs_stopDuration);
                    }
                    break;
            }
#endif
        }

        // Mantener escala mundo al reparentar
        static void SetWorldScale(Transform t, Vector3 worldScale)
        {
            var p = t.parent;
            if (!p) { t.localScale = worldScale; return; }
            var inv = new Vector3(
                p.lossyScale.x != 0f ? 1f / p.lossyScale.x : 1f,
                p.lossyScale.y != 0f ? 1f / p.lossyScale.y : 1f,
                p.lossyScale.z != 0f ? 1f / p.lossyScale.z : 1f);
            t.localScale = Vector3.Scale(worldScale, inv);
        }

        // Pasar StasisTipController a cada bone (sin .Find)
        void TryAssignTipControllerToBone(GameObject boneGO, Component tipController)
        {
            if (!boneGO || !tipController) return;
            var receivers = boneGO.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var r in receivers)
                if (r is IStasisPartIK iface) iface.SetTipController(tipController);

            // fallback por nombres comunes si algún script no implementa la interfaz
            string[] names = { "stasisTipController", "StasisTipController", "tipController", "TipController", "stasisTipCtrl", "StasisTipCtrl" };
            foreach (var r in receivers)
            {
                var t = r.GetType();
                foreach (var n in names)
                {
                    var f = t.GetField(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (f != null && (tipController == null || f.FieldType.IsAssignableFrom(tipController.GetType())))
                        f.SetValue(r, tipController);

                    var p = t.GetProperty(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (p != null && p.CanWrite && (tipController == null || p.PropertyType.IsAssignableFrom(tipController.GetType())))
                        p.SetValue(r, tipController, null);
                }
            }
        }

        // =====================================================================
        // Rig / RigBuilder guards
        // =====================================================================
        void EnsureRigComponents()
        {
            _rigBuilder = arm ? arm.GetComponent<RigBuilder>() : null;
            if (arm && _rigBuilder == null) _rigBuilder = arm.gameObject.AddComponent<RigBuilder>();

            _rig = rigObject ? rigObject.GetComponent<Rig>() : null;
            if (rigObject && _rig == null) _rig = rigObject.gameObject.AddComponent<Rig>();

            if (_rigBuilder != null)
            {
                // Dejar una sola capa, apuntando a nuestro Rig
                for (int i = _rigBuilder.layers.Count - 1; i >= 0; i--)
                {
                    var layer = _rigBuilder.layers[i];
                    if (layer.rig == null || (_rig != null && layer.rig != _rig))
                        _rigBuilder.layers.RemoveAt(i);
                }
                bool listed = false;
                foreach (var l in _rigBuilder.layers) { if (l.rig == _rig) { listed = true; break; } }
                if (!listed && _rig != null) _rigBuilder.layers.Add(new RigLayer(_rig, true));
            }
        }

        // =====================================================================
        // Helpers de modo
        // =====================================================================
        GameObject GetBonePrefabForMode(IKSystemType mode)
        {
            switch (mode)
            {
                case IKSystemType.IK_Distance:
                case IKSystemType.IK_DistanceInverse: return bonePrefab_Distance;
                case IKSystemType.IK_PlatformMovement: return bonePrefab_PlatformMovement;
                case IKSystemType.IK_PlatformRotation:
                case IKSystemType.IK_PlatformRotation_Stairs: return bonePrefab_Rotation;
            }
            return null;
        }

        GameObject GetTipPrefabForMode(IKSystemType mode)
        {
            switch (mode)
            {
                case IKSystemType.IK_Distance:
                case IKSystemType.IK_DistanceInverse: return tipPrefab_Distance_MeshOn;
                default: return tipPrefab_MeshOff;
            }
        }

        Transform GetTargetForMode(IKSystemType mode)
        {
            switch (mode)
            {
                case IKSystemType.IK_Distance: return target_IK_Distance;
                case IKSystemType.IK_DistanceInverse: return target_IK_DistanceInverse;
                case IKSystemType.IK_PlatformMovement: return target_IK_PlatformMovement;
                case IKSystemType.IK_PlatformRotation: return target_IK_PlatformRotation;
                case IKSystemType.IK_PlatformRotation_Stairs: return target_IK_PlatformRotation_Stairs;
            }
            return null;
        }

        Component GetStasisTipControllerForMode(IKSystemType mode)
        {
            switch (mode)
            {
                case IKSystemType.IK_Distance: return stasisTipCtrl_Distance;
                case IKSystemType.IK_DistanceInverse: return stasisTipCtrl_Inverse ? stasisTipCtrl_Inverse : stasisTipCtrl_Distance;
                case IKSystemType.IK_PlatformMovement: return stasisTipCtrl_Movement;
                case IKSystemType.IK_PlatformRotation: return stasisTipCtrl_Rotation;
                case IKSystemType.IK_PlatformRotation_Stairs: return stasisTipCtrl_Stairs ? stasisTipCtrl_Stairs : stasisTipCtrl_Rotation;
            }
            return null;
        }

        // =====================================================================
        // Validación
        // =====================================================================
        bool ValidateSetup()
        {
            if (!preset) return false;
            if (!arm) return false;
            if (!root) return false;
            if (!rigObject) return false;
            if (!GetBonePrefabForMode(preset.systemType)) return false;
            if (!GetTipPrefabForMode(preset.systemType)) return false;

            // Tanto root como rigObject DEBEN colgar de arm (Animator) y ser INSTANCIAS DE ESCENA
            if (!IsDescendantOf(root, arm) || !root.gameObject.scene.IsValid())
            {
                Debug.LogError("[IK] 'root' must be a Scene instance and a descendant of 'arm' (Animator).", this);
                return false;
            }
            if (!IsDescendantOf(rigObject, arm) || !rigObject.gameObject.scene.IsValid())
            {
                Debug.LogError("[IK] 'rigObject' (Rig + ChainIKConstraint) must be a Scene instance and a descendant of 'arm'.", this);
                return false;
            }
            return true;
        }

        static bool IsDescendantOf(Transform child, Transform potentialAncestor)
        {
            if (!child || !potentialAncestor) return false;
            var t = child;
            while (t != null)
            {
                if (t == potentialAncestor) return true;
                t = t.parent;
            }
            return false;
        }

#if UNITY_EDITOR
        int ComputePresetHash()
        {
            if (!preset) return 0;
            unchecked { return (int)preset.systemType * 23 + preset.boneCount; }
        }

        // Setters por SerializedObject para scripts externos
        static void ApplyToPathFollower1(Transform target, float speed, float distanceThreshold)
        {
            if (!target) return;
            var mb = target.GetComponent<MonoBehaviour>();
            if (!mb) return;
            var so = new SerializedObject(mb);
            TrySetFloat(so, "speed", speed);
            TrySetFloat(so, "distanceThreshold", distanceThreshold);
            so.ApplyModifiedProperties();
        }

        static void ApplyToFollowMultipleTargetController(Transform target, AnimationCurve remap, float arcHeight, float moveDelay, float travelTime, float stopDuration)
        {
            if (!target) return;
            var mb = target.GetComponent<MonoBehaviour>();
            if (!mb) return;
            var so = new SerializedObject(mb);
            TrySetCurve(so, "remapLerp", remap);
            TrySetFloat(so, "arcHeight", arcHeight);
            TrySetFloat(so, "moveDelay", moveDelay);
            TrySetFloat(so, "travelTime", travelTime);
            TrySetFloat(so, "stopDuration", stopDuration);
            so.ApplyModifiedProperties();
        }

        static bool TrySetFloat(SerializedObject so, string prop, float value)
        {
            var p = so.FindProperty(prop);
            if (p == null) return false;
            p.floatValue = value;
            return true;
        }

        static bool TrySetCurve(SerializedObject so, string prop, AnimationCurve curve)
        {
            var p = so.FindProperty(prop);
            if (p == null) return false;
            p.animationCurveValue = curve;
            return true;
        }
#endif
    }
}
