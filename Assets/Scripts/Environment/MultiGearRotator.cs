using System;
using System.Collections.Generic;
using UnityEngine;

namespace Environment
{
    [AddComponentMenu("Gameplay/MultiGearRotator (rotación de múltiples engranajes)")]
    public class MultiGearRotator : MonoBehaviour
    {
        [Serializable]
        public enum Axis { X, Y, Z, Custom }
        [Serializable]
        public enum SpaceMode { Local, World }
        [Serializable]
        public enum TimeMode { Scaled, Unscaled }

        [Serializable]
        public class RotatorItem
        {
            [Header("Objeto a rotar")]
            public Transform target;

            [Header("Eje de rotación")]
            public Axis axis = Axis.Y;
            public Vector3 customAxis = Vector3.up;

            [Header("Velocidad"), Range(-720f, 720f)]
            public float speed = 90f;

            [Header("Espacio & Tiempo")]
            public SpaceMode space = SpaceMode.Local;
            public TimeMode timeMode = TimeMode.Scaled;

            [Header("Extras")]
            public bool randomizeStartAngle = false;

            [Header("Actualización")]
            [Tooltip("Si está activo, este item se actualizará en FixedUpdate (ideal para cosas que interactúan con física). " +
                     "Si está desactivado, se actualiza en Update (ideal para engranajes puramente visuales).")]
            public bool useFixedUpdate = false;

            [NonSerialized] internal Vector3 cachedAxis = Vector3.up;
            [NonSerialized] internal bool axisValid = true;
            [NonSerialized] internal bool initialized = false;
        }

        [Header("Lista de engranajes a rotar")]
        public List<RotatorItem> items = new List<RotatorItem>();

        [Header("Opciones globales")]
        public bool runInEditMode = false;

        [Header("Runtime Control")]
        [Tooltip("Si está activo, pausa TODAS las rotaciones gestionadas por este componente.")]
        public bool paused = false;

        private readonly HashSet<Transform> _pausedItems = new HashSet<Transform>(); // per-item
        private readonly Dictionary<Transform, int> _indexByTarget = new Dictionary<Transform, int>(128); // fast lookup

#if UNITY_EDITOR
        private void OnValidate()
        {
            RebuildAxisCache();
            RebuildIndex();
        }
#endif

        private void Awake()
        {
            RebuildAxisCache();
            InitializeRandomStarts();
            RebuildIndex();
        }

        // ===================== API pública per-item =====================
        public bool PauseItem(Transform t)
        {
            if (t == null) return false;
            if (!_indexByTarget.ContainsKey(t))
            {
                Debug.LogWarning($"[MultiGearRotator] PauseItem: '{t.name}' no está en items.", this);
                return false;
            }
            _pausedItems.Add(t);
            return true;
        }

        public bool ResumeItem(Transform t)
        {
            if (t == null) return false;
            return _pausedItems.Remove(t);
        }

        public void PauseAll() => paused = true;
        public void ResumeAll() => paused = false;

        // ===================== Ciclos de actualización =====================

        private void Update()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && !runInEditMode) return;
#endif
            if (paused) return;

            // false = este frame es de Update normal
            RotateItems(isFixedStep: false);
        }

        private void FixedUpdate()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && !runInEditMode) return;
#endif
            if (paused) return;

            // true = este frame es del paso de física
            RotateItems(isFixedStep: true);
        }

        /// <summary>
        /// Aplica la rotación a todos los items que correspondan a este tipo de paso (Update o FixedUpdate).
        /// </summary>
        private void RotateItems(bool isFixedStep)
        {
            int count = items != null ? items.Count : 0;
            for (int i = 0; i < count; i++)
            {
                var it = items[i];
                if (it == null || it.target == null) continue;
                if (_pausedItems.Contains(it.target)) continue;

                // Filtramos por el tipo de actualización elegido para este item
                if (it.useFixedUpdate != isFixedStep) continue;

                if (Mathf.Abs(it.speed) < 0.0001f) continue;
                if (!it.axisValid) continue;

                // Elegimos el delta de tiempo según el modo y el tipo de paso
                float dt;
                if (it.timeMode == TimeMode.Scaled)
                {
                    dt = isFixedStep ? Time.fixedDeltaTime : Time.deltaTime;
                }
                else
                {
                    dt = isFixedStep ? Time.fixedUnscaledDeltaTime : Time.unscaledDeltaTime;
                }

                float angle = it.speed * dt;
                if (Mathf.Approximately(angle, 0f)) continue;

                if (it.space == SpaceMode.Local)
                    it.target.Rotate(it.cachedAxis, angle, Space.Self);
                else
                    it.target.Rotate(it.cachedAxis, angle, Space.World);
            }
        }

        // ===================== Helpers =====================
        private void RebuildAxisCache()
        {
            int count = items != null ? items.Count : 0;
            for (int i = 0; i < count; i++)
            {
                var it = items[i];
                if (it == null) continue;

                Vector3 axisVec = it.axis switch
                {
                    Axis.X => Vector3.right,
                    Axis.Y => Vector3.up,
                    Axis.Z => Vector3.forward,
                    Axis.Custom => it.customAxis,
                    _ => Vector3.up
                };

                if (axisVec.magnitude > 1e-5f)
                {
                    it.cachedAxis = axisVec.normalized;
                    it.axisValid = true;
                }
                else
                {
                    it.cachedAxis = Vector3.up;
                    it.axisValid = false;
                }
            }
        }

        private void InitializeRandomStarts()
        {
            int count = items != null ? items.Count : 0;
            for (int i = 0; i < count; i++)
            {
                var it = items[i];
                if (it == null || it.target == null) continue;
                if (it.initialized) continue;

                if (it.randomizeStartAngle && it.axisValid)
                {
                    float startAngle = UnityEngine.Random.Range(0f, 360f);
                    if (it.space == SpaceMode.Local)
                        it.target.Rotate(it.cachedAxis, startAngle, Space.Self);
                    else
                        it.target.Rotate(it.cachedAxis, startAngle, Space.World);
                }

                it.initialized = true;
            }
        }

        private void RebuildIndex()
        {
            _indexByTarget.Clear();
            int count = items != null ? items.Count : 0;
            for (int i = 0; i < count; i++)
            {
                var it = items[i];
                if (it?.target == null) continue;

                _indexByTarget.TryAdd(it.target, i);
            }
        }
    }
}
