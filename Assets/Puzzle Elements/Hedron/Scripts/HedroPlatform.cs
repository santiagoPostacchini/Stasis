using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HedroPlatform : MonoBehaviour
{
    [Header("Detección de plataforma")]
    public LayerMask platformMask;            // capa de “suelo / plataforma”
    public float groundCheckRadius = 0.25f; // radio de chequeo
    public float groundCheckDistance = 0.3f;// distancia hacia abajo
    public Transform groundProbe;           // punto de origen (ej: pies del player)

    [Header("Ajustes")]
    public bool applyPlatformRotation = true;
    public float stickDownForce = 0.05f;    // empuje mínimo hacia abajo para no “levitar”
    public float maxSnapSpeed = 5f;         // límite para no pegar saltos con plataformas muy rápidas

    private Rigidbody _rb;
    [SerializeField] private MovingPlatformDeltaPosition _platform;       // plataforma actual (si hay)
    private Vector3 _lastHitPoint;          // punto de contacto del frame anterior
    [SerializeField] private bool _isOnPlatform;



    private bool isGrounded;
    [SerializeField] private LayerMask groundLayer;
    
    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        if (groundProbe == null) groundProbe = transform;
        // Recomiendo:
        // _cc.minMoveDistance = 0f;
    }

    private void LateUpdate()
    {
        // 1) Detectar si estamos sobre una plataforma válida
        RaycastHit hit;
        Vector3 origin = groundProbe.position + Vector3.up * 0.05f;
        bool groundedByCast = Physics.SphereCast(
            origin, groundCheckRadius, Vector3.down,
            out hit, groundCheckDistance + 0.05f, platformMask, QueryTriggerInteraction.Ignore);
       

        MovingPlatformDeltaPosition newPlatform = null;
        Vector3 hitPoint = Vector3.zero;

        if (groundedByCast)
        {
            newPlatform = hit.collider.GetComponentInParent<MovingPlatformDeltaPosition>();
            Debug.Log("NEWPLATFORM " + newPlatform);
            hitPoint = hit.point;
        }

        // 2) Si cambiamos de plataforma (o entramos/salimos), actualizar estado
        if (newPlatform != _platform)
        {
            _platform = newPlatform;
            _isOnPlatform = (_platform != null);
            _lastHitPoint = hitPoint;
        }

        // 3) Si estamos sobre plataforma, aplicar su delta (traslación + rotación)
        if (_isOnPlatform && _platform != null)
        {
            Vector3 platformDeltaPos = Vector3.ClampMagnitude(_platform.DeltaPosition, maxSnapSpeed * Time.deltaTime);

            // Rotación alrededor del punto de contacto anterior (para que no “deslice” al rotar)
            Vector3 rotatedPos = transform.position;
            if (applyPlatformRotation)
            {
                rotatedPos = _platform.DeltaRotation * (transform.position - _lastHitPoint) + _lastHitPoint;
            }
            Vector3 rotationDelta = rotatedPos - transform.position;

            // Delta total a aplicar al player
            Vector3 totalDelta = platformDeltaPos + rotationDelta;

            if (totalDelta.sqrMagnitude > 0f)
                _rb.MovePosition(_rb.position + totalDelta);
        }

        // 4) Pequeño “pegamento” hacia abajo para mantener contacto
        if (_isOnPlatform && IsGrounded())
        {
            _rb.MovePosition(_rb.position + Vector3.down * stickDownForce);

        }

        _lastHitPoint = hitPoint;
    }
    private bool IsGrounded()
    {
        isGrounded = Physics.Raycast(transform.position, Vector3.down, groundCheckDistance, groundLayer);
        return isGrounded;
    }
    private void OnDrawGizmos()
    {
        // Origen del chequeo
        Transform probe = groundProbe != null ? groundProbe : transform;
        Vector3 origin = probe.position + Vector3.up * 0.05f;
        float totalDist = groundCheckDistance + 0.05f;
        Vector3 end = origin + Vector3.down * totalDist;

        // Hacemos el mismo SphereCast que usa el script
        bool hit = Physics.SphereCast(
            origin,
            groundCheckRadius,
            Vector3.down,
            out RaycastHit rh,
            totalDist,
            groundLayer,
            QueryTriggerInteraction.Ignore
        );

        // Color según estado
        Gizmos.color = hit
            ? ((Application.isPlaying && _platform != null) ? Color.cyan : Color.green)
            : Color.red;

        // Dibujo "cápsula" (aproximación con dos esferas y 4 líneas)
        Gizmos.DrawWireSphere(origin, groundCheckRadius);
        Gizmos.DrawWireSphere(end, groundCheckRadius);

        Vector3 right = probe.right * groundCheckRadius;
        Vector3 forward = probe.forward * groundCheckRadius;
        Gizmos.DrawLine(origin + right, end + right);
        Gizmos.DrawLine(origin - right, end - right);
        Gizmos.DrawLine(origin + forward, end + forward);
        Gizmos.DrawLine(origin - forward, end - forward);

        // Punto de impacto y normal (si hay)
        if (hit)
        {
            Gizmos.DrawSphere(rh.point, 0.03f);
            Gizmos.DrawLine(rh.point, rh.point + rh.normal * 0.2f);
        }

        // Visual del delta de la plataforma (solo en Play y si hay plataforma)
        if (Application.isPlaying && _platform != null)
        {
            Gizmos.color = Color.blue;
            Vector3 p1 = _platform.transform.position;
            Vector3 p0 = p1 - _platform.DeltaPosition;
            Gizmos.DrawLine(p0, p1); // línea que indica cuánto se movió este frame
        }
    }
}
