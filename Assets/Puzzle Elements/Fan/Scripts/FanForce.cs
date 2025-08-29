using UnityEngine;
using System.Collections.Generic;

public class FanForce : MonoBehaviour
{
    [Header("Volumen del aire (frontal)")]
    [Tooltip("Longitud del empuje a lo largo de forward (m).")]
    public float length = 10f;

    [Tooltip("Radio en la cara del ventilador (z=0).")]
    public float startRadius = 1.0f;

    [Tooltip("Radio al final (z=length). Igual al inicial => cilindro.")]
    public float endRadius = 1.0f;

    [Header("Succión trasera (add-on)")]
    [Tooltip("Activar succión desde atrás del ventilador (sin cambiar la lógica frontal).")]
    public bool enableBackSuction = false;

    [Tooltip("Longitud de la succión hacia atrás (m).")]
    public float backLength = 6f;

    [Tooltip("Radio trasero en la cara del ventilador (z=0 detrás).")]
    public float backStartRadius = 1.0f;

    [Tooltip("Radio al final de la succión (z=backLength).")]
    public float backEndRadius = 1.0f;

    [Header("Capas afectadas")]
    [Tooltip("Capas que pueden ser empujadas/atraídas.")]
    public LayerMask affectLayers = ~0;

    [Header("Fuerza (frontal)")]
    [Tooltip("Aceleración máxima en el eje forward (m/s²) antes de atenuaciones.")]
    public float maxAcceleration = 30f;

    [Tooltip("Atenuación a lo largo del eje (input = z/length).")]
    public AnimationCurve longitudinalFalloff = AnimationCurve.Linear(0f, 1f, 1f, 0f);

    [Tooltip("Atenuación radial por corte (input = r/radio(z)).")]
    public AnimationCurve radialFalloff = AnimationCurve.Linear(0f, 1f, 1f, 0f);

    [Header("Fuerza (trasera)")]
    [Tooltip("Aceleración máxima de succión (m/s²) antes de atenuaciones.")]
    public float backMaxAcceleration = 20f;

    [Tooltip("Atenuación longitudinal trasera (input = z/backLength).")]
    public AnimationCurve backLongitudinalFalloff = AnimationCurve.Linear(0f, 1f, 1f, 0f);

    [Tooltip("Atenuación radial para succión (input = r/radio(z)).")]
    public AnimationCurve backRadialFalloff = AnimationCurve.Linear(0f, 1f, 1f, 0f);

    [Header("Objetos apoyados")]
    [Tooltip("Porción de la aceleración como 'lift' vertical para vencer fricción estática.")]
    [Range(0f, 0.5f)] public float liftFraction = 0.15f;

    [Tooltip("µ aproximada del suelo; se usa µ*g como umbral para arrancar objetos en reposo.")]
    public float approxMuStatic = 0.5f;

    [Header("CharacterController (sin componentes extra)")]
    [Tooltip("Aplicar empuje/succión a CharacterController con Move().")]
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

    // Velocidades acumuladas por CC (sin agregar componentes al player)
    private readonly Dictionary<CharacterController, Vector3> _ccExternalVel = new();

    private void FixedUpdate()
    {
        // Llamadas a la lógica de empuje y succión
        ForwardForce();
        if (enableBackSuction) BackwardForce();
    }

