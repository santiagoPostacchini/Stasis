using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
public class FollowMultipleTargetController : MonoBehaviour
{
    [Header("Waypoints")]
    public List<Transform> brothers = new List<Transform>();

    [Header("Easing temporal (0..1)")]
    public AnimationCurve remapLerp = AnimationCurve.Linear(0, 0, 1, 1);

    [Header("Curva espacial (Bezier cuadrática)")]
    [Tooltip("Altura del arco entre puntos (unidades)")]
    public float arcHeight = 0f;
    [Tooltip("Eje para elevar el arco (si es null, usa Vector3.up)")]
    public Transform arcUp; // opcional

    [Header("Tiempos")]
    [Tooltip("Tiempo antes de iniciar cada movimiento")]
    public float moveDelay = 0f;
    [Tooltip("Tiempo de viaje entre puntos")]
    public float travelTime = 1f;
    [Tooltip("Pausa tras llegar al punto")]
    public float stopDuration = 0.5f;

    [Header("Orientación")]
    [Tooltip("Alinear rotación con la tangente de la curva")]
    public bool orientAlongPath = false;

    [Header("Control")]
    public bool CanMove = true;
    [Tooltip("Forzar Rigidbody a kinematic (recomendado)")]
    public bool forceKinematic = true;

    // currentIndex = índice del punto en el que ESTÁS (no del que vas)
    private int currentIndex = 0;
    private bool forward = true;

    private Coroutine moveRoutine;
    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (forceKinematic) rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    private void Update()
    {
        if (!CanMove)
        {
            CancelMovement();
            return;
        }

        if (brothers == null || brothers.Count == 0) return;
        if (moveRoutine != null) return;

        TryStartNextMove();
    }

    private void TryStartNextMove()
    {
        if (brothers.Count <= 1) return;

        int targetIndex;
        bool newForward;
        PeekNext(out targetIndex, out newForward);

        if (brothers[targetIndex] == null) return;

        moveRoutine = StartCoroutine(MoveCoroutine(targetIndex, newForward));
    }

    /// Calcula siguiente índice y dirección SIN aplicarlos.
    private void PeekNext(out int targetIndex, out bool newForward)
    {
        newForward = forward;

        if (forward)
        {
            if (currentIndex >= brothers.Count - 1)
            {
                newForward = false;
                targetIndex = Mathf.Max(0, brothers.Count - 2);
            }
            else targetIndex = currentIndex + 1;
        }
        else
        {
            if (currentIndex <= 0)
            {
                newForward = true;
                targetIndex = (brothers.Count > 1) ? 1 : 0;
            }
            else targetIndex = currentIndex - 1;
        }
    }

    private IEnumerator MoveCoroutine(int targetIndex, bool newForward)
    {
        // Snapshot de poses de este tramo (para estabilidad)
        Vector3 p1 = rb.position;                         // inicio
        Vector3 p2 = brothers[targetIndex].position;      // fin
        Quaternion startRot = rb.rotation;
        Quaternion endRot = brothers[targetIndex].rotation;

        // Bezier: punto de control elevado
        Vector3 up = (arcUp != null ? arcUp.up : Vector3.up);
        Vector3 mid = (p1 + p2) * 0.5f;
        Vector3 ctrl = mid + up * arcHeight;

        // Delay previo (alineado a física)
        if (moveDelay > 0f)
        {
            float tD = 0f;
            while (tD < moveDelay)
            {
                if (!CanMove) { CancelMovement(); yield break; }
                tD += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }
        }

        float dur = Mathf.Max(0.0001f, travelTime);
        float t = 0f;

        while (t < dur)
        {
            if (!CanMove) { CancelMovement(); yield break; }

            float u = remapLerp.Evaluate(t / dur); // 0..1

            // Bezier cuadrática
            Vector3 a = Vector3.LerpUnclamped(p1, ctrl, u);
            Vector3 b = Vector3.LerpUnclamped(ctrl, p2, u);
            Vector3 pos = Vector3.LerpUnclamped(a, b, u);
            rb.MovePosition(pos);

            if (orientAlongPath)
            {
                // Tangente de Bezier cuadrática: 2*(1-u)*(ctrl-p1) + 2*u*(p2-ctrl)
                Vector3 tan = 2f * (1f - u) * (ctrl - p1) + 2f * u * (p2 - ctrl);
                if (tan.sqrMagnitude > 1e-6f)
                {
                    Quaternion look = Quaternion.LookRotation(tan.normalized, Vector3.up);
                    Quaternion rot = Quaternion.SlerpUnclamped(startRot, look, u);
                    rb.MoveRotation(rot);
                }
            }
            else
            {
                Quaternion rot = Quaternion.SlerpUnclamped(startRot, endRot, u);
                rb.MoveRotation(rot);
            }

            t += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        if (CanMove)
        {
            // Pose final exacta
            rb.isKinematic = true;
            rb.MovePosition(p2);
            rb.MoveRotation(orientAlongPath
                ? Quaternion.LookRotation((p2 - ctrl).normalized, Vector3.up)
                : endRot);

            // Avanzar estado
            currentIndex = targetIndex;
            forward = newForward;

            // Pausa tras llegar (en física)
            if (stopDuration > 0f)
            {
                float tStop = 0f;
                while (tStop < stopDuration)
                {
                    if (!CanMove) { CancelMovement(); yield break; }
                    tStop += Time.fixedDeltaTime;
                    yield return new WaitForFixedUpdate();
                }
            }
        }

        moveRoutine = null; // listo para el siguiente tramo
    }

    private void CancelMovement()
    {
        if (moveRoutine != null)
        {
            StopAllCoroutines();
            moveRoutine = null;
        }
        // Frenar cualquier residuo físico
        rb.isKinematic = false;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        // No tocamos currentIndex ni forward.
    }

    private void OnDisable() => CancelMovement();
    private void OnDestroy() => CancelMovement();
}
