using UnityEngine;
using System.Collections.Generic;

public class FanForce : MonoBehaviour
{
    [Header("Volumen del aire")]
    [Tooltip("Longitud del empuje a lo largo de forward (m).")]
    public float length = 10f;

    [Tooltip("Radio en la cara del ventilador (z=0).")]
    public float startRadius = 1.0f;

    [Tooltip("Radio al final (z=length). Igual al inicial => cilindro.")]
    public float endRadius = 1.0f;

    [Tooltip("Capas que pueden ser empujadas.")]
    public LayerMask affectLayers = ~0;

    [Header("Fuerza")]
    [Tooltip("Aceleración máxima en el eje forward (m/s²) antes de atenuaciones.")]
    public float maxAcceleration = 30f;

    [Tooltip("Atenuación a lo largo del eje (input = z/length).")]
    public AnimationCurve longitudinalFalloff = AnimationCurve.Linear(0f, 1f, 1f, 0f);

    [Tooltip("Atenuación radial por corte (input = r/radio(z)).")]
    public AnimationCurve radialFalloff = AnimationCurve.Linear(0f, 1f, 1f, 0f);

    [Header("Objetos apoyados")]
    [Tooltip("Porción de la aceleración como 'lift' vertical para vencer fricción estática.")]
    [Range(0f, 0.5f)] public float liftFraction = 0.15f;

    [Tooltip("µ aproximada del suelo para arrancar objetos en reposo (usa µ*g como umbral).")]
    public float approxMuStatic = 0.5f;

    [Header("CharacterController (sin componentes extra)")]
    [Tooltip("Aplicar empuje a CharacterController directamente con Move().")]
    public bool pushCharacterControllers = true;

    [Tooltip("Velocidad externa máxima para CC (m/s).")]
    public float ccMaxExternalSpeed = 10f;

    [Tooltip("Amortiguación por frame fijo de la velocidad externa del CC (0..1).")]
    [Range(0f, 1f)] public float ccDamping = 0.15f;

    [Header("Línea de visión (opcional)")]
    public bool requireLineOfSight = false;
    public LayerMask occluderLayers = ~0;
    [Tooltip("Offset vertical del origen del rayo para evitar chocar contra el piso.")]
    public float losOriginYOffset = 0.1f;
    public float losProbeRadius = 0.2f;

    // Velocidades acumuladas por CC (no agregamos componentes al player)
    private readonly Dictionary<CharacterController, Vector3> _ccExternalVel = new();

    private void FixedUpdate()
    {
        if (length <= 0f) return;

        Vector3 origin = transform.position;
        Vector3 fwd = transform.forward;

        float capRadius = Mathf.Max(startRadius, endRadius);
        Vector3 a = origin;
        Vector3 b = origin + fwd * length;

        // Buscamos candidatos dentro del volumen
        var hits = Physics.OverlapCapsule(a, b, capRadius, affectLayers, QueryTriggerInteraction.Ignore);

        // Para no procesar el mismo CC varias veces por frame
        HashSet<CharacterController> processedCC = null;
        if (pushCharacterControllers) processedCC = new HashSet<CharacterController>();

        foreach (var col in hits)
        {
            // 1) CharacterController (player) sin componente extra
            if (pushCharacterControllers)
            {
                var cc = col.GetComponentInParent<CharacterController>();
                if (cc != null && (processedCC == null || processedCC.Add(cc)))
                {
                    Vector3 accelCC;
                    if (ComputeAccelAtPoint(cc.bounds.center, origin, fwd, out accelCC))
                    {
                        // Integrar como velocidad externa y mover
                        float dt = Time.fixedDeltaTime;
                        Vector3 lift = Vector3.up * (accelCC.magnitude * liftFraction);
                        if (!_ccExternalVel.TryGetValue(cc, out var vel)) vel = Vector3.zero;

                        vel += (accelCC + lift) * dt;
                        vel = Vector3.ClampMagnitude(vel, ccMaxExternalSpeed);

                        // Aplicar movimiento externo
                        cc.Move(vel * dt);

                        // Amortiguar para que se disipe
                        vel = Vector3.Lerp(vel, Vector3.zero, ccDamping);
                        _ccExternalVel[cc] = vel;
                    }
                    continue; // nada más para este collider
                }
            }

            // 2) Rigidbody dinámico
            var rb = col.attachedRigidbody;
            if (!rb || rb.isKinematic) continue;

            Vector3 accelRB;
            if (!ComputeAccelAtPoint(rb.worldCenterOfMass, origin, fwd, out accelRB))
                continue;

            // Empuje mínimo para arrancar contra fricción µ*g
            float g = Physics.gravity.magnitude;
            float minAccel = approxMuStatic * g;
            float mag = accelRB.magnitude;
            if (mag < minAccel && mag > 1e-4f)
                accelRB = accelRB.normalized * Mathf.Min(minAccel, maxAcceleration);

            // Lift vertical para reducir normal y fricción
            if (liftFraction > 0f)
                rb.AddForce(Vector3.up * (accelRB.magnitude * liftFraction), ForceMode.Acceleration);

            rb.AddForce(accelRB, ForceMode.Acceleration);
        }

        // Amortiguar CC que no estuvieron dentro este frame (para que se frenen)
        if (pushCharacterControllers && _ccExternalVel.Count > 0)
        {
            // Nota: no podemos iterar y modificar diccionario a la vez
            var keys = ListCache<CharacterController>.Get();
            keys.AddRange(_ccExternalVel.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                var cc = keys[i];
                if (processedCC == null || !processedCC.Contains(cc))
                {
                    var vel = _ccExternalVel[cc];
                    vel = Vector3.Lerp(vel, Vector3.zero, ccDamping);
                    if (vel.sqrMagnitude < 1e-6f) _ccExternalVel.Remove(cc);
                    else _ccExternalVel[cc] = vel;
                }
            }
            ListCache<CharacterController>.Release(keys);
        }
    }

