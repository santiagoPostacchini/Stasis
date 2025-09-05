using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HedroPlatform : MonoBehaviour
{
    [Header("Ground Detection")]
    [SerializeField] private LayerMask groundLayer;
    public float groundCheckRadius = 0.25f;
    public float groundCheckDistance = 0.3f;
    public float offSetY = 0;
    public Transform groundProbe;

    [Header("Platform Settings")]
    public bool applyPlatformRotation = true;
    public float stickDownForce = 0.05f;
    public float maxSnapSpeed = 5f;

    private Rigidbody _rb;
    private MovingPlatformDeltaPosition _platform;
    private Vector3 _lastHitPoint;
    [SerializeField]private bool _isOnPlatform;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        if (groundProbe == null) groundProbe = transform;
    }

    private void FixedUpdate()
    {
        // Detectar suelo/plataforma
        RaycastHit hit;
        Vector3 origin = groundProbe.position + Vector3.up * 0.05f + new Vector3(0,offSetY,0);
        bool groundedByCast = Physics.SphereCast(
            origin, groundCheckRadius, Vector3.down,
            out hit, groundCheckDistance + 0.05f,
            groundLayer, QueryTriggerInteraction.Ignore
        );

        MovingPlatformDeltaPosition newPlatform = null;
        Vector3 hitPoint = Vector3.zero;

        if (groundedByCast)
        {
            newPlatform = hit.collider.GetComponentInParent<MovingPlatformDeltaPosition>();
            hitPoint = hit.point;
        }

        // Actualizar estado si cambia la plataforma
        if (newPlatform != _platform)
        {
            _platform = newPlatform;
            _isOnPlatform = (_platform != null);
            _lastHitPoint = hitPoint;
        }

        // Si estamos sobre plataforma, aplicar delta
        if (_isOnPlatform && _platform != null)
        {
            Vector3 platformDeltaPos = Vector3.ClampMagnitude(
                _platform.DeltaPosition,
                maxSnapSpeed * Time.deltaTime
            );

            Vector3 rotatedPos = transform.position;
            if (applyPlatformRotation)
            {
                rotatedPos = _platform.DeltaRotation *
                             (transform.position - _lastHitPoint) + _lastHitPoint;
            }

            Vector3 rotationDelta = rotatedPos - transform.position;
            Vector3 totalDelta = platformDeltaPos + rotationDelta;

            if (totalDelta.sqrMagnitude > 0f)
                _rb.MovePosition(_rb.position + totalDelta);
        }

        // Pegamento hacia abajo si estamos en plataforma y casi tocando suelo
        if (_isOnPlatform && groundedByCast && hit.distance > 0.01f)
        {
            _rb.MovePosition(_rb.position + Vector3.down * stickDownForce);
        }

        _lastHitPoint = hitPoint;
    }

    private void OnDrawGizmos()
    {
        Transform probe = groundProbe != null ? groundProbe : transform;
        Vector3 origin = probe.position + Vector3.up * 0.05f + new Vector3(0,offSetY,0);
        float totalDist = groundCheckDistance + 0.05f;
        Vector3 end = origin + Vector3.down * totalDist;

        bool hit = Physics.SphereCast(
            origin,
            groundCheckRadius,
            Vector3.down,
            out RaycastHit rh,
            totalDist,
            groundLayer,
            QueryTriggerInteraction.Ignore
        );

        Gizmos.color = hit
            ? ((Application.isPlaying && _platform != null) ? Color.cyan : Color.green)
            : Color.red;

        Gizmos.DrawWireSphere(origin, groundCheckRadius);
        Gizmos.DrawWireSphere(end, groundCheckRadius);

        Vector3 right = probe.right * groundCheckRadius;
        Vector3 forward = probe.forward * groundCheckRadius;
        Gizmos.DrawLine(origin + right, end + right);
        Gizmos.DrawLine(origin - right, end - right);
        Gizmos.DrawLine(origin + forward, end + forward);
        Gizmos.DrawLine(origin - forward, end - forward);

        if (hit)
        {
            Gizmos.DrawSphere(rh.point, 0.03f);
            Gizmos.DrawLine(rh.point, rh.point + rh.normal * 0.2f);
        }

        if (Application.isPlaying && _platform != null)
        {
            Gizmos.color = Color.blue;
            Vector3 p1 = _platform.transform.position;
            Vector3 p0 = p1 - _platform.DeltaPosition;
            Gizmos.DrawLine(p0, p1);
        }
    }
}
