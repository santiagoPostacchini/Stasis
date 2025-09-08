using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Puzzle_Elements.LaunchPlate.Scripts
{
    [RequireComponent(typeof(Collider))]
    public class LaunchPlate : MonoBehaviour
    {
        [Tooltip("Que tanto afectan los inputs del player al desfase de la trayectoria del pad")]
        [Range(0,1)]
        [SerializeField] private float playerControlFactor = 0.5f;

        [Header("Trajectories (use children as control points)")]
        [SerializeField] private Transform playerTrajectoryParent;
        [SerializeField] private Transform objectTrajectoryParent;

        [Header("Line Renderers (optional)")]
        [SerializeField] private LineRenderer playerLineRenderer;
        [SerializeField] private LineRenderer objectLineRenderer;
        [SerializeField] private Color playerColor = Color.cyan;
        [SerializeField] private Color objectColor = Color.yellow;

        [Header("Motion")]
        [Tooltip("Tiempo total para recorrer la curva completa (seg).")]
        [SerializeField] private float totalLaunchTime = 1.35f;
        [Tooltip("Muestras por segmento para construir la tabla de arco (suavidad).")]
        [SerializeField, Range(8, 128)] private int samplesPerSegment = 32;
        [Tooltip("Velocidad extra al salir (se suma a la tangente).")]
        [SerializeField] private float exitSpeedBoost = 0f;

        [Header("Cooldown")]
        [SerializeField] private float cooldown = 0.35f;
        [Tooltip("Evita relanzar mientras hay un lanzamiento activo.")]
        [SerializeField] private bool singleFireWhileActive = true;

        private bool _canLaunch = true;

        void Awake()
        {
            var col = GetComponent<Collider>();
            col.isTrigger = true;
            RedrawAll();
        }

        void OnValidate()
        {
            totalLaunchTime = Mathf.Max(0.05f, totalLaunchTime);
            samplesPerSegment = Mathf.Clamp(samplesPerSegment, 8, 128);
            RedrawAll();
        }

        private void RedrawAll()
        {
            DrawTrajectory(playerTrajectoryParent, playerLineRenderer, playerColor);
            DrawTrajectory(objectTrajectoryParent, objectLineRenderer, objectColor);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_canLaunch && singleFireWhileActive) return;

            var rb = other.attachedRigidbody ?? other.GetComponentInParent<Rigidbody>();
            if (!rb) return;

            bool isPlayer = other.CompareTag("Player") || other.transform.root.CompareTag("Player");
            Transform parent = (isPlayer && playerTrajectoryParent) ? playerTrajectoryParent : objectTrajectoryParent;
            if (!parent || parent.childCount < 2) return;

            var sampler = new ArcLengthSpline(parent, samplesPerSegment);
            if (sampler.TotalLength <= 1e-4f) return;

            StartCoroutine(LaunchAlongArc(rb, sampler));
        }

        private IEnumerator LaunchAlongArc(Rigidbody rb, ArcLengthSpline sampler)
        {
            _canLaunch = false;

            var prevInterp = rb.interpolation;
            var prevCCD = rb.collisionDetectionMode;
            var prevUseGravity = rb.useGravity;

            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            // Como forzamos posición sobre la curva, apagamos gravedad para no pelear con MovePosition
            rb.useGravity = false;

            float totalLen = sampler.TotalLength;
            float speedAlongCurve = totalLen / Mathf.Max(0.0001f, totalLaunchTime);

            // Control lateral (solo X relativo a la curva)
            float lateralMaxSpeed = speedAlongCurve * Mathf.Clamp01(playerControlFactor); // m/s lateral
            float lateralAccel = lateralMaxSpeed * 3f;                                 // aceleración lateral

            // Estado de integración
            float s = 0f;
            float elapsed = 0f;
            float dt;
            float inputH;

            // Offset lateral acumulado y su “velocidad” (en metros, no en ejes locales)
            float lateralOffset = 0f;
            float lateralVel = 0f;

            // Posicionar al inicio
            Vector3 startPos = sampler.PointAtArc(0f);
            rb.MovePosition(startPos);
            rb.velocity = Vector3.zero;

            while (elapsed < totalLaunchTime)
            {
                dt = Time.fixedDeltaTime;
                elapsed += dt;

                // Avance a lo largo de la curva (siempre)
                s = Mathf.Min(s + speedAlongCurve * dt, totalLen);

                // Tangente de la curva en s
                Vector3 T = sampler.TangentAtArc(s).normalized;

                // Vector “derecha” relativo a la curva en el plano XZ (binormal plana)
                Vector3 up = Vector3.up;
                Vector3 R = Vector3.Cross(up, T);
                if (R.sqrMagnitude < 1e-6f)
                {
                    // Si la tangente es casi vertical, elegimos una derecha estable
                    R = Vector3.Cross(Vector3.forward, up);
                }
                R.Normalize();

                // Input A/D: solo horizontal
                inputH = Input.GetAxisRaw("Horizontal");

                // Aceleramos la velocidad lateral hacia la deseada (no tocamos Y ni Z del avance)
                float targetLateralVel = inputH * lateralMaxSpeed;
                lateralVel = Mathf.MoveTowards(lateralVel, targetLateralVel, lateralAccel * dt);

                // Integramos el offset lateral (persistente; al soltar no vuelve)
                lateralOffset += lateralVel * dt;

                // Posición base sobre la curva + offset lateral
                Vector3 basePos = sampler.PointAtArc(s);
                Vector3 pos = basePos + R * lateralOffset;

                // Forzamos posición (guiado); anulamos cualquier velocidad residual
                rb.MovePosition(pos);
                rb.velocity = Vector3.zero;

                yield return new WaitForFixedUpdate();
            }

            // Velocidad de salida: dirección combinada (tangente + componente lateral actual)
            Vector3 finalT = sampler.TangentAtArc(s).normalized;
            Vector3 up2 = Vector3.up;
            Vector3 finalR = Vector3.Cross(up2, finalT).normalized;
            Vector3 outDir = (finalT * Mathf.Max(0.1f, speedAlongCurve) + finalR * lateralVel).normalized;

            // Aplicamos empuje extra si corresponde
            rb.useGravity = prevUseGravity; // restauramos gravedad para el vuelo posterior
            rb.velocity = outDir * (speedAlongCurve + Mathf.Max(0f, exitSpeedBoost));

            // Restaurar estado del rigidbody
            rb.interpolation = prevInterp;
            rb.collisionDetectionMode = prevCCD;

            yield return new WaitForSeconds(cooldown);
            _canLaunch = true;
        }



        // =================== Visual helpers ===================
        private void DrawTrajectory(Transform parent, LineRenderer lr, Color color)
        {
            if (!lr) return;

            if (!parent || parent.childCount < 2)
            {
                lr.positionCount = 0;
                return;
            }

            var sampler = new ArcLengthSpline(parent, samplesPerSegment);
            int steps = Mathf.Max(16, samplesPerSegment * (parent.childCount - 1));
            var pts = new Vector3[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                float s = (sampler.TotalLength * i) / steps;
                pts[i] = sampler.PointAtArc(s);
            }
            lr.positionCount = pts.Length;
            lr.SetPositions(pts);
#if UNITY_EDITOR
            lr.startColor = color;
            lr.endColor = color;
#endif
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            void Draw(Transform parent, Color col)
            {
                if (!parent || parent.childCount < 2) return;
                var sampler = new ArcLengthSpline(parent, samplesPerSegment);
                int steps = Mathf.Max(16, samplesPerSegment * (parent.childCount - 1));
                Vector3 prev = sampler.PointAtArc(0f);
                Gizmos.color = col;
                for (int i = 1; i <= steps; i++)
                {
                    float s = (sampler.TotalLength * i) / steps;
                    Vector3 p = sampler.PointAtArc(s);
                    Gizmos.DrawLine(prev, p);
                    prev = p;
                }
            }
            Draw(playerTrajectoryParent, playerColor);
            Draw(objectTrajectoryParent, objectColor);
        }
#endif
    }

    // =================== Arc-length Catmull-Rom spline ===================
    public class ArcLengthSpline
    {
        private readonly List<Vector3> ctrl = new();     // p(-1), p0..pn, p(n+1)
        private readonly List<Vector3> samples = new();  // puntos muestreados
        private readonly List<float> cumLen = new();    // longitud acumulada
        public float TotalLength { get; private set; }

        public ArcLengthSpline(Transform parent, int stepsPerSegment)
        {
            // control points
            var basePts = new List<Vector3>(parent.childCount);
            for (int i = 0; i < parent.childCount; i++)
                basePts.Add(parent.GetChild(i).position);
            if (basePts.Count < 2) { TotalLength = 0f; return; }

            Vector3 pre = basePts[0] + (basePts[0] - basePts[1]);
            Vector3 post = basePts[^1] + (basePts[^1] - basePts[^2]);
            ctrl.Add(pre);
            ctrl.AddRange(basePts);
            ctrl.Add(post);

            // sampling uniformly in t, then build arc-length table
            samples.Clear();
            cumLen.Clear();
            float acc = 0f;
            samples.Add(CR(ctrl[0], ctrl[1], ctrl[2], ctrl[3], 0f));
            cumLen.Add(0f);

            int segs = ctrl.Count - 3;
            for (int s = 0; s < segs; s++)
            {
                Vector3 prev = CR(ctrl[s], ctrl[s + 1], ctrl[s + 2], ctrl[s + 3], 0f);
                int steps = Mathf.Max(2, stepsPerSegment);
                for (int j = 1; j <= steps; j++)
                {
                    float t = j / (float)steps;
                    Vector3 p = CR(ctrl[s], ctrl[s + 1], ctrl[s + 2], ctrl[s + 3], t);
                    acc += Vector3.Distance(prev, p);
                    samples.Add(p);
                    cumLen.Add(acc);
                    prev = p;
                }
            }
            TotalLength = Mathf.Max(acc, 1e-4f);
        }

        public Vector3 PointAtArc(float s)
        {
            if (samples.Count == 0) return Vector3.zero;
            s = Mathf.Clamp(s, 0f, TotalLength);
            int idx = FindIndexByArc(s);
            if (idx >= samples.Count - 1) return samples[^1];

            float s0 = cumLen[idx];
            float s1 = cumLen[idx + 1];
            float u = (s1 - s0) > 1e-6f ? (s - s0) / (s1 - s0) : 0f;
            return Vector3.Lerp(samples[idx], samples[idx + 1], u);
        }

        public Vector3 TangentAtArc(float s)
        {
            if (samples.Count < 2) return Vector3.forward;
            s = Mathf.Clamp(s, 0f, TotalLength);
            int idx = Mathf.Min(FindIndexByArc(s), samples.Count - 2);
            Vector3 dir = samples[idx + 1] - samples[idx];
            return dir.sqrMagnitude > 1e-8f ? dir.normalized : Vector3.forward;
        }

        private int FindIndexByArc(float s)
        {
            int lo = 0, hi = cumLen.Count - 1;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (cumLen[mid] < s) lo = mid + 1; else hi = mid;
            }
            return Mathf.Max(0, lo - 1);
        }

        private static Vector3 CR(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * ((2f * p1) +
                           (-p0 + p2) * t +
                           (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                           (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }
    }
}
