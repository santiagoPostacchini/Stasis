using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Puzzle_Elements.LaunchPlate.Scripts
{
    [RequireComponent(typeof(Collider))]
    public class LaunchPlate : MonoBehaviour
    {
        [Header("Trajectories (use children as control points)")]
        [SerializeField] private Transform playerTrajectoryParent;
        [SerializeField] private Transform objectTrajectoryParent;

        [Header("Motion")]
        [Tooltip("Muestras por segmento para construir la tabla de arco (suavidad).")]
        [SerializeField, Range(8, 128)] private int samplesPerSegment = 32;
        [Tooltip("Velocidad extra opcional en la dirección del impulso inicial.")]
        [SerializeField] private float exitSpeedBoost;

        [Header("Lanzamiento (Portal-like)")]
        [Tooltip("Velocidad inicial mínima deseada (m/s). El sistema buscará un T que cumpla v0 >= este valor.")]
        [SerializeField] private float minInitialSpeed = 16f;
        [Tooltip("Búsqueda de T: mínimo y máximo en segundos.")]
        [SerializeField] private float tmin = 0.06f;
        [SerializeField] private float tmax = 6f;
        [Tooltip("Iteraciones de búsqueda numérica para T.")]
        [SerializeField, Range(8, 80)] private int searchIterations = 40;
        [Tooltip("Prefiere la rama lenta (T mayor) cuando hay dos soluciones con la misma velocidad.")]
        [SerializeField] private bool preferSlowerBranch = true;

        [Header("Correcciones suaves (nudges)")]
        [Tooltip("Cuántos 'nodos' temporales para pequeñas correcciones.")]
        [SerializeField, Range(0, 16)] private int nudgeNodes = 6;
        [Tooltip("Ganancia de posición del nudge (baja).")]
        [SerializeField] private float nudgeKp = 2.0f;
        [Tooltip("Ganancia de velocidad del nudge (baja).")]
        [SerializeField] private float nudgeKd = 0.5f;
        [Tooltip("Fuerza máxima por nudge (N).")]
        [SerializeField] private float maxNudgeForce = 200f;
        [Tooltip("Ventana temporal alrededor del nodo en la que se aplica el nudge.")]
        [SerializeField] private float nudgeTimeWindow = 0.12f;

        [Header("Cooldown")]
        [SerializeField] private float cooldown = 0.35f;
        [Tooltip("Evita relanzar mientras hay un lanzamiento activo.")]
        [SerializeField] private bool singleFireWhileActive = true;

        [Header("Gizmos")]
        [SerializeField] private bool drawGizmos = true;
        [SerializeField] private bool alwaysDraw; // si false, dibuja sólo cuando está seleccionado
        [SerializeField, Range(8, 128)] private int gizmoTrajectoryDetail = 48;

        private bool _canLaunch = true;

        // ====== Cache para gizmos y depuración ======
        private Vector3 _lastStart;
        private Vector3 _lastEnd;
        private Vector3 _lastV0;
        private float _lastT;
        private List<Vector3> _lastSplineSamplesPlayer;
        private List<Vector3> _lastSplineSamplesObject;
        private List<Vector3> _lastBallisticPath; // muestreo de la parábola prevista
        private List<Vector3> _lastNudgeMarks;    // posiciones previstas de nodos (sobre la spline activa)

        private void Awake()
        {
            var col = GetComponent<Collider>();
            col.isTrigger = true;
        }

        private void OnValidate()
        {
            samplesPerSegment = Mathf.Clamp(samplesPerSegment, 8, 128);
            nudgeNodes = Mathf.Clamp(nudgeNodes, 0, 16);
            nudgeKp = Mathf.Max(0f, nudgeKp);
            nudgeKd = Mathf.Max(0f, nudgeKd);
            maxNudgeForce = Mathf.Max(0f, maxNudgeForce);
            nudgeTimeWindow = Mathf.Max(0.01f, nudgeTimeWindow);
            tmin = Mathf.Max(0.02f, tmin);
            tmax = Mathf.Max(tmin + 0.1f, tmax);
            minInitialSpeed = Mathf.Max(0f, minInitialSpeed);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_canLaunch && singleFireWhileActive) return;

            var rb = other.attachedRigidbody ?? other.GetComponentInParent<Rigidbody>();
            if (!rb) return;

            bool isPlayer = other.CompareTag("Player") || other.transform.root.CompareTag("Player");
            Transform parent = (isPlayer && playerTrajectoryParent) ? playerTrajectoryParent : objectTrajectoryParent;
            if (!parent || parent.childCount < 2) return;

            var spline = new ArcLengthSpline(parent, samplesPerSegment);
            if (spline.TotalLength <= 1e-4f) return;

            // Calcular plan de vuelo usando velocidad mínima
            var plan = BuildFlightPlan_MinInitialSpeed(rb.position, spline);
            if (plan.T <= 0f) return;

            // Guardar cache para gizmos
            CacheForGizmos(rb.position, plan, spline, isPlayer);

            StartCoroutine(LaunchBallisticWithNudges(rb, spline, plan));
        }

        // ========= Plan de vuelo con velocidad inicial mínima =========
        private FlightPlan BuildFlightPlan_MinInitialSpeed(Vector3 start, ArcLengthSpline spline)
        {
            Vector3 end = spline.PointAtArc(spline.TotalLength);
            Vector3 g = Physics.gravity;

            // Funciones locales: v0(T) y |v0(T)|
            Vector3 V0OfT(float T)
            {
                T = Mathf.Max(T, 1e-4f);
                return (end - start - 0.5f * g * (T * T)) / T;
            }
            float SpeedOfT(float T) => V0OfT(T).magnitude;

            // 1) Encontrar T* que minimiza |v0| (búsqueda ternaria)
            float tA = tmin, tB = tmax;
            for (int i = 0; i < searchIterations; i++)
            {
                float l = Mathf.Lerp(tA, tB, 1f / 3f);
                float r = Mathf.Lerp(tA, tB, 2f / 3f);
                if (SpeedOfT(l) < SpeedOfT(r)) tB = r; else tA = l;
            }
            float minSpeed = 0.5f * (tA + tB);
            float vmin = SpeedOfT(minSpeed);

            float targetSpeed = Mathf.Max(minInitialSpeed, vmin);

            // 2) Si targetSpeed==vmin, usamos T_minSpeed. Si no, buscamos T con |v0(T)|=targetSpeed.
            float chosenT = minSpeed;

            if (targetSpeed > vmin + 1e-3f)
            {
                // |v0(T)| tiene forma de U: decrece hasta T_minSpeed y luego crece.
                // Para |v0| = targetSpeed hay dos soluciones; elegimos rama según preferencia.
                // Buscamos cada rama por bisección.

                bool FindRoot(float a, float b, out float root)
                {
                    float fa = SpeedOfT(a) - targetSpeed;
                    float fb = SpeedOfT(b) - targetSpeed;
                    // Necesitamos cambio de signo
                    if (fa * fb > 0f) { root = 0f; return false; }
                    for (int i = 0; i < searchIterations; i++)
                    {
                        float m = 0.5f * (a + b);
                        float fm = SpeedOfT(m) - targetSpeed;
                        if (fa * fm <= 0f) { b = m;
                        }
                        else { a = m; fa = fm; }
                    }
                    root = 0.5f * (a + b);
                    return true;
                }

                bool hasLeft = FindRoot(tmin, minSpeed, out var leftRoot);
                bool hasRight = FindRoot(minSpeed, tmax, out var rightRoot);

                if (preferSlowerBranch && hasRight) chosenT = rightRoot;
                else if (!preferSlowerBranch && hasLeft) chosenT = leftRoot;
                else if (hasRight) chosenT = rightRoot;
                else if (hasLeft) chosenT = leftRoot;
                else chosenT = minSpeed; // fallback (no debería pasar si los rangos están bien)
            }

            Vector3 v0 = V0OfT(chosenT);
            if (exitSpeedBoost > 0f)
                v0 += v0.normalized * exitSpeedBoost;

            return new FlightPlan { V0 = v0, T = chosenT };
        }

        private IEnumerator LaunchBallisticWithNudges(Rigidbody rb, ArcLengthSpline spline, FlightPlan plan)
        {
            _canLaunch = false;

            var prevInterp = rb.interpolation;
            var prevCcd = rb.collisionDetectionMode;

            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // 1) Impulso inicial limpio (no teletransporta)
            rb.AddForce(plan.V0 - rb.velocity, ForceMode.VelocityChange);

            // 2) Programar nudges pequeñitos (opcional)
            var marks = BuildNudgeSchedule(spline, plan.T, nudgeNodes);

            float elapsed = 0f;
            while (elapsed < plan.T)
            {
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;

                if (marks.Count == 0) continue;

                for (int i = 0; i < marks.Count; i++)
                {
                    var m = marks[i];
                    if (m.Done) continue;

                    if (Mathf.Abs(elapsed - m.T) <= nudgeTimeWindow)
                    {
                        float s = Mathf.Clamp01(m.Alpha) * spline.TotalLength;
                        Vector3 pDesired = spline.PointAtArc(s);

                        // PD MUY SUAVE (solo para ayudar, no para forzar trayecto)
                        Vector3 posErr = pDesired - rb.position;
                        Vector3 velErr = m.VDesired - rb.velocity;

                        Vector3 force = (nudgeKp * posErr + nudgeKd * velErr) * rb.mass;
                        rb.AddForce(Vector3.ClampMagnitude(force, maxNudgeForce), ForceMode.Force);

                        m.Done = true;
                        marks[i] = m;
                    }
                }
            }

            // 3) Nudge final suave para cerrar cerca del objetivo
            {
                Vector3 pF = spline.PointAtArc(spline.TotalLength);
                Vector3 posErr = pF - rb.position;
                Vector3 velErr = Vector3.zero - rb.velocity;
                Vector3 force = (nudgeKp * posErr + nudgeKd * velErr) * rb.mass;
                rb.AddForce(Vector3.ClampMagnitude(force, maxNudgeForce), ForceMode.Force);
            }

            rb.interpolation = prevInterp;
            rb.collisionDetectionMode = prevCcd;

            yield return new WaitForSeconds(cooldown);
            _canLaunch = true;
        }

        private struct FlightPlan
        {
            public Vector3 V0;
            public float T;
        }

        private struct NudgeMark
        {
            public float T;       // tiempo objetivo del nudge
            public float Alpha;   // 0..1 para mapear a arco
            public Vector3 VDesired;
            public bool Done;
        }

        private List<NudgeMark> BuildNudgeSchedule(ArcLengthSpline spline, float T, int count)
        {
            var list = new List<NudgeMark>(count);
            if (count <= 0) return list;

            for (int i = 1; i <= count; i++)
            {
                float alpha = i / (float)(count + 1); // evita extremos
                float t = alpha * T;

                Vector3 tan = spline.TangentAtArc(alpha * spline.TotalLength).normalized;
                Vector3 vDes = tan * (spline.TotalLength / Mathf.Max(0.0001f, T));

                list.Add(new NudgeMark { T = t, Alpha = alpha, VDesired = vDes, Done = false });
            }
            return list;
        }

        // ======= GIZMOS =======
        private void CacheForGizmos(Vector3 start, FlightPlan plan, ArcLengthSpline spline, bool isPlayer)
        {
            _lastStart = start;
            _lastEnd = spline.PointAtArc(spline.TotalLength);
            _lastV0 = plan.V0;
            _lastT = plan.T;

            // Cache spline samples
            var samples = SampleSplineWorld(spline, gizmoTrajectoryDetail);
            if (isPlayer)
            {
                _lastSplineSamplesPlayer = samples;
            }
            else
            {
                _lastSplineSamplesObject = samples;
            }

            // Cache ballistic path predicted
            _lastBallisticPath = SampleBallistic(start, plan.V0, plan.T, gizmoTrajectoryDetail);

            // Cache nudge marks (posiciones esperadas)
            _lastNudgeMarks = new List<Vector3>();
            if (nudgeNodes > 0)
            {
                for (int i = 1; i <= nudgeNodes; i++)
                {
                    float alpha = i / (float)(nudgeNodes + 1);
                    _lastNudgeMarks.Add(spline.PointAtArc(alpha * spline.TotalLength));
                }
            }
        }

        private List<Vector3> SampleSplineWorld(ArcLengthSpline spline, int detail)
        {
            var pts = new List<Vector3>(detail + 1);
            for (int i = 0; i <= detail; i++)
            {
                float s = spline.TotalLength * (i / (float)detail);
                pts.Add(spline.PointAtArc(s));
            }
            return pts;
        }

        private List<Vector3> SampleBallistic(Vector3 start, Vector3 v0, float T, int detail)
        {
            var pts = new List<Vector3>(detail + 1);
            Vector3 g = Physics.gravity;
            for (int i = 0; i <= detail; i++)
            {
                float t = Mathf.Lerp(0f, T, i / (float)detail);
                Vector3 p = start + v0 * t + 0.5f * g * t * t;
                pts.Add(p);
            }
            return pts;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!drawGizmos) return;
            if (!alwaysDraw && !UnityEditor.Selection.Contains(gameObject)) return;

            // Dibujar hijos (control points) y splines
            DrawParentSpline(playerTrajectoryParent, new Color(0.25f, 0.9f, 0.35f), ref _lastSplineSamplesPlayer);
            DrawParentSpline(objectTrajectoryParent, new Color(0.2f, 0.55f, 1.0f), ref _lastSplineSamplesObject);

            // Dibujar trayectoria balística prevista (si hay plan cacheado)
            if (_lastBallisticPath is { Count: > 1 })
            {
                Gizmos.color = new Color(1.0f, 0.85f, 0.2f);
                for (int i = 0; i < _lastBallisticPath.Count - 1; i++)
                    Gizmos.DrawLine(_lastBallisticPath[i], _lastBallisticPath[i + 1]);

                // Inicio/fin
                Gizmos.color = new Color(1.0f, 0.5f, 0.1f);
                Gizmos.DrawSphere(_lastStart, 0.06f);
                Gizmos.color = new Color(1.0f, 0.3f, 0.0f);
                Gizmos.DrawSphere(_lastEnd, 0.06f);

                // Vector v0
                Gizmos.color = new Color(1.0f, 0.7f, 0.1f);
                Gizmos.DrawLine(_lastStart, _lastStart + _lastV0 * 0.1f);
            }

            // Marcas de nudge
            if (_lastNudgeMarks != null)
            {
                Gizmos.color = Color.yellow;
                foreach (var p in _lastNudgeMarks)
                    Gizmos.DrawSphere(p, 0.05f);
            }
        }

        private void DrawParentSpline(Transform parent, Color c, ref List<Vector3> cache)
        {
            if (!parent || parent.childCount < 2) return;

            // Puntos de control
            Gizmos.color = new Color(c.r, c.g, c.b, 0.9f);
            for (int i = 0; i < parent.childCount; i++)
            {
                var p = parent.GetChild(i).position;
                Gizmos.DrawSphere(p, 0.05f);
                if (i > 0)
                {
                    var p0 = parent.GetChild(i - 1).position;
                    Gizmos.DrawLine(p0, p);
                }
            }

            // Spline muestreada (si no hay cache, rehacer rápido)
            if (cache == null || cache.Count == 0)
            {
                var spline = new ArcLengthSpline(parent, samplesPerSegment);
                cache = SampleSplineWorld(spline, gizmoTrajectoryDetail);
            }

            // Trazar la curva final
            Gizmos.color = c;
            for (int i = 0; i < cache.Count - 1; i++)
                Gizmos.DrawLine(cache[i], cache[i + 1]);
        }