    // ======================== EMPUJE HACIA ADELANTE ========================
    private void ForwardForce()
    {
        if (length <= 0f) return;

        Vector3 origin = transform.position;
        Vector3 axis = transform.forward; // eje positivo hacia adelante

        float capRadius = Mathf.Max(startRadius, endRadius);
        Vector3 a = origin;
        Vector3 b = origin + axis * length;

        var hits = Physics.OverlapCapsule(a, b, capRadius, affectLayers, QueryTriggerInteraction.Ignore);

        foreach (var col in hits)
        {
            // 1) CharacterController (player) sin componente extra
            if (pushCharacterControllers)
            {
                var cc = col.GetComponentInParent<CharacterController>();
                if (cc != null)
                {
                    Vector3 accelCC;
                    if (ComputeAccelAtPoint(cc.bounds.center, origin, axis, out accelCC))
                    {
                        float dt = Time.fixedDeltaTime;
                        Vector3 lift = Vector3.up * (accelCC.magnitude * liftFraction);
                        if (!_ccExternalVel.TryGetValue(cc, out var vel)) vel = Vector3.zero;

                        vel += (accelCC + lift) * dt;
                        vel = Vector3.ClampMagnitude(vel, ccMaxExternalSpeed);

                        cc.Move(vel * dt);

                        vel = Vector3.Lerp(vel, Vector3.zero, ccDamping);
                        _ccExternalVel[cc] = vel;
                    }
                    continue;
                }
            }

            // 2) Rigidbody dinámico
            var rb = col.attachedRigidbody;
            if (!rb || rb.isKinematic) continue;

            Vector3 accelRB;
            if (!ComputeAccelAtPoint(rb.worldCenterOfMass, origin, axis, out accelRB)) continue;

            float g = Physics.gravity.magnitude;
            float minAccel = approxMuStatic * g;
            float mag = accelRB.magnitude;
            if (mag < minAccel && mag > 1e-4f)
                accelRB = accelRB.normalized * Mathf.Min(minAccel, maxAcceleration);

            if (liftFraction > 0f)
                rb.AddForce(Vector3.up * (accelRB.magnitude * liftFraction), ForceMode.Acceleration);

            rb.AddForce(accelRB, ForceMode.Acceleration);
        }
    }

    // ======================== SUCCIÓN DESDE ATRÁS ========================
    private void BackwardForce()
    {
        if (backLength <= 0f) return;

        Vector3 origin = transform.position;
        Vector3 backAxis = -transform.forward; // medimos hacia atrás
        Vector3 pullDir = transform.forward;   // succión acelera hacia el ventilador (+forward)

        float capRadius = Mathf.Max(backStartRadius, backEndRadius);
        Vector3 a = origin;
        Vector3 b = origin + backAxis * backLength;

        var hits = Physics.OverlapCapsule(a, b, capRadius, affectLayers, QueryTriggerInteraction.Ignore);

        foreach (var col in hits)
        {
            // 1) CharacterController
            if (pushCharacterControllers)
            {
                var cc = col.GetComponentInParent<CharacterController>();
                if (cc != null)
                {
                    if (ComputeBackAccelAtPoint(cc.bounds.center, origin, backAxis, out Vector3 accelCC))
                    {
                        accelCC = pullDir * accelCC.magnitude; // dirección hacia el ventilador

                        float dt = Time.fixedDeltaTime;
                        Vector3 lift = Vector3.up * (accelCC.magnitude * liftFraction);
                        if (!_ccExternalVel.TryGetValue(cc, out var vel)) vel = Vector3.zero;

                        vel += (accelCC + lift) * dt;
                        vel = Vector3.ClampMagnitude(vel, ccMaxExternalSpeed);

                        cc.Move(vel * dt);

                        vel = Vector3.Lerp(vel, Vector3.zero, ccDamping);
                        _ccExternalVel[cc] = vel;
                    }
                    continue;
                }
            }

            // 2) Rigidbody dinámico
            var rb = col.attachedRigidbody;
            if (!rb || rb.isKinematic) continue;

            if (ComputeBackAccelAtPoint(rb.worldCenterOfMass, origin, backAxis, out Vector3 accelRB))
            {
                accelRB = pullDir * accelRB.magnitude; // dirección hacia el ventilador

                float g = Physics.gravity.magnitude;
                float minAccel = approxMuStatic * g;
                float mag = accelRB.magnitude;
                if (mag < minAccel && mag > 1e-4f)
                    accelRB = accelRB.normalized * Mathf.Min(minAccel, backMaxAcceleration);

                if (liftFraction > 0f)
                    rb.AddForce(Vector3.up * (accelRB.magnitude * liftFraction), ForceMode.Acceleration);

                rb.AddForce(accelRB, ForceMode.Acceleration);
            }
        }
    }

