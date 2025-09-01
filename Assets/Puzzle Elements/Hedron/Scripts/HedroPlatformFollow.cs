using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class HedroPlatformFollower : MonoBehaviour
{
    [Header("Detección de plataforma")]
    public LayerMask platformMask;           // capa de plataforma
    public Transform probe;                  // punto desde donde chequeamos (ej: base del objeto)
    public float groundCheckDistance = 0.1f; // altura mínima para considerar que está sobre la plataforma

    [Header("Ajustes")]
    public bool applyPlatformRotation = true; // aplicar rotación de la plataforma

    private Rigidbody rb;
    private MovingPlatformDeltaPosition currentPlatform;
    private bool isOnPlatform;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (probe == null) probe = transform;

        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    private void FixedUpdate()
    {
        // 1) Detectar plataforma debajo
        RaycastHit hit;
        Vector3 origin = probe.position + Vector3.up * 0.05f;
        bool grounded = Physics.Raycast(origin, Vector3.down, out hit, groundCheckDistance + 0.05f, platformMask);

        MovingPlatformDeltaPosition newPlatform = grounded ? hit.collider.GetComponentInParent<MovingPlatformDeltaPosition>() : null;

        // 2) Si cambiamos de plataforma, actualizar estado
        if (newPlatform != currentPlatform)
        {
            currentPlatform = newPlatform;
            isOnPlatform = (currentPlatform != null);

            if (isOnPlatform)
            {
                // Resetear velocidades al tocar la plataforma por primera vez
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        // 3) Aplicar delta suavizado de la plataforma
        if (isOnPlatform && currentPlatform != null)
        {
            // Escalar el delta a FixedUpdate para evitar saltos
            float factor = Time.fixedDeltaTime / Time.deltaTime;

            Vector3 deltaPos = currentPlatform.DeltaPosition * factor;
            Quaternion deltaRot = Quaternion.Slerp(Quaternion.identity, currentPlatform.DeltaRotation, factor);

            if (applyPlatformRotation)
            {
                Vector3 relativePos = rb.position - currentPlatform.transform.position;
                Vector3 rotatedDelta = deltaRot * relativePos - relativePos;
                deltaPos += rotatedDelta;
            }

            rb.MovePosition(rb.position + deltaPos);

            // Opcional: si querés que también rote junto a la plataforma
            // rb.MoveRotation(deltaRot * rb.rotation);
        }
    }

    private void OnDrawGizmos()
    {
        Transform p = probe != null ? probe : transform;
        Vector3 origin = p.position + Vector3.up * 0.05f;
        Vector3 end = origin + Vector3.down * (groundCheckDistance + 0.05f);

        Gizmos.color = Color.red;
        Gizmos.DrawLine(origin, end);
        Gizmos.DrawWireSphere(origin, 0.05f);

        if (Application.isPlaying && currentPlatform != null)
        {
            Gizmos.color = Color.blue;
            Vector3 p1 = currentPlatform.transform.position;
            Vector3 p0 = p1 - currentPlatform.DeltaPosition;
            Gizmos.DrawLine(p0, p1);
        }
    }
}
