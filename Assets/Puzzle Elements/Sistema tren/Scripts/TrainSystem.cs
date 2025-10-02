using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class TrainSystem : MonoBehaviour
{
    [Header("Container")]
    public HedronContainer container;

    [Header("Elevator (Rigidbody)")]
    public Rigidbody elevatorRb;
    public Transform elevatorWP1;
    public Transform elevatorWP2;
    public float elevatorSpeed = 2f;           // dynamic at runtime
    public float elevatorWaitSeconds = 2f;
    public float elevatorArriveThreshold = 0.02f;

    [Header("Barricade (Rigidbody)")]
    public Rigidbody barricadeRb;
    public Transform barricadeWP1;
    public Transform barricadeWP2;
    public float barricadeSpeed = 2.5f;        // dynamic at runtime
    public float barricadeArriveThreshold = 0.02f;

    [Header("Train (Rigidbody + Waypoints)")]
    public Rigidbody trainRb;
    public List<Transform> trainWaypoints = new List<Transform>(); // ordered path
    public Transform departure;                                      // must be one item from trainWaypoints
    public float trainSpeed = 4f;            // dynamic at runtime
    public float trainArriveThreshold = 0.03f;
    public float trainTeleportWaitSeconds = 2f;

    [Header("Events")]
    public UnityEvent onTrainStarted;
    public UnityEvent onTrainStoppedAtDeparture;

    private bool systemEnabled;
    private bool trainRunRequested;
    private bool trainHaltAtDeparture;

    private Coroutine elevatorCo;
    private Coroutine barricadeCo;
    private Coroutine trainCo;

    // Resueltos por altura para claridad automática
    private Transform elevatorLow;
    private Transform elevatorHigh;
    private Transform barricadeDown;
    private Transform barricadeUp;

    void Awake()
    {
        ResolveHeights();

        if (container != null)
        {
            container.onPlaced.AddListener(EnableSystem);
            container.onRemoved.AddListener(DisableSystem);
        }
    }

    void OnEnable()
    {
        if (container != null)
        {
            container.onPlaced.AddListener(EnableSystem);
            container.onRemoved.AddListener(DisableSystem);
        }
    }

    void OnDisable()
    {
        if (container != null)
        {
            container.onPlaced.RemoveListener(EnableSystem);
            container.onRemoved.RemoveListener(DisableSystem);
        }
        StopAllCoroutines();
        elevatorCo = null;
        barricadeCo = null;
        trainCo = null;
    }

    // Arranque manual equivalente a colocar el hedro
    public void StartAllMovement()
    {
        systemEnabled = true;
        trainHaltAtDeparture = false;

        ResolveHeights();

        if (elevatorCo != null) StopCoroutine(elevatorCo);
        elevatorCo = StartCoroutine(ElevatorLoop());

        if (barricadeCo != null) StopCoroutine(barricadeCo);
        barricadeCo = StartCoroutine(BarricadeRaiseThenIdle());

        if (trainCo == null) trainCo = StartCoroutine(TrainSupervisor());
    }

    public void EnableSystem()
    {
        StartAllMovement();
    }

    public void DisableSystem()
    {
        // Se marca inmediatamente el halt para que el tren apunte a Departure y frene
        systemEnabled = false;
        trainHaltAtDeparture = true;

        // Detener elevador donde esté
        if (elevatorCo != null)
        {
            StopCoroutine(elevatorCo);
            elevatorCo = null;
        }

        // Barricada baja a DOWN
        if (barricadeCo != null) StopCoroutine(barricadeCo);
        if (barricadeRb != null)
        {
            ResolveHeights();
            if (barricadeDown != null)
                barricadeCo = StartCoroutine(MoveBodyToDynamic(barricadeRb, barricadeDown.position, () => barricadeSpeed, barricadeArriveThreshold));
        }
    }

    private void ResolveHeights()
    {
        // Elevator: low/high por Y
        if (elevatorWP1 != null && elevatorWP2 != null)
        {
            if (elevatorWP1.position.y <= elevatorWP2.position.y)
            {
                elevatorLow = elevatorWP1;
                elevatorHigh = elevatorWP2;
            }
            else
            {
                elevatorLow = elevatorWP2;
                elevatorHigh = elevatorWP1;
            }
        }

        // Barricade: down/up por Y
        if (barricadeWP1 != null && barricadeWP2 != null)
        {
            if (barricadeWP1.position.y <= barricadeWP2.position.y)
            {
                barricadeDown = barricadeWP1;
                barricadeUp = barricadeWP2;   // llegar aquí dispara el tren
            }
            else
            {
                barricadeDown = barricadeWP2;
                barricadeUp = barricadeWP1;   // llegar aquí dispara el tren
            }
        }
    }

    // ---------------- Elevator: velocidad dinámica ----------------
    private IEnumerator ElevatorLoop()
    {
        if (elevatorRb == null || elevatorLow == null || elevatorHigh == null) yield break;

        float dLow = Vector3.Distance(elevatorRb.position, elevatorLow.position);
        float dHigh = Vector3.Distance(elevatorRb.position, elevatorHigh.position);
        Transform next = (dLow <= dHigh) ? elevatorHigh : elevatorLow;

        while (systemEnabled)
        {
            Transform a = next;
            Transform b = (a == elevatorLow) ? elevatorHigh : elevatorLow;

            // Va a 'a'
            yield return MoveBodyToDynamic(elevatorRb, a.position, () => elevatorSpeed, elevatorArriveThreshold);
            yield return WaitSecondsRealtime(elevatorWaitSeconds);
            if (!systemEnabled) break;

            // Va a 'b'
            yield return MoveBodyToDynamic(elevatorRb, b.position, () => elevatorSpeed, elevatorArriveThreshold);
            yield return WaitSecondsRealtime(elevatorWaitSeconds);
            if (!systemEnabled) break;

            next = a; // ping-pong
        }
    }

    // ---------------- Barricade ----------------
    private IEnumerator BarricadeRaiseThenIdle()
    {
        if (barricadeRb == null || barricadeDown == null || barricadeUp == null) yield break;

        // Garantiza que arranca abajo
        float toDown = Vector3.Distance(barricadeRb.position, barricadeDown.position);
        if (toDown > barricadeArriveThreshold)
            yield return MoveBodyToDynamic(barricadeRb, barricadeDown.position, () => barricadeSpeed, barricadeArriveThreshold);

        if (!systemEnabled) yield break;

        // Sube
        yield return MoveBodyToDynamic(barricadeRb, barricadeUp.position, () => barricadeSpeed, barricadeArriveThreshold);

        // Al llegar arriba, habilita el tren
        RequestStartTrain();

        while (systemEnabled) yield return null;
    }

    private void RequestStartTrain()
    {
        if (!systemEnabled) return;
        trainRunRequested = true;
        if (onTrainStarted != null) onTrainStarted.Invoke();
    }

    // ---------------- Train: arranca en Departure, frena en Departure al quitar hedro ----------------
    private IEnumerator TrainSupervisor()
    {
        if (!ValidateTrainSetup()) yield break;

        int firstIdx = 0;
        int lastIdx = trainWaypoints.Count - 1;
        int depIdx = trainWaypoints.IndexOf(departure);

        // Coloca el tren en Departure al iniciar
        if (!IsNear(trainRb.position, trainWaypoints[depIdx].position, trainArriveThreshold))
            yield return MoveBodyToDynamic(trainRb, trainWaypoints[depIdx].position, () => trainSpeed, trainArriveThreshold);

        while (true)
        {
            // Espera a que la barricada habilite el tren o que se pida frenar (por si el hedro se quitó rápido)
            while (!trainRunRequested && !trainHaltAtDeparture) yield return null;

            // Segmento: DEPARTURE -> ... -> END
            for (int i = depIdx + 1; i <= lastIdx; i++)
            {
                // Si se pidió frenar mientras vamos hacia END, REVERSA inmediato hacia DEPARTURE y frenate ahí
                if (trainHaltAtDeparture)
                {
                    // Reversa desde el waypoint actual hacia depIdx
                    for (int j = i - 1; j >= depIdx; j--)
                        yield return MoveBodyToDynamic(trainRb, trainWaypoints[j].position, () => trainSpeed, trainArriveThreshold);

                    trainRunRequested = false;
                    if (onTrainStoppedAtDeparture != null) onTrainStoppedAtDeparture.Invoke();

                    // Quedar detenido en Departure hasta que vuelva a habilitarse
                    while (!systemEnabled) yield return null;
                    trainHaltAtDeparture = false;
                    goto ContinueLoop;
                }

                yield return MoveBodyToDynamic(trainRb, trainWaypoints[i].position, () => trainSpeed, trainArriveThreshold);
            }

            // En END: espera y teletransporta a FIRST (solo si está habilitado)
            if (!trainHaltAtDeparture)
            {
                yield return WaitSecondsRealtime(trainTeleportWaitSeconds);
                TeleportBodyTo(trainRb, trainWaypoints[firstIdx].position);
            }
            else
            {
                // Si justo se pidió frenar en END, ir hacia atrás hasta Departure y frenar
                for (int j = lastIdx - 1; j >= depIdx; j--)
                    yield return MoveBodyToDynamic(trainRb, trainWaypoints[j].position, () => trainSpeed, trainArriveThreshold);

                trainRunRequested = false;
                if (onTrainStoppedAtDeparture != null) onTrainStoppedAtDeparture.Invoke();
                while (!systemEnabled) yield return null;
                trainHaltAtDeparture = false;
            }

        ContinueLoop:
            ;
        }
    }

    private bool ValidateTrainSetup()
    {
        if (trainRb == null) return false;
        if (trainWaypoints == null || trainWaypoints.Count < 2) return false;
        if (departure == null) return false;
        int dep = trainWaypoints.IndexOf(departure);
        return dep >= 0 && dep < trainWaypoints.Count;
    }

    // ---------------- Helpers de movimiento: velocidad dinámica ----------------
    private IEnumerator MoveBodyToDynamic(Rigidbody rb, Vector3 targetPos, System.Func<float> getSpeed, float arriveThreshold)
    {
        if (rb == null) yield break;
        WaitForFixedUpdate wait = new WaitForFixedUpdate();

        while (!IsNear(rb.position, targetPos, arriveThreshold))
        {
            float s = 0f;
            if (getSpeed != null) s = Mathf.Max(0f, getSpeed());
            if (s <= 0f)
            {
                // velocidad 0: no avanzar
                yield return wait;
                continue;
            }

            Vector3 dir = targetPos - rb.position;
            float step = s * Time.fixedDeltaTime;
            Vector3 next = (dir.sqrMagnitude <= step * step) ? targetPos : rb.position + dir.normalized * step;
            rb.MovePosition(next);
            yield return wait;
        }
        rb.MovePosition(targetPos);
    }

    private static bool IsNear(Vector3 a, Vector3 b, float eps)
    {
        return (a - b).sqrMagnitude <= eps * eps;
    }

    private static IEnumerator WaitSecondsRealtime(float seconds)
    {
        float end = Time.unscaledTime + seconds;
        while (Time.unscaledTime < end) yield return null;
    }

    private void TeleportBodyTo(Rigidbody rb, Vector3 pos)
    {
        if (rb == null) return;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
#if UNITY_2022_1_OR_NEWER
        rb.position = pos;
#else
        rb.MovePosition(pos);
#endif
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        // Gizmos de ayuda para ver alto/bajo y arriba/abajo
        if (elevatorWP1 != null && elevatorWP2 != null)
        {
            Transform low = (elevatorWP1.position.y <= elevatorWP2.position.y) ? elevatorWP1 : elevatorWP2;
            Transform high = (low == elevatorWP1) ? elevatorWP2 : elevatorWP1;
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(low.position, 0.08f);
            Gizmos.color = Color.magenta;
            Gizmos.DrawCube(high.position, Vector3.one * 0.14f);
        }

        if (barricadeWP1 != null && barricadeWP2 != null)
        {
            Transform down = (barricadeWP1.position.y <= barricadeWP2.position.y) ? barricadeWP1 : barricadeWP2;
            Transform up = (down == barricadeWP1) ? barricadeWP2 : barricadeWP1;
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(down.position, 0.08f);
            Gizmos.color = Color.red;
            Gizmos.DrawCube(up.position, Vector3.one * 0.14f);
        }
    }
#endif
}
