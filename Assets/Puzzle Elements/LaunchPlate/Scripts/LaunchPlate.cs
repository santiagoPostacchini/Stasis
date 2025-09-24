using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Puzzle_Elements.LaunchPlate.Scripts
{
    [RequireComponent(typeof(Collider))]
    public class LaunchPlate : MonoBehaviour
    {
        // ====== (tus campos iguales) ======
        [Header("Trajectories (children are checkpoints)")] [SerializeField]
        private Transform playerTrajectoryParent;

        [SerializeField] private Transform objectTrajectoryParent;

        [Header("Launch")] [SerializeField] private bool useFixedLaunchOrigin = true;
        [SerializeField] private Transform launchOrigin;
        [SerializeField] private bool snapBodyToOrigin;

        [SerializeField] private float minInitialSpeed = 18f;

        [SerializeField, Range(5f, 45f)]
        private float maxElevationDeg = 25f; // opcional: si querés limitar elevación, podés clamplear v0 luego.

        [SerializeField] private float tMin = 0.15f, tMax = 2.0f;
        [SerializeField, Range(8, 64)] private int tSearchIterations = 32;
        [SerializeField] private bool preferSlowerBranch;

        [Header("Discrete Nudges (very light)")] [SerializeField, Range(0, 32)]
        private int overrideCheckpointCount = 0;

        [SerializeField] private float nudgeTimeWindow = 0.12f;
        [SerializeField] private float nudgeProximity = 1.25f;
        [SerializeField] private float nudgeKp = 1.8f, nudgeKd = 0.45f;
        [SerializeField] private float maxNudgeForce = 120f;
        [SerializeField] private bool lateralOnly = true;

        [Header("Cooldown")] [SerializeField] private float cooldown = 0.25f;
        [SerializeField] private bool singleFireWhileActive = true;

        [Header("Gizmos")] [SerializeField] private bool drawGizmos = true, alwaysDraw = false;
        [SerializeField, Range(8, 128)] private int gizmoDetail = 48;
        [SerializeField] private Color playerColor = new(0.25f, 0.9f, 0.35f);
        [SerializeField] private Color objectColor = new(0.2f, 0.55f, 1.0f);
        [SerializeField] private Color ballisticColor = new(1.0f, 0.85f, 0.2f);
        [SerializeField] private Color nudgeColor = new(1.0f, 0.8f, 0.2f);

        private bool _busy;

        void Awake()
        {
            var col = GetComponent<Collider>();
            col.isTrigger = true;
        }

        void OnValidate()
        {
            tMin = Mathf.Max(0.05f, tMin);
            tMax = Mathf.Max(tMin + 0.05f, tMax);
            nudgeTimeWindow = Mathf.Clamp(nudgeTimeWindow, 0.02f, 0.6f);
            nudgeProximity = Mathf.Max(0.05f, nudgeProximity);
            maxNudgeForce = Mathf.Max(1f, maxNudgeForce);
            minInitialSpeed = Mathf.Max(0f, minInitialSpeed);
        }

        void OnTriggerEnter(Collider other)
        {
            if (_busy && singleFireWhileActive) return;

            var rb = other.attachedRigidbody ?? other.GetComponentInParent<Rigidbody>();
            if (!rb) return;

            bool isPlayer = other.CompareTag("Player") || other.transform.root.CompareTag("Player");
            Transform parent = (isPlayer && playerTrajectoryParent) ? playerTrajectoryParent : objectTrajectoryParent;
            if (!parent || parent.childCount < 2) return;

            var checkpoints = BuildCheckpoints(parent, overrideCheckpointCount);
            Vector3 end = checkpoints[^1];

            // >>>>> START ROBUSTO:
            // Si snappeás: usá origen fijo. Si NO snappeás: siempre planificá desde la posición actual para que el plan coincida con la realidad.
            Vector3 start = (snapBodyToOrigin && useFixedLaunchOrigin)
                ? ResolveOrigin(parent)
                : (!snapBodyToOrigin ? rb.position : (useFixedLaunchOrigin ? ResolveOrigin(parent) : rb.position));

            if (snapBodyToOrigin && useFixedLaunchOrigin)
            {
                rb.position = start;
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            // >>>>> PLAN: Elegir T que mejor “ajuste” TODOS los hijos (y caiga exacto en end)
            var plan = ComputeBallisticPlanFitToCheckpoints(start, end, checkpoints, minInitialSpeed, tMin, tMax,
                tSearchIterations, preferSlowerBranch);
            if (plan.T <= 0f) return;

            // Impulso inicial invariante respecto a cómo llega
            rb.AddForce(plan.v0 - rb.velocity, ForceMode.VelocityChange);

            // t* por cercanía para cada checkpoint (mejora los pequeños empujes)
            var schedule = BuildNudgeSchedule(checkpoints, start, plan);

            StartCoroutine(FlyWithDiscreteNudges(rb, plan, schedule));

            //CacheForGizmos(start, end, plan, checkpoints, schedule, isPlayer);

            _busy = true;
            StartCoroutine(ClearBusyAfter(cooldown));
        }

        // ---------- Plan: T que minimiza error a checkpoints (end exacto) ----------
        struct BallisticPlan
        {
            public Vector3 v0;
            public float T;
        }

        BallisticPlan ComputeBallisticPlanFitToCheckpoints(
            Vector3 start, Vector3 end, List<Vector3> checkpoints,
            float minSpeed, float Tmin, float Tmax, int iters, bool preferSlow)
        {
            Vector3 g = Physics.gravity;

            // Alphas por longitud de arco (0=start, 1=end)
            int n = checkpoints.Count;
            var alphas = new float[n];
            float L = 0f;
            var cum = new float[n];
            cum[0] = 0f;
            for (int i = 1; i < n; i++)
            {
                L += Vector3.Distance(checkpoints[i - 1], checkpoints[i]);
                cum[i] = L;
            }

            for (int i = 0; i < n; i++) alphas[i] = L > 1e-4f ? (cum[i] / L) : (i / (float)(n - 1));

            Vector3 V0OfT(float T) => (end - start - 0.5f * g * (T * T)) / Mathf.Max(T, 1e-4f);
            float SpeedOfT(float T) => V0OfT(T).magnitude;

            // Función objetivo: SSE a TODOS los puntos excepto el final (que ya es exacto)
            float Cost(float T)
            {
                // penaliza si no alcanza velocidad mínima
                float sp = SpeedOfT(T);
                float penalty = (minSpeed > 0f && sp < minSpeed) ? Mathf.Pow((minSpeed - sp) * 20f, 2f) : 0f;

                Vector3 v0 = V0OfT(T);
                float sse = 0f;
                for (int i = 1; i < n - 1; i++) // sin el 0 (start) ni el último (end exacto)
                {
                    float ti = alphas[i] * T;
                    Vector3 pi = start + v0 * ti + 0.5f * g * ti * ti;
                    float di2 = (pi - checkpoints[i]).sqrMagnitude;
                    // pesos suaves: damos un poquito más a medianos
                    float w = 1f;
                    sse += w * di2;
                }

                return sse + penalty;
            }

            // Búsqueda 1D (golden/ternaria)
            float a = Tmin, b = Tmax;
            for (int i = 0; i < iters; i++)
            {
                float l = Mathf.Lerp(a, b, 1f / 3f);
                float r = Mathf.Lerp(a, b, 2f / 3f);
                if (Cost(l) < Cost(r)) b = r;
                else a = l;
            }

            float Tbest = 0.5f * (a + b);

            // Opcional: si hay dos ramas con misma speed, preferimos lenta/rápida.
            // (Acá ya minimizamos SSE; si querés, podés ajustar Tbest hacia la rama preferida si el coste es similar.)
            Vector3 v0best = V0OfT(Tbest);

            // Si querés clamplear elevación para “feel” jump-pad sin desarmar el ajuste, solo rota dirección
            // conservando magnitud (pequeño toque, opcional):
            if (maxElevationDeg > 0f)
            {
                float sp = v0best.magnitude;
                if (sp > 1e-4f)
                {
                    Vector3 dir = v0best.normalized;
                    float maxSin = Mathf.Sin(maxElevationDeg * Mathf.Deg2Rad);
                    float vy = Mathf.Clamp(dir.y, -maxSin, maxSin);
                    float vxz = Mathf.Sqrt(Mathf.Max(0f, 1f - vy * vy));
                    Vector3 horiz = Vector3.ProjectOnPlane(dir, Vector3.up).normalized;
                    if (horiz.sqrMagnitude < 1e-6f) horiz = Vector3.forward;
                    dir = horiz * vxz + Vector3.up * vy;
                    v0best = dir * sp;
                }
            }

            return new BallisticPlan { v0 = v0best, T = Tbest };
        }

        // ---------- Nudges (idénticos a tu versión mejorada) ----------
        struct Nudge
        {
            public float t;
            public Vector3 point;
            public Vector3 vDesired;
            public bool fired;
        }

        List<Nudge> BuildNudgeSchedule(List<Vector3> checkpoints, Vector3 start, BallisticPlan plan)
        {
            int n = checkpoints.Count;
            var list = new List<Nudge>(n - 1);
            float pathLen = EstimatePolylineLength(checkpoints);

            for (int i = 1; i < n; i++)
            {
                Vector3 P = checkpoints[i];
                float tStar = ClosestTimeToPoint(start, plan.v0, Physics.gravity, plan.T, P, 18);

                Vector3 next = (i < n - 1) ? checkpoints[i + 1] : checkpoints[^1];
                Vector3 vDesDir = (next - P);
                vDesDir = vDesDir.sqrMagnitude > 1e-6f ? vDesDir.normalized : Vector3.forward;
                float vMag = pathLen / Mathf.Max(0.05f, plan.T);

                list.Add(new Nudge { t = tStar, point = P, vDesired = vDesDir * vMag, fired = false });
            }

            return list;
        }

        static float ClosestTimeToPoint(Vector3 s, Vector3 v0, Vector3 g, float T, Vector3 P, int iters)
        {
            float a = 0f, b = T;

            float F(float t)
            {
                Vector3 x = s + v0 * t + 0.5f * g * t * t;
                return (x - P).sqrMagnitude;
            }

            for (int i = 0; i < iters; i++)
            {
                float l = Mathf.Lerp(a, b, 1f / 3f);
                float r = Mathf.Lerp(a, b, 2f / 3f);
                if (F(l) < F(r)) b = r;
                else a = l;
            }

            return 0.5f * (a + b);
        }

        IEnumerator FlyWithDiscreteNudges(Rigidbody rb, BallisticPlan plan, List<Nudge> schedule)
        {
            float elapsed = 0f;

            while (elapsed < plan.T)
            {
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;

                for (int i = 0; i < schedule.Count; i++)
                {
                    var n = schedule[i];
                    if (n.fired) continue;

                    float dt = elapsed - n.t;
                    float timeWeight = Mathf.Exp(-(dt * dt) / (2f * nudgeTimeWindow * nudgeTimeWindow));
                    if (timeWeight < 0.03f) continue;

                    float dist = Vector3.Distance(rb.position, n.point);
                    float spaceWeight = Mathf.Exp(-(dist * dist) / (2f * nudgeProximity * nudgeProximity));

                    Vector3 posErr = (n.point - rb.position);
                    Vector3 velErr = (n.vDesired - rb.velocity);
                    Vector3 force = (nudgeKp * posErr + nudgeKd * velErr) * rb.mass;

                    if (lateralOnly && rb.velocity.sqrMagnitude > 1e-6f)
                    {
                        Vector3 vN = rb.velocity.normalized;
                        force -= Vector3.Project(force, vN);
                    }

                    float w = Mathf.Clamp01(timeWeight * spaceWeight);
                    if (w > 0.001f)
                        rb.AddForce(Vector3.ClampMagnitude(force, maxNudgeForce) * w, ForceMode.Force);

                    if (elapsed > n.t + nudgeTimeWindow * 1.6f)
                    {
                        n.fired = true;
                        schedule[i] = n;
                    }
                }
            }
        }

        // ---------- Utilidades ----------
        static List<Vector3> BuildCheckpoints(Transform parent, int overrideCount)
        {
            var pts = new List<Vector3>(Mathf.Max(2, parent.childCount));
            for (int i = 0; i < parent.childCount; i++) pts.Add(parent.GetChild(i).position);
            if (overrideCount <= 0 || overrideCount >= pts.Count) return pts;

            float L = 0f;
            var cum = new List<float> { 0f };
            for (int i = 1; i < pts.Count; i++)
            {
                L += Vector3.Distance(pts[i - 1], pts[i]);
                cum.Add(L);
            }

            var outPts = new List<Vector3>(overrideCount);
            for (int k = 0; k < overrideCount; k++)
            {
                float s = (L * k) / (overrideCount - 1);
                int idx = 0;
                while (idx < cum.Count - 1 && cum[idx + 1] < s) idx++;
                float s0 = cum[idx], s1 = cum[Mathf.Min(idx + 1, cum.Count - 1)];
                float u = (s1 > s0) ? (s - s0) / (s1 - s0) : 0f;
                Vector3 p = Vector3.Lerp(pts[idx], pts[Mathf.Min(idx + 1, pts.Count - 1)], u);
                outPts.Add(p);
            }

            return outPts;
        }

        static float EstimatePolylineLength(List<Vector3> pts)
        {
            float L = 0f;
            for (int i = 1; i < pts.Count; i++) L += Vector3.Distance(pts[i - 1], pts[i]);
            return Mathf.Max(L, 0.01f);
        }

        Vector3 ResolveOrigin(Transform activeParent)
        {
            if (launchOrigin) return launchOrigin.position;
            if (activeParent && activeParent.childCount > 0) return activeParent.GetChild(0).position;
            return transform.position;
        }

        IEnumerator ClearBusyAfter(float t)
        {
            yield return new WaitForSeconds(t);
            _busy = false;
        }

        // ---------- Gizmos ----------
#if UNITY_EDITOR
        Vector3 _gStart, _gEnd;
        BallisticPlan _gPlan;
        List<Vector3> _gCheckpoints, _gBallistic;
        List<float> _gTimes;

        void CacheForGizmos(Vector3 start, Vector3 end, BallisticPlan plan, List<Vector3> checkpoints, List<Nudge> sched,
            bool isPlayer)
        {
            _gStart = start;
            _gEnd = end;
            _gPlan = plan;
            _gCheckpoints = checkpoints;
            _gTimes = new List<float>(sched.Count);
            foreach (var n in sched) _gTimes.Add(n.t);

            int N = Mathf.Max(8, gizmoDetail);
            _gBallistic = new List<Vector3>(N + 1);
            for (int i = 0; i <= N; i++)
            {
                float t = Mathf.Lerp(0f, plan.T, i / (float)N);
                Vector3 p = start + plan.v0 * t + 0.5f * Physics.gravity * t * t;
                _gBallistic.Add(p);
            }
        }

        void OnDrawGizmos()
        {
            if (!drawGizmos) return;
            if (!alwaysDraw && !UnityEditor.Selection.Contains(gameObject)) return;

            DrawParent(playerTrajectoryParent, playerColor);
            DrawParent(objectTrajectoryParent, objectColor);

            if (_gBallistic != null && _gBallistic.Count > 1)
            {
                Gizmos.color = ballisticColor;
                for (int i = 0; i < _gBallistic.Count - 1; i++)
                    Gizmos.DrawLine(_gBallistic[i], _gBallistic[i + 1]);

                Gizmos.color = new Color(1f, 0.5f, 0.1f);
                Gizmos.DrawSphere(_gStart, 0.05f);
                Gizmos.color = new Color(1f, 0.3f, 0f);
                Gizmos.DrawSphere(_gEnd, 0.05f);

                // checkpoints + punto de parabola en t*
                for (int i = 0; i < _gCheckpoints.Count; i++)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawSphere(_gCheckpoints[i], 0.045f);

                    if (_gTimes != null && i > 0 && i - 1 < _gTimes.Count)
                    {
                        float t = _gTimes[i - 1];
                        Vector3 p = _gStart + _gPlan.v0 * t + 0.5f * Physics.gravity * t * t;
                        Gizmos.color = nudgeColor;
                        Gizmos.DrawWireSphere(p, 0.05f);
                    }
                }
            }
        }

        void DrawParent(Transform parent, Color c)
        {
            if (!parent || parent.childCount < 2) return;

            Gizmos.color = new Color(c.r, c.g, c.b, 0.9f);
            for (int i = 0; i < parent.childCount; i++)
            {
                var p = parent.GetChild(i).position;
                Gizmos.DrawSphere(p, 0.045f);
                if (i > 0) Gizmos.DrawLine(parent.GetChild(i - 1).position, p);
            }
        }
#endif
    }
}