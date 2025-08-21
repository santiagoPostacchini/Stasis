using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class FollowMultipleTargetController : MonoBehaviour
{
    [Header("Lista de objetos a seguir")]
    public List<Transform> brothers = new List<Transform>();
    public AnimationCurve remapLerp = AnimationCurve.Linear(0, 0, 1, 1);

    [Tooltip("Tiempo antes de iniciar cada movimiento")]
    public float moveDelay = 0f;

    [Tooltip("Pausa tras llegar al punto")]
    public float stopDuration = 0.5f;

    [Tooltip("Tiempo que tarda en viajar de un punto al siguiente")]
    public float travelTime = 1f;

    public bool CanMove = true;

    // currentIndex = índice del punto en el que ESTÁS (no del que vas a ir)
    private int currentIndex = 0;
    private bool forward = true;

    private Coroutine moveRoutine; // guardia anti-race
    private Rigidbody rb;          // opcional: para frenar física

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        if (!CanMove)
        {
            CancelMovement();
            return;
        }

        if (brothers == null || brothers.Count == 0)
            return;

        if (moveRoutine != null)
            return;

        // Intentá ir al próximo destino (sin avanzar estado todavía)
        TryStartNextMove();
    }

    private void TryStartNextMove()
    {
        if (brothers.Count == 1)
            return;

        // Calculamos el PRÓXIMO destino como vista previa (no tocamos estado aún)
        int targetIndex;
        bool newForward;
        PeekNext(out targetIndex, out newForward);

        Transform target = brothers[Mathf.Clamp(targetIndex, 0, brothers.Count - 1)];
        if (target == null) return;

        // Arrancamos la corrutina y al COMPLETAR recién aplicamos el cambio de estado
        moveRoutine = StartCoroutine(MoveCoroutine(target, targetIndex, newForward));
    }

    /// <summary>
    /// Calcula el próximo índice y posible cambio de dirección SIN aplicar el cambio.
    /// </summary>
    private void PeekNext(out int targetIndex, out bool newForward)
    {
        newForward = forward;

        if (forward)
        {
            if (currentIndex >= brothers.Count - 1)
            {
                // Estamos en el último -> rebotar
                newForward = false;
                targetIndex = Mathf.Max(0, brothers.Count - 2);
            }
            else
            {
                targetIndex = currentIndex + 1;
            }
        }
        else
        {
            if (currentIndex <= 0)
            {
                // Estamos en el primero -> rebotar
                newForward = true;
                targetIndex = (brothers.Count > 1) ? 1 : 0;
            }
            else
            {
                targetIndex = currentIndex - 1;
            }
        }
    }

    private IEnumerator MoveCoroutine(Transform to, int targetIndex, bool newForward)
    {
        // Delay previo (cancelable)
        if (moveDelay > 0f)
        {
            float tDelay = 0f;
            while (tDelay < moveDelay)
            {
                if (!CanMove) { CancelMovement(); yield break; }
                tDelay += Time.deltaTime;
                yield return null;
            }
        }

        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;

        float dur = Mathf.Max(0.0001f, travelTime);
        float t = 0f;

        while (t < dur)
        {
            if (!CanMove) { CancelMovement(); yield break; }

            float k = remapLerp.Evaluate(t / dur);
            // (Unclamped por si la curva remapea >1/<0)
            transform.position = Vector3.LerpUnclamped(startPos, to.position, k);
            transform.rotation = Quaternion.SlerpUnclamped(startRot, to.rotation, k);

            t += Time.deltaTime;
            yield return null;
        }

        if (CanMove)
        {
            // Asegurar estado final
            transform.position = to.position;
            transform.rotation = to.rotation;

            // **Recién ahora** aplicamos el avance de estado
            currentIndex = targetIndex;
            forward = newForward;

            // Pausa (cancelable)
            if (stopDuration > 0f)
            {
                float tStop = 0f;
                while (tStop < stopDuration)
                {
                    if (!CanMove) { CancelMovement(); yield break; }
                    tStop += Time.deltaTime;
                    yield return null;
                }
            }
        }

        moveRoutine = null; // listo para un nuevo movimiento
    }

    private void CancelMovement()
    {
        if (moveRoutine != null)
        {
            StopAllCoroutines(); // mata cualquier corrutina de este componente
            moveRoutine = null;
        }

        // Opcional: frenar física por si hay arrastre
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        // Importante: **NO** tocamos currentIndex ni forward al cancelar.
    }

    private void OnDisable() => CancelMovement();
    private void OnDestroy() => CancelMovement();
}