    // ======================== CALCULA LA INTENSIDAD (frontal) ========================
    private bool ComputeAccelAtPoint(Vector3 point, Vector3 origin, Vector3 forward, out Vector3 accel)
    {
        Vector3 to = point - origin;

        float z = Vector3.Dot(to, forward);             // avance a lo largo del eje
        if (z < 0f || z > length) { accel = default; return false; }

        Vector3 radial = to - forward * z;
        float r = radial.magnitude;

        float sectionRadius = Mathf.Lerp(Mathf.Max(0f, startRadius), Mathf.Max(0f, endRadius), length > 0f ? z / length : 1f);
        if (r > sectionRadius + 1e-4f) { accel = default; return false; }

        float longT = Mathf.Clamp01(length > 0f ? z / length : 1f);
        float radT = Mathf.Clamp01(sectionRadius > 0f ? r / sectionRadius : 0f);
        float intensity = Mathf.Clamp01(longitudinalFalloff.Evaluate(longT)) * Mathf.Clamp01(radialFalloff.Evaluate(radT));
        if (intensity <= 0f) { accel = default; return false; }

        accel = forward * (maxAcceleration * intensity);
        return true;
    }

    // ======================== CALCULA LA INTENSIDAD (trasera) ========================
    private bool ComputeBackAccelAtPoint(Vector3 point, Vector3 origin, Vector3 backAxis, out Vector3 accel)
    {
        Vector3 to = point - origin;

        float z = Vector3.Dot(to, backAxis);            // avance a lo largo del eje TRASERO
        if (z < 0f || z > backLength) { accel = default; return false; }

        Vector3 radial = to - backAxis * z;
        float r = radial.magnitude;

        float sectionRadius = Mathf.Lerp(Mathf.Max(0f, backStartRadius), Mathf.Max(0f, backEndRadius), backLength > 0f ? z / backLength : 1f);
        if (r > sectionRadius + 1e-4f) { accel = default; return false; }

        float longT = Mathf.Clamp01(backLength > 0f ? z / backLength : 1f);
        float radT = Mathf.Clamp01(sectionRadius > 0f ? r / sectionRadius : 0f);
        float intensity = Mathf.Clamp01(backLongitudinalFalloff.Evaluate(longT)) * Mathf.Clamp01(backRadialFalloff.Evaluate(radT));
        if (intensity <= 0f) { accel = default; return false; }

        accel = backAxis * (backMaxAcceleration * intensity);
        return true;
    }

    // ======================== GIZMOS (frontal y trasero) ========================
    [Header("Gizmos")]
    public bool drawGizmos = true;
    public int gizmoRings = 6;
    public int gizmoRingSegments = 32;
    public Color gizmoColorFront = new(0f, 0.8f, 1f, 0.7f);
    public Color gizmoColorBack = new(1f, 0.6f, 0f, 0.7f);

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos || gizmoRings < 1 || gizmoRingSegments < 3) return;

        Vector3 origin = transform.position;
        Vector3 fwd = transform.forward;
        Vector3 right = transform.right;
        Vector3 up = transform.up;

        // Frontal
        if (length > 0f)
        {
            Gizmos.color = gizmoColorFront;
            DrawFrustum(origin, fwd, right, up, length, startRadius, endRadius);
        }

        // Trasero (succión)
        if (enableBackSuction && backLength > 0f)
        {
            Gizmos.color = gizmoColorBack;
            DrawFrustum(origin, -fwd, right, up, backLength, backStartRadius, backEndRadius);
        }
    }

    private void DrawFrustum(Vector3 origin, Vector3 axis, Vector3 right, Vector3 up, float segLen, float r0, float r1)
    {
        Vector3 prevCenter = origin;
        float prevRadius = Mathf.Max(0f, r0);
        DrawWireDisc(prevCenter, right, up, prevRadius, gizmoRingSegments);

        for (int i = 1; i <= gizmoRings; i++)
        {
            float t = i / (float)gizmoRings;
            float z = t * segLen;
            float radius = Mathf.Lerp(Mathf.Max(0f, r0), Mathf.Max(0f, r1), t);
            Vector3 center = origin + axis * z;

            DrawWireDisc(center, right, up, radius, gizmoRingSegments);
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