    // Calcula la aceleración en un punto dentro del frustum/cilindro y respeta línea de visión si está activa
    private bool ComputeAccelAtPoint(Vector3 point, Vector3 origin, Vector3 forward, out Vector3 accel)
    {
        Vector3 to = point - origin;

        float z = Vector3.Dot(to, forward);             // avance a lo largo del eje
        if (z < 0f || z > length) { accel = default; return false; }

        Vector3 radial = to - forward * z;
        float r = radial.magnitude;

        float sectionRadius = Mathf.Lerp(Mathf.Max(0f, startRadius), Mathf.Max(0f, endRadius), length > 0f ? z / length : 1f);
        if (r > sectionRadius + 1e-4f) { accel = default; return false; }

        if (requireLineOfSight)
        {
            Vector3 start = origin + Vector3.up * losOriginYOffset;
            if (Physics.SphereCast(start, losProbeRadius, (point - start).normalized, out RaycastHit hit, Vector3.Distance(start, point), occluderLayers, QueryTriggerInteraction.Ignore))
            {
                // Algo tapa el flujo
                accel = default; return false;
            }
        }

        float longT = Mathf.Clamp01(length > 0f ? z / length : 1f);
        float radT = Mathf.Clamp01(sectionRadius > 0f ? r / sectionRadius : 0f);
        float intensity = Mathf.Clamp01(longitudinalFalloff.Evaluate(longT)) * Mathf.Clamp01(radialFalloff.Evaluate(radT));
        if (intensity <= 0f) { accel = default; return false; }

        accel = forward * (maxAcceleration * intensity);
        return true;
    }

    // ---------- Gizmos del volumen (cilindro/frustum que nace en TODO el radio inicial) ----------
    [Header("Gizmos")]
    public bool drawGizmos = true;
    public int gizmoRings = 6;
    public int gizmoRingSegments = 32;
    public Color gizmoColor = new(0f, 0.8f, 1f, 0.7f);

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos || length <= 0f || gizmoRings < 1 || gizmoRingSegments < 3) return;

        Vector3 origin = transform.position;
        Vector3 fwd = transform.forward;
        Vector3 right = transform.right;
        Vector3 up = transform.up;

        Gizmos.color = gizmoColor;

        Vector3 prevCenter = origin;
        float prevRadius = Mathf.Max(0f, startRadius);
        DrawWireDisc(prevCenter, right, up, prevRadius, gizmoRingSegments);

        for (int i = 1; i <= gizmoRings; i++)
        {
            float t = i / (float)gizmoRings;
            float z = t * length;
            float radius = Mathf.Lerp(Mathf.Max(0f, startRadius), Mathf.Max(0f, endRadius), t);
            Vector3 center = origin + fwd * z;

            DrawWireDisc(center, right, up, radius, gizmoRingSegments);

            // costillas cardinales
            Gizmos.DrawLine(prevCenter + right * prevRadius, center + right * radius);
            Gizmos.DrawLine(prevCenter - right * prevRadius, center - right * radius);
            Gizmos.DrawLine(prevCenter + up * prevRadius, center + up * radius);
            Gizmos.DrawLine(prevCenter - up * prevRadius, center - up * radius);

            prevCenter = center;
            prevRadius = radius;
        }
    }

    private void DrawWireDisc(Vector3 center, Vector3 axisX, Vector3 axisY, float radius, int segments)
    {
        if (radius <= 0f) return;
        float step = Mathf.PI * 2f / segments;
        Vector3 prev = center + axisX * radius;
        for (int i = 1; i <= segments; i++)
        {
            float a = i * step;
            Vector3 p = center + axisX * (Mathf.Cos(a) * radius) + axisY * (Mathf.Sin(a) * radius);
            Gizmos.DrawLine(prev, p);
            prev = p;
        }
    }

    // Pequeña utilidad para evitar allocs al iterar diccionarios
    private static class ListCache<T>
    {
        private static readonly Stack<List<T>> Pool = new();
        public static List<T> Get() => Pool.Count > 0 ? Pool.Pop() : new List<T>(8);
        public static void Release(List<T> list) { list.Clear(); Pool.Push(list); }
    }
}
