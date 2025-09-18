// Assets/Scripts/IKSuite/IKObjectController.cs
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace IKSuite
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class IKObjectController : MonoBehaviour
    {
        [Header("Preset (enum + tunables)")]
        public IKObjectPreset preset;

        [Header("Estructura de escena")]
        public Transform arm;        // Animator + RigBuilder
        public Transform root;       // cadena de bones (hijo de Arm)
        public Transform rigObject;  // tiene Rig (ChainIKConstraint se configura en runtime)
        public Transform sistemas;   // opcional: agrupador

        [Header("Contenedores de cada sistema (se activan/ocultan en editor)")]
        public Transform sys_IK_Distance;
        public Transform sys_IK_DistanceInverse;
        public Transform sys_IK_PlatformMovement;
        public Transform sys_IK_PlatformRotation;
        public Transform sys_IK_PlatformRotation_Stairs;

        [Header("Targets (arrastrar todos)")]
        public Transform target_IK_Distance;                 // TipController (Distance)
        public Transform target_IK_DistanceInverse;          // TipController (Inverse)
        public Transform target_IK_PlatformMovement;         // PlatformM
        public Transform target_IK_PlatformRotation;         // Platform
        public Transform target_IK_PlatformRotation_Stairs;  // Platform (Escaleras)

        [Header("Stasis Tip Controllers (componentes exactos)")]
        public Component stasisTipCtrl_Distance;
        public Component stasisTipCtrl_Inverse;   // si null, usa Distance
        public Component stasisTipCtrl_Movement;
        public Component stasisTipCtrl_Rotation;
        public Component stasisTipCtrl_Stairs;    // si null, usa Rotation

        [Header("Prefabs (se asignan AQUÍ, no en el preset)")]
        public GameObject bonePrefab_Distance;
        public GameObject bonePrefab_PlatformMovement;
        public GameObject bonePrefab_Rotation;

        public GameObject tipPrefab_Distance_MeshOn;
        public GameObject tipPrefab_MeshOff;

        // cache runtime
        ChainIKConstraint _chain;
        RigBuilder _rigBuilder;
        Rig _rig;

        readonly List<Transform> _generatedBones = new();
        Transform _generatedTip;

        // ---------- lifecycle ----------
        void OnEnable()
        {
            ApplySystemVisibilityInEditor();
            ApplyTunablesInEditor();   // importantes en editor
            if (Application.isPlaying) BuildAtRuntime();
        }

        void OnDisable()
        {
            if (Application.isPlaying) CleanupRuntime();
        }

        void OnValidate()
        {
            ApplySystemVisibilityInEditor();
            ApplyTunablesInEditor();
        }

        // ---------- EDITOR: mostrar solo el sistema elegido ----------
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

        // ---------- EDITOR: aplicar tunables en los componentes destino ----------
        void ApplyTunablesInEditor()
        {
            if (!preset) return;

#if UNITY_EDITOR
            switch (preset.systemType)
            {
                case IKSystemType.IK_Distance:
                    ApplyToFollowTargetController(target_IK_Distance,
                        preset.distance_remapLerp,
                        preset.distance_moveDuration,
                        preset.distance_outMin,
                        preset.distance_outMax);
                    break;

                case IKSystemType.IK_DistanceInverse:
                    float outMin = preset.inverse_overrideOut ? preset.inverse_outMin : preset.distance_outMin;
                    float outMax = preset.inverse_overrideOut ? preset.inverse_outMax : preset.distance_outMax;
                    ApplyToFollowTargetController(target_IK_DistanceInverse,
                        preset.distance_remapLerp,
                        preset.distance_moveDuration,
                        outMin, outMax);
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
                        ApplyToFollowMultipleTargetController(target_IK_PlatformRotation_Stairs,
                            preset.rotation_remapLerp,
                            preset.rotation_arcHeight,
                            preset.rotation_moveDelay,
                            preset.rotation_travelTime,
                            preset.rotation_stopDuration);
                    else
                        ApplyToFollowMultipleTargetController(target_IK_PlatformRotation_Stairs,
                            preset.stairs_remapLerp,
                            preset.stairs_arcHeight,
                            preset.stairs_moveDelay,
                            preset.stairs_travelTime,
                            preset.stairs_stopDuration);
                    break;
            }
#endif
        }

#if UNITY_EDITOR
        // ------- Setters por SerializedObject (sin tipos duros) -------
        static void ApplyToFollowTargetController(Transform target, AnimationCurve remap, float moveDuration, float outMin, float outMax)
        {
            if (!target) return;
            var mb = target.GetComponent<MonoBehaviour>();  // asume 1 controller principal en ese GO
            if (!mb) return;

            var so = new SerializedObject(mb);
            bool changed = false;

            changed |= TrySetCurve(so, "remapLerp", remap);
            changed |= TrySetFloat(so, "moveDuration", moveDuration);
            changed |= TrySetFloat(so, "outMin", outMin);
            changed |= TrySetFloat(so, "outMax", outMax);

            if (changed) so.ApplyModifiedProperties();
        }

        static void ApplyToPathFollower1(Transform target, float speed, float distanceThreshold)
        {
            if (!target) return;
            var mb = target.GetComponent<MonoBehaviour>();
            if (!mb) return;

            var so = new SerializedObject(mb);
            bool changed = false;

            changed |= TrySetFloat(so, "speed", speed);
            changed |= TrySetFloat(so, "distanceThreshold", distanceThreshold);

            if (changed) so.ApplyModifiedProperties();
        }

        static void ApplyToFollowMultipleTargetController(Transform target, AnimationCurve remap, float arcHeight, float moveDelay, float travelTime, float stopDuration)
        {
            if (!target) return;
            var mb = target.GetComponent<MonoBehaviour>();
            if (!mb) return;

            var so = new SerializedObject(mb);
            bool changed = false;

            changed |= TrySetCurve(so, "remapLerp", remap);
            changed |= TrySetFloat(so, "arcHeight", arcHeight);
            changed |= TrySetFloat(so, "moveDelay", moveDelay);
            changed |= TrySetFloat(so, "travelTime", travelTime);
            changed |= TrySetFloat(so, "stopDuration", stopDuration);

            if (changed) so.ApplyModifiedProperties();
        }

        static bool TrySetFloat(SerializedObject so, string propName, float value)
        {
            var p = so.FindProperty(propName);
            if (p == null) return false;
            p.floatValue = value;
            return true;
        }
        static bool TrySetCurve(SerializedObject so, string propName, AnimationCurve curve)
        {
            var p = so.FindProperty(propName);
            if (p == null) return false;
            p.animationCurveValue = curve;
            return true;
        }
#endif

        // ---------- RUNTIME: construcción de cadena ----------
        void BuildAtRuntime()
        {
            if (!ValidateSetup()) return;
            EnsureRigComponents();

            var bonePrefab = GetBonePrefabForMode(preset.systemType);
            var tipPrefab = GetTipPrefabForMode(preset.systemType);
            var target = GetTargetForMode(preset.systemType);
            var stasisTC = GetStasisTipControllerForMode(preset.systemType);

            CleanupRuntime();

            Transform firstBone = null;
            Transform prevBone = null;
            Transform prevEnd = null;
            Vector3 firstBoneWorldScale = Vector3.one;

            int count = Mathf.Max(1, preset.boneCount);
            for (int i = 0; i < count; i++)
            {
                var boneGO = Instantiate(bonePrefab, i == 0 ? root : prevBone);
                boneGO.name = $"Bone_{i + 1}";
                var boneT = boneGO.transform;
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
                    if (!prevEnd)
                    {
                        Debug.LogError("[IKObjectController] Cadena incompleta: falta 'end' del bone anterior.");
                        return;
                    }
                    boneT.localPosition = prevEnd.localPosition;
                    boneT.localRotation = prevEnd.localRotation;
                    SetWorldScale(boneT, firstBoneWorldScale);
                }

                TryAssignTipControllerToBone(boneGO, stasisTC);

                var be = boneGO.GetComponentInChildren<BoneEnd>(true);
                if (!be || !be.GetEnd())
                {
                    Debug.LogError($"[IKObjectController] El prefab '{bonePrefab.name}' debe tener BoneEnd.end asignado.");
                    return;
                }
                prevEnd = be.GetEnd();
                prevBone = boneT;
            }

            // TIP bajo el END del último bone
            var tipGO = Instantiate(tipPrefab, prevEnd);
            tipGO.name = "TIP";
            var tipT = tipGO.transform;
            tipT.localPosition = Vector3.zero;
            tipT.localRotation = Quaternion.identity;
            _generatedTip = tipT;

            // IK
            _chain = rigObject.GetComponent<ChainIKConstraint>();
            if (_chain == null) _chain = rigObject.gameObject.AddComponent<ChainIKConstraint>();

            var data = _chain.data;
            data.root = firstBone;
            data.tip = tipT;
            data.target = target;
            _chain.data = data;
            _chain.weight = 1f;

            _rigBuilder.Build();
        }

        // ---------- helpers ----------
        static Vector3 Reciprocal(Vector3 v) =>
            new Vector3(v.x != 0f ? 1f / v.x : 1f, v.y != 0f ? 1f / v.y : 1f, v.z != 0f ? 1f / v.z : 1f);

        static void SetWorldScale(Transform t, Vector3 worldScale)
        {
            var p = t.parent;
            if (!p) { t.localScale = worldScale; return; }
            var inv = Reciprocal(p.lossyScale);
            t.localScale = Vector3.Scale(worldScale, inv);
        }

        void TryAssignTipControllerToBone(GameObject boneGO, Component tipController)
        {
            if (!boneGO || !tipController) return;

            var receivers = boneGO.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var r in receivers)
                if (r is IStasisPartIK iface) iface.SetTipController(tipController);

            // fallback por reflection
            string[] names = { "stasisTipController", "StasisTipController", "tipController", "TipController", "stasisTipCtrl", "StasisTipCtrl" };
            foreach (var r in receivers)
            {
                var t = r.GetType();
                foreach (var n in names)
                {
                    var f = t.GetField(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (f != null && f.FieldType.IsAssignableFrom(tipController.GetType()))
                        f.SetValue(r, tipController);

                    var p = t.GetProperty(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (p != null && p.CanWrite && p.PropertyType.IsAssignableFrom(tipController.GetType()))
                        p.SetValue(r, tipController);
                }
            }
        }

        bool ValidateSetup()
        {
            if (!preset) { Debug.LogError("[IKObjectController] Falta 'preset'"); return false; }
            if (!arm || !root || !rigObject) { Debug.LogError("[IKObjectController] Faltan Arm/Root/RigObject"); return false; }

            if (!GetBonePrefabForMode(preset.systemType)) { Debug.LogError($"[IKObjectController] Falta bone prefab para {preset.systemType}"); return false; }
            if (!GetTipPrefabForMode(preset.systemType)) { Debug.LogError($"[IKObjectController] Falta tip prefab para {preset.systemType}"); return false; }
            if (!GetTargetForMode(preset.systemType)) { Debug.LogError($"[IKObjectController] Falta Target para {preset.systemType}"); return false; }
            if (!GetStasisTipControllerForMode(preset.systemType)) { Debug.LogError($"[IKObjectController] Falta Stasis Tip Controller para {preset.systemType}"); return false; }

            return true;
        }

        void EnsureRigComponents()
        {
            _rigBuilder = arm.GetComponent<RigBuilder>();
            if (_rigBuilder == null) _rigBuilder = arm.gameObject.AddComponent<RigBuilder>();

            _rig = rigObject.GetComponent<Rig>();
            if (_rig == null) _rig = rigObject.gameObject.AddComponent<Rig>();

            bool listed = false;
            foreach (var l in _rigBuilder.layers)
                if (l.rig == _rig) { listed = true; break; }
            if (!listed) _rigBuilder.layers.Add(new RigLayer(_rig, true));
        }

        GameObject GetBonePrefabForMode(IKSystemType mode) =>
            mode switch
            {
                IKSystemType.IK_Distance or IKSystemType.IK_DistanceInverse => bonePrefab_Distance,
                IKSystemType.IK_PlatformMovement => bonePrefab_PlatformMovement,
                IKSystemType.IK_PlatformRotation or IKSystemType.IK_PlatformRotation_Stairs
                                                                              => bonePrefab_Rotation,
                _ => null
            };

        GameObject GetTipPrefabForMode(IKSystemType mode) =>
            mode switch
            {
                IKSystemType.IK_Distance or IKSystemType.IK_DistanceInverse => tipPrefab_Distance_MeshOn,
                _ => tipPrefab_MeshOff
            };

        Transform GetTargetForMode(IKSystemType mode) =>
            mode switch
            {
                IKSystemType.IK_Distance => target_IK_Distance,
                IKSystemType.IK_DistanceInverse => target_IK_DistanceInverse,
                IKSystemType.IK_PlatformMovement => target_IK_PlatformMovement,
                IKSystemType.IK_PlatformRotation => target_IK_PlatformRotation,
                IKSystemType.IK_PlatformRotation_Stairs => target_IK_PlatformRotation_Stairs,
                _ => null
            };

        Component GetStasisTipControllerForMode(IKSystemType mode) =>
            mode switch
            {
                IKSystemType.IK_Distance => stasisTipCtrl_Distance,
                IKSystemType.IK_DistanceInverse => stasisTipCtrl_Inverse ? stasisTipCtrl_Inverse : stasisTipCtrl_Distance,
                IKSystemType.IK_PlatformMovement => stasisTipCtrl_Movement,
                IKSystemType.IK_PlatformRotation => stasisTipCtrl_Rotation,
                IKSystemType.IK_PlatformRotation_Stairs => stasisTipCtrl_Stairs ? stasisTipCtrl_Stairs : stasisTipCtrl_Rotation,
                _ => null
            };

        void CleanupRuntime()
        {
            foreach (var t in _generatedBones)
                if (t) Destroy(t.gameObject);
            _generatedBones.Clear();

            if (_generatedTip)
            {
                Destroy(_generatedTip.gameObject);
                _generatedTip = null;
            }
        }
    }
}