#endif

        // ========= Spline con tabla de arco =========
        private class ArcLengthSpline
        {
            private readonly List<Vector3> _ctrl = new();
            private readonly List<Vector3> _samples = new();
            private readonly List<float> _cumLen = new();
            public float TotalLength { get; private set; }

            public ArcLengthSpline(Transform parent, int stepsPerSegment)
            {
                var basePts = new List<Vector3>(parent.childCount);
                for (int i = 0; i < parent.childCount; i++) basePts.Add(parent.GetChild(i).position);
                if (basePts.Count < 2) { TotalLength = 0f; return; }

                Vector3 pre = basePts[0] + (basePts[0] - basePts[1]);
                Vector3 post = basePts[^1] + (basePts[^1] - basePts[^2]);
                _ctrl.Add(pre); _ctrl.AddRange(basePts); _ctrl.Add(post);

                _samples.Clear(); _cumLen.Clear();
                float acc = 0f;
                _samples.Add(CR(_ctrl[0], _ctrl[1], _ctrl[2], _ctrl[3], 0f));
                _cumLen.Add(0f);

                int segs = _ctrl.Count - 3;
                for (int s = 0; s < segs; s++)
                {
                    Vector3 prev = CR(_ctrl[s], _ctrl[s + 1], _ctrl[s + 2], _ctrl[s + 3], 0f);
                    int steps = Mathf.Max(2, stepsPerSegment);
                    for (int j = 1; j <= steps; j++)
                    {
                        float t = j / (float)steps;
                        Vector3 p = CR(_ctrl[s], _ctrl[s + 1], _ctrl[s + 2], _ctrl[s + 3], t);
                        acc += Vector3.Distance(prev, p);
                        _samples.Add(p);
                        _cumLen.Add(acc);
                        prev = p;
                    }
                }
                TotalLength = Mathf.Max(acc, 1e-4f);
            }

            public Vector3 PointAtArc(float s)
            {
                if (_samples.Count == 0) return Vector3.zero;
                s = Mathf.Clamp(s, 0f, TotalLength);
                int idx = FindIndexByArc(s);
                if (idx >= _samples.Count - 1) return _samples[^1];

                float s0 = _cumLen[idx];
                float s1 = _cumLen[idx + 1];
                float u = (s1 - s0) > 1e-6f ? (s - s0) / (s1 - s0) : 0f;
                return Vector3.Lerp(_samples[idx], _samples[idx + 1], u);
            }

            public Vector3 TangentAtArc(float s)
            {
                if (_samples.Count < 2) return Vector3.forward;
                s = Mathf.Clamp(s, 0f, TotalLength);
                int idx = Mathf.Min(FindIndexByArc(s), _samples.Count - 2);
                Vector3 dir = _samples[idx + 1] - _samples[idx];
                return dir.sqrMagnitude > 1e-8f ? dir.normalized : Vector3.forward;
            }

            private int FindIndexByArc(float s)
            {
                int lo = 0, hi = _cumLen.Count - 1;
                while (lo < hi)
                {
                    int mid = (lo + hi) >> 1;
                    if (_cumLen[mid] < s) lo = mid + 1; else hi = mid;
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
}