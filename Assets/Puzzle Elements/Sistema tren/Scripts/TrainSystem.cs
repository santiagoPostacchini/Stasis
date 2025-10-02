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
    public float elevatorSpeed = 2f;
    public float elevatorWaitSeconds = 2f;
    public float elevatorArriveThreshold = 0.02f;

    [Header("Barricade (Rigidbody)")]
    public Rigidbody barricadeRb;
    public Transform barricadeWP1;
    public Transform barricadeWP2;
    public float barricadeSpeed = 2.5f;
    public float barricadeArriveThreshold = 0.02f;

    [Header("Train (Rigidbody + Waypoints)")]
    public Rigidbody trainRb;
    public List<Transform> trainWaypoints = new List<Transform>();
    public Transform departure;
    public float trainSpeed = 4f;
    public float trainArriveThreshold = 0.03f;
    public float trainTeleportWaitSeconds = 2f;
    public float trainRotationSpeed = 180f; // grados por segundo

    [Header("Events")]
    public UnityEvent onTrainStarted;
    public UnityEvent onTrainStoppedAtDeparture;

    private bool systemEnabled;
    private bool trainRunRequested;
    private bool trainHaltAtDeparture;

    private Coroutine elevatorCo;
    private Coroutine barricadeCo;
    private Coroutine trainCo;

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

    public void StartAllMovement()
    {
        systemEnabled = true;
        trainHaltAtDeparture = false;

        ResolveHeights();

        if (elevatorCo != null) StopCoroutine(elevatorCo);
        if (IsElevatorConfigured())
            elevatorCo = StartCoroutine(ElevatorLoop());

        if (barricadeCo != null) StopCoroutine(barricadeCo);
        if (IsBarricadeConfigured())
            barricadeCo = StartCoroutine(BarricadeRaiseThenIdle());

        if (trainCo == null) trainCo = StartCoroutine(TrainSupervisor());
    }

    public void EnableSystem()
    {
        StartAllMovement();
    }

    public void DisableSystem()
    {
        systemEnabled = false;
        trainHaltAtDeparture = true;

        if (elevatorCo != null)
        {
            StopCoroutine(elevatorCo);
            elevatorCo = null;
        }

        if (barricadeCo != null) StopCoroutine(barricadeCo);
        if (IsBarricadeConfigured())
            barricadeCo = StartCoroutine(MoveBodyToDynamic(barricadeRb, barricadeDown.position, () => barricadeSpeed, barricadeArriveThreshold));
    }

    private void ResolveHeights()
    {
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
        else
        {
            elevatorLow = null;
            elevatorHigh = null;
        }

        if (barricadeWP1 != null && barricadeWP2 != null)
        {
            if (barricadeWP1.position.y <= barricadeWP2.position.y)
            {
                barricadeDown = barricadeWP1;
                barricadeUp = barricadeWP2;
            }
            else
            {
                barricadeDown = barricadeWP2;
                barricadeUp = barricadeWP1;
            }
        }
        else
        {
            barricadeDown = null;
            barricadeUp = null;
        }
    }

    private bool IsElevatorConfigured()
    {
        return elevatorRb != null && elevatorLow != null && elevatorHigh != null;
    }

    private bool IsBarricadeConfigured()
    {
        return barricadeRb != null && barricadeDown != null && barricadeUp != null;
    }

    // =============== Elevator (no rotation) ===============
    private IEnumerator ElevatorLoop()
    {
        if (!IsElevatorConfigured()) yield break;

        float dLow = Vector3.Distance(elevatorRb.position, elevatorLow.position);
        float dHigh = Vector3.Distance(elevatorRb.position, elevatorHigh.position);
        Transform next = (dLow <= dHigh) ? elevatorHigh : elevatorLow;

        while (systemEnabled && IsElevatorConfigured())
        {
            Transform a = next;
            Transform b = (a == elevatorLow) ? elevatorHigh : elevatorLow;

            yield return MoveBodyToDynamic(elevatorRb, a.position, () => elevatorSpeed, elevatorArriveThreshold);
            yield return WaitSecondsRealtime(elevatorWaitSeconds);
            if (!systemEnabled || !IsElevatorConfigured()) break;

            yield return MoveBodyToDynamic(elevatorRb, b.position, () => elevatorSpeed, elevatorArriveThreshold);
            yield return WaitSecondsRealtime(elevatorWaitSeconds);
            if (!systemEnabled || !IsElevatorConfigured()) break;

            next = a;
        }
    }

    // =============== Barricade (no rotation) ===============
    private IEnumerator BarricadeRaiseThenIdle()
    {
        if (!IsBarricadeConfigured()) yield break;

        float toDown = Vector3.Distance(barricadeRb.position, barricadeDown.position);
        if (toDown > barricadeArriveThreshold)
            yield return MoveBodyToDynamic(barricadeRb, barricadeDown.position, () => barricadeSpeed, barricadeArriveThreshold);

        if (!systemEnabled) yield break;

        yield return MoveBodyToDynamic(barricadeRb, barricadeUp.position, () => barricadeSpeed, barricadeArriveThreshold);

        RequestStartTrain();

        while (systemEnabled) yield return null;
    }

    private void RequestStartTrain()
    {
        if (!systemEnabled) return;
        trainRunRequested = true;
        if (onTrainStarted != null) onTrainStarted.Invoke();
    }

    // =============== Train (with rotation) ===============
    private IEnumerator TrainSupervisor()
    {
        if (!ValidateTrainSetup()) yield break;

        int firstIdx = 0;
        int lastIdx = trainWaypoints.Count - 1;
        int depIdx = trainWaypoints.IndexOf(departure);

        // Snap to DEPARTURE at boot
        if (!IsNear(trainRb.position, trainWaypoints[depIdx].position, trainArriveThreshold))
            yield return MoveBodyToDynamicWithRotation(trainRb, trainWaypoints[depIdx].position, () => trainSpeed, trainArriveThreshold, trainRotationSpeed);

        while (true)
        {
            while (!trainRunRequested && !trainHaltAtDeparture) yield return null;

            // Phase A: DEPARTURE -> END
            for (int i = depIdx + 1; i <= lastIdx; i++)
            {
                yield return MoveBodyToDynamicWithRotation(trainRb, trainWaypoints[i].position, () => trainSpeed, trainArriveThreshold, trainRotationSpeed);
            }

            yield return WaitSecondsRealtime(trainTeleportWaitSeconds);
            TeleportBodyTo(trainRb, trainWaypoints[firstIdx].position);

            if (trainHaltAtDeparture && depIdx == firstIdx)
            {
                trainRunRequested = false;
                if (onTrainStoppedAtDeparture != null) onTrainStoppedAtDeparture.Invoke();
                while (!systemEnabled) yield return null;
                trainHaltAtDeparture = false;
                continue;
            }

            // Phase B: FIRST -> END
            for (int i = firstIdx; i <= lastIdx; i++)
            {
                if (!IsNear(trainRb.position, trainWaypoints[i].position, trainArriveThreshold))
                    yield return MoveBodyToDynamicWithRotation(trainRb, trainWaypoints[i].position, () => trainSpeed, trainArriveThreshold, trainRotationSpeed);

                if (trainHaltAtDeparture && i == depIdx)
                {
                    trainRunRequested = false;
                    if (onTrainStoppedAtDeparture != null) onTrainStoppedAtDeparture.Invoke();
                    while (!systemEnabled) yield return null;
                    trainHaltAtDeparture = false;
                    break;
                }
            }
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

    // ===== Helpers =====
    // Normal movement (no rotation)
    private IEnumerator MoveBodyToDynamic(Rigidbody rb, Vector3 targetPos, System.Func<float> getSpeed, float arriveThreshold)
    {
        if (rb == null) yield break;
        WaitForFixedUpdate wait = new WaitForFixedUpdate();

        while (!IsNear(rb.position, targetPos, arriveThreshold))
        {
            float s = (getSpeed != null) ? Mathf.Max(0f, getSpeed()) : 0f;
            if (s <= 0f)
            {
                yield return wait;
                continue;
            }

            Vector3 dir = targetPos - rb.position;
            float step = s * Time.fixedDeltaTime;
            Vector3 next = (dir.sqrMagnitude <= step * step)
                ? targetPos
                : rb.position + dir.normalized * step;

            rb.MovePosition(next);
            yield return wait;
        }
        rb.MovePosition(targetPos);
    }

    // Movement with rotation (for train only)
    private IEnumerator MoveBodyToDynamicWithRotation(Rigidbody rb, Vector3 targetPos, System.Func<float> getSpeed, float arriveThreshold, float rotationSpeed)
    {
        if (rb == null) yield break;
        WaitForFixedUpdate wait = new WaitForFixedUpdate();

        while (!IsNear(rb.position, targetPos, arriveThreshold))
        {
            float s = (getSpeed != null) ? Mathf.Max(0f, getSpeed()) : 0f;
            if (s <= 0f)
            {
                yield return wait;
                continue;
            }

            Vector3 dir = targetPos - rb.position;
            float step = s * Time.fixedDeltaTime;
            Vector3 next = (dir.sqrMagnitude <= step * step)
                ? targetPos
                : rb.position + dir.normalized * step;

            if (dir.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
                rb.MoveRotation(
                    Quaternion.RotateTowards(rb.rotation, targetRot, rotationSpeed * Time.fixedDeltaTime)
                );
            }

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
            Gizmos.DrawCube(up.position, 0.14f * Vector3.one);
        }
    }
#endif
}
