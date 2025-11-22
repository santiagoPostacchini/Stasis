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
            [Tooltip("Velocidad base en grados/segundo. En Ping-Pong se usa el valor absoluto.")]
            public float speed = 90f;

            [Header("Espacio & Tiempo")]
            public SpaceMode space = SpaceMode.Local;
            public TimeMode timeMode = TimeMode.Scaled;

            [Header("Extras")]
            [Tooltip("Randomiza el ángulo inicial entre 0 y 360 grados.")]
            public bool randomizeStartAngle = false;

            [Header("Actualización")]
            [Tooltip("Si está activo, este item se actualizará en FixedUpdate (ideal para cosas que interactúan con física). " +
                     "Si está desactivado, se actualiza en Update (ideal para engranajes puramente visuales).")]
            public bool useFixedUpdate = false;

            [Header("Rotación limitada")]
            [Tooltip("Si está activo, este item usa un ángulo máximo acumulado.")]
            public bool useAngleLimit = false;

            [Tooltip("Ángulo máximo (valor absoluto) en grados. Ej: 90 para un cuarto de vuelta.")]
            [Min(0f)] public float maxAngleAbs = 90f;

            [Tooltip("Si está activo, el engranaje oscilará entre -maxAngleAbs y +maxAngleAbs (modo ping-pong).")]
            public bool usePingPong = false;

            [Header("Easing cerca del límite")]
            [Tooltip("Suaviza la velocidad al acercarse al ángulo máximo (aplica a modo limitado y ping-pong).")]
            public bool useEaseNearLimit = false;

            [Tooltip("Fracción del recorrido donde empieza a frenar (0.25 = último 25%).")]
            [Range(0.01f, 1f)] public float easePortion = 0.25f;

            [Tooltip("Factor mínimo de velocidad en el borde (1 = sin freno, 0.1 = muy suave al final).")]
            [Range(0.05f, 1f)] public float minEaseFactor = 0.2f;

            [NonSerialized] internal Vector3 cachedAxis = Vector3.up;
            [NonSerialized] internal bool axisValid = true;
            [NonSerialized] internal bool initialized = false;

            // Para modos limitados:
            [NonSerialized] internal float accumulatedAngle = 0f; // ángulo relativo actual (para limitado y ping-pong)

            // Para ping-pong:
            [NonSerialized] internal float pingPongTime = 0f;     // tiempo acumulado para la función PingPong
            [NonSerialized] internal Quaternion baseRotation;     // rotación base desde la cual se aplica el ángulo relativo
        }

        [Header("Lista de engranajes a rotar")]
        public List<RotatorItem> items = new List<RotatorItem>();

        [Header("Opciones globales")]
        [Tooltip("Permite que la rotación ocurra también en modo edición (sin Play).")]
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

        /// <summary>
        /// Pausa la rotación de un engranaje específico.
        /// </summary>
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

        /// <summary>
        /// Reanuda la rotación de un engranaje específico.
        /// </summary>
        public bool ResumeItem(Transform t)
        {
            if (t == null) return false;
            return _pausedItems.Remove(t);
        }

        /// <summary>
        /// Resetea el ángulo relativo de un engranaje (útil para modo limitado/ping-pong).
        /// </summary>
        public bool ResetItemAngle(Transform t)
        {
            if (t == null) return false;
            if (!_indexByTarget.TryGetValue(t, out int index)) return false;

            var it = items[index];
            if (it == null || it.target == null) return false;

            it.accumulatedAngle = 0f;
            it.pingPongTime = 0f;

            // Volvemos a la rotación base almacenada
            if (it.space == SpaceMode.Local)
                it.target.localRotation = it.baseRotation;
            else
                it.target.rotation = it.baseRotation;

            return true;
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
                if (!it.axisValid) continue;

                // Tipo de paso
                if (it.useFixedUpdate != isFixedStep) continue;
                if (Mathf.Abs(it.speed) < 0.0001f) continue;

                // Delta de tiempo
                float dt;
                if (it.timeMode == TimeMode.Scaled)
                    dt = isFixedStep ? Time.fixedDeltaTime : Time.deltaTime;
                else
                    dt = isFixedStep ? Time.fixedUnscaledDeltaTime : Time.unscaledDeltaTime;

                // ================== MODO PING-PONG ==================
                if (it.useAngleLimit && it.usePingPong && it.maxAngleAbs > 0f)
                {
                    it.pingPongTime += dt;

                    float absSpeed = Mathf.Abs(it.speed);
                    float range = it.maxAngleAbs * 2f;

                    // PingPong lineal en [0, range]
                    float raw = Mathf.PingPong(it.pingPongTime * absSpeed, range); // 0..2*max
                    float norm = (range > 0f) ? (raw / range) : 0f;                 // 0..1

                    // Si queremos easing, aplicamos SmoothStep al parámetro
                    float easedNorm = it.useEaseNearLimit
                        ? Mathf.SmoothStep(0f, 1f, norm)
                        : norm;

                    // Mapeamos de 0..1 a -max..+max
                    float targetAngle = Mathf.Lerp(-it.maxAngleAbs, it.maxAngleAbs, easedNorm);

                    it.accumulatedAngle = targetAngle;

                    Quaternion rotOffset = Quaternion.AngleAxis(targetAngle, it.cachedAxis);

                    if (it.space == SpaceMode.Local)
                        it.target.localRotation = it.baseRotation * rotOffset;
                    else
                        it.target.rotation = it.baseRotation * rotOffset;

                    continue;
                }

                // ================== RESTO DE MODOS ==================
                float angleStep = it.speed * dt;

                // ================== MODO LIBRE (SIN LÍMITE) ==================
                if (!it.useAngleLimit || it.maxAngleAbs <= 0f)
                {
                    if (Mathf.Approximately(angleStep, 0f)) continue;

                    if (it.space == SpaceMode.Local)
                        it.target.Rotate(it.cachedAxis, angleStep, Space.Self);
                    else
                        it.target.Rotate(it.cachedAxis, angleStep, Space.World);

                    continue;
                }

                // ================== MODO LIMITADO UNA VEZ ==================
                float usedAbs = Mathf.Abs(it.accumulatedAngle);
                float remainingAbs = it.maxAngleAbs - usedAbs;

                if (remainingAbs <= 0f) continue;

                float stepAbs = Mathf.Abs(angleStep);

                // --- EASING CERCA DEL LÍMITE ---
                if (it.useEaseNearLimit && it.maxAngleAbs > 0f)
                {
                    float progress = usedAbs / it.maxAngleAbs; // 0..1 de cuánto ya giró
                    float easeStart = 1f - it.easePortion;     // p.ej. 0.75 si easePortion=0.25

                    float tEase = Mathf.InverseLerp(easeStart, 1f, progress); // 0 fuera de la zona, 1 en el borde
                    float easeFactor = 1f;

                    if (tEase > 0f)
                    {
                        float smooth = Mathf.SmoothStep(0f, 1f, tEase);
                        easeFactor = Mathf.Lerp(1f, it.minEaseFactor, smooth);
                    }

                    stepAbs *= easeFactor;
                    angleStep = Mathf.Sign(angleStep) * stepAbs;
                }

                // Nos aseguramos de no pasarnos del límite en este frame
                stepAbs = Mathf.Abs(angleStep);
                if (stepAbs > remainingAbs)
                    angleStep = Mathf.Sign(angleStep) * remainingAbs;

                if (Mathf.Approximately(angleStep, 0f)) continue;

                if (it.space == SpaceMode.Local)
                    it.target.Rotate(it.cachedAxis, angleStep, Space.Self);
                else
                    it.target.Rotate(it.cachedAxis, angleStep, Space.World);

                it.accumulatedAngle += angleStep;
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

                // Reset de ángulos relativos
                it.accumulatedAngle = 0f;
                it.pingPongTime = 0f;

                // Rotación inicial aleatoria si corresponde
                if (it.randomizeStartAngle && it.axisValid)
                {
                    float startAngle = UnityEngine.Random.Range(0f, 360f);
                    if (it.space == SpaceMode.Local)
                        it.target.Rotate(it.cachedAxis, startAngle, Space.Self);
                    else
                        it.target.Rotate(it.cachedAxis, startAngle, Space.World);
                }

                // Guardamos la rotación base después de aplicar random start
                if (it.space == SpaceMode.Local)
                    it.baseRotation = it.target.localRotation;
                else
                    it.baseRotation = it.target.rotation;

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
