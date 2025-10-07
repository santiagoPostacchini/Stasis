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
    public float elevatorAcceleration = 20f;
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
    public float trainRotationSpeed = 180f;

    [Header("Events")]
    public UnityEvent onTrainStarted;
    public UnityEvent onTrainStoppedAtDeparture;

    // ---- Flags/estado global ----
    private bool systemEnabled;
    private bool trainRunRequested;
    private bool trainHaltAtDeparture;

    // ---- Waypoints resueltos ----
    private Transform elevatorLow;
    private Transform elevatorHigh;
    private Transform barricadeDown;
    private Transform barricadeUp;

    // ---- Elevator ----
    private enum ElevatorState { Idle, ToA, WaitAtA, ToB, WaitAtB }
    private ElevatorState elevState = ElevatorState.Idle;
    private Vector3 elevTarget;
    private float elevCurrentSpeed;
    private float elevWaitUntil;
    private bool elevConfigured;

    // ---- Barricade ----
    private enum BarricadeState { Idle, ToDown, ToUp, HoldUp }
    private BarricadeState barrState = BarricadeState.Idle;
    private Vector3 barrTarget;
    private bool barrConfigured;

    // ---- Train ----
    private enum TrainPhase { IdleAtDeparture, RunDepToEnd, TeleportWait, RunFirstToEnd }
    private TrainPhase trainPhase = TrainPhase.IdleAtDeparture;
    private int trainFirstIdx;
    private int trainLastIdx;
    private int trainDepIdx;
    private int trainCurrentIdx;
    private float trainWaitUntil;
    private bool trainConfigured;

    void Awake()
    {
        ResolveHeights();
        HookContainer(true);
        PrepareSystems();
    }

    void OnEnable()
    {
        HookContainer(true);
        PrepareSystems();
    }

    void OnDisable()
    {
        HookContainer(false);
        systemEnabled = false;
    }

    public void StartAllMovement()
    {
        systemEnabled = true;
        trainHaltAtDeparture = false;
        ResolveHeights();
        PrepareSystems(true);
    }

    public void EnableSystem() => StartAllMovement();

    public void DisableSystem()
    {
        systemEnabled = false;
        trainHaltAtDeparture = true;

        // Colocar barricada hacia abajo como “shutdown” suave
        if (barrConfigured && barricadeRb != null && barricadeDown != null)
        {
            barrState = BarricadeState.ToDown;
            barrTarget = barricadeDown.position;
        }
    }

    void FixedUpdate()
    {
        elevConfigured = IsElevatorConfigured();
        barrConfigured = IsBarricadeConfigured();
        trainConfigured = ValidateTrainSetup();

        if (!systemEnabled)
        {
            // Frenos seguros
            if (elevatorRb) { elevatorRb.velocity = Vector3.zero; elevatorRb.angularVelocity = Vector3.zero; }
            if (barricadeRb) { barricadeRb.velocity = Vector3.zero; barricadeRb.angularVelocity = Vector3.zero; }
            if (trainRb) { trainRb.velocity = Vector3.zero; trainRb.angularVelocity = Vector3.zero; }
            return;
        }

        TickElevator();
        TickBarricade();
        TickTrain();
    }

    // --------- Setup ---------
    private void HookContainer(bool add)
    {
        if (container == null) return;
        if (add)
        {
            container.onPlaced.AddListener(EnableSystem);
            container.onRemoved.AddListener(DisableSystem);
        }
        else
        {
            container.onPlaced.RemoveListener(EnableSystem);
            container.onRemoved.RemoveListener(DisableSystem);
        }
    }

    private void PrepareSystems(bool resetPositions = false)
    {
        // Elevator
        elevCurrentSpeed = 0f;
        if (elevConfigured)
        {
            Vector3 p = elevatorRb.position;
            float dLow = Vector3.Distance(p, elevatorLow.position);
            float dHigh = Vector3.Distance(p, elevatorHigh.position);
            Transform first = (dLow <= dHigh) ? elevatorHigh : elevatorLow;
            elevTarget = first.position;
            elevState = ElevatorState.ToA;
            elevWaitUntil = 0f;
        }
        else elevState = ElevatorState.Idle;

        // Barricade
        if (barrConfigured)
        {
            float toDown = Vector3.Distance(barricadeRb.position, barricadeDown.position);
            barrState = (toDown > barricadeArriveThreshold) ? BarricadeState.ToDown : BarricadeState.ToUp;
            barrTarget = (barrState == BarricadeState.ToDown) ? barricadeDown.position : barricadeUp.position;
        }
        else barrState = BarricadeState.Idle;

        // Train
        if (ValidateTrainSetup())
        {
            trainFirstIdx = 0;
            trainLastIdx = trainWaypoints.Count - 1;
            trainDepIdx = trainWaypoints.IndexOf(departure);

            if (resetPositions && !IsNear(trainRb.position, trainWaypoints[trainDepIdx].position, trainArriveThreshold))
                TeleportBodyTo(trainRb, trainWaypoints[trainDepIdx].position);

            trainPhase = TrainPhase.IdleAtDeparture;
            trainRunRequested = false;
        }
        else trainPhase = TrainPhase.IdleAtDeparture;
    }

    private void ResolveHeights()
    {
        if (elevatorWP1 != null && elevatorWP2 != null)
        {
            if (elevatorWP1.position.y <= elevatorWP2.position.y) { elevatorLow = elevatorWP1; elevatorHigh = elevatorWP2; }
            else { elevatorLow = elevatorWP2; elevatorHigh = elevatorWP1; }
        }
        else { elevatorLow = null; elevatorHigh = null; }

        if (barricadeWP1 != null && barricadeWP2 != null)
        {
            if (barricadeWP1.position.y <= barricadeWP2.position.y) { barricadeDown = barricadeWP1; barricadeUp = barricadeWP2; }
            else { barricadeDown = barricadeWP2; barricadeUp = barricadeWP1; }
        }
        else { barricadeDown = null; barricadeUp = null; }
    }

    private bool IsElevatorConfigured() =>
        elevatorRb != null && elevatorLow != null && elevatorHigh != null;

    private bool IsBarricadeConfigured() =>
        barricadeRb != null && barricadeDown != null && barricadeUp != null;

    private bool ValidateTrainSetup()
    {
        if (trainRb == null) return false;
        if (trainWaypoints == null || trainWaypoints.Count < 2) return false;
        if (departure == null) return false;
        int dep = trainWaypoints.IndexOf(departure);
        return dep >= 0 && dep < trainWaypoints.Count;
    }

    // --------- Elevator (FixedUpdate) ---------
    private void TickElevator()
    {
        if (!elevConfigured) return;

        switch (elevState)
        {
            case ElevatorState.ToA:
                {
                    bool reached = MoveElevatorAcc(elevTarget);
                    if (reached)
                    {
                        elevCurrentSpeed = 0f;
                        elevWaitUntil = Time.fixedUnscaledTime + elevatorWaitSeconds;
                        elevState = ElevatorState.WaitAtA;
                    }
                    break;
                }
            case ElevatorState.WaitAtA:
                {
                    elevatorRb.velocity = Vector3.zero;
                    if (Time.fixedUnscaledTime >= elevWaitUntil)
                    {
                        elevTarget = (IsNear(elevTarget, elevatorHigh.position, 1e-4f)) ? elevatorLow.position : elevatorHigh.position;
                        elevState = ElevatorState.ToB;
                    }
                    break;
                }
            case ElevatorState.ToB:
                {
                    bool reached = MoveElevatorAcc(elevTarget);
                    if (reached)
                    {
                        elevCurrentSpeed = 0f;
                        elevWaitUntil = Time.fixedUnscaledTime + elevatorWaitSeconds;
                        elevState = ElevatorState.WaitAtB;
                    }
                    break;
                }
            case ElevatorState.WaitAtB:
                {
                    elevatorRb.velocity = Vector3.zero;
                    if (Time.fixedUnscaledTime >= elevWaitUntil)
                    {
                        elevTarget = (IsNear(elevTarget, elevatorHigh.position, 1e-4f)) ? elevatorLow.position : elevatorHigh.position;
                        elevState = ElevatorState.ToA;
                    }
                    break;
                }
        }
    }

    private bool MoveElevatorAcc(Vector3 targetPos)
    {
        Vector3 toTarget = targetPos - elevatorRb.position;
        float distance = toTarget.magnitude;
        if (distance <= elevatorArriveThreshold)
        {
            elevatorRb.MovePosition(targetPos);
            elevatorRb.velocity = Vector3.zero;
            return true;
        }

        float maxSpeed = Mathf.Max(0f, elevatorSpeed);
        float accel = Mathf.Max(0f, elevatorAcceleration);
        float maxSpeedForStop = Mathf.Sqrt(Mathf.Max(0f, 2f * accel * distance));
        float desiredSpeed = Mathf.Min(maxSpeed, maxSpeedForStop);
        elevCurrentSpeed = Mathf.MoveTowards(elevCurrentSpeed, desiredSpeed, accel * Time.fixedDeltaTime);

        float step = elevCurrentSpeed * Time.fixedDeltaTime;
        Vector3 dir = (distance > 1e-5f) ? (toTarget / distance) : Vector3.zero;
        Vector3 next = (step >= distance) ? targetPos : elevatorRb.position + dir * step;

        elevatorRb.velocity = (next - elevatorRb.position) / Time.fixedDeltaTime;
        elevatorRb.MovePosition(next);
        return false;
    }

    // --------- Barricade (FixedUpdate) ---------
    private void TickBarricade()
    {
        if (!barrConfigured) return;

        switch (barrState)
        {
            case BarricadeState.ToDown:
                {
                    barrTarget = barricadeDown.position;
                    bool reached = MoveBodyLinear(barricadeRb, barrTarget, barricadeSpeed, barricadeArriveThreshold);
                    if (reached) barrState = BarricadeState.ToUp;
                    break;
                }
            case BarricadeState.ToUp:
                {
                    barrTarget = barricadeUp.position;
                    bool reached = MoveBodyLinear(barricadeRb, barrTarget, barricadeSpeed, barricadeArriveThreshold);
                    if (reached)
                    {
                        RequestStartTrain();
                        barrState = BarricadeState.HoldUp;
                    }
                    break;
                }
            case BarricadeState.HoldUp:
                barricadeRb.velocity = Vector3.zero;
                break;
            case BarricadeState.Idle:
                break;
        }
    }

    private void RequestStartTrain()
    {
        if (!systemEnabled) return;
        trainRunRequested = true;
        onTrainStarted?.Invoke();
    }

    // --------- Train (FixedUpdate) ---------
    private void TickTrain()
    {
        if (!trainConfigured) return;

        switch (trainPhase)
        {
            case TrainPhase.IdleAtDeparture:
                {
                    // Asegurar posición en departure
                    Vector3 depPos = trainWaypoints[trainDepIdx].position;
                    if (!IsNear(trainRb.position, depPos, trainArriveThreshold))
                        TeleportBodyTo(trainRb, depPos);

                    if (trainRunRequested)
                    {
                        trainCurrentIdx = trainDepIdx + 1;
                        trainPhase = TrainPhase.RunDepToEnd;
                    }
                    break;
                }

            case TrainPhase.RunDepToEnd:
                {
                    if (trainCurrentIdx > trainLastIdx)
                    {
                        trainWaitUntil = Time.fixedUnscaledTime + trainTeleportWaitSeconds;
                        trainPhase = TrainPhase.TeleportWait;
                        break;
                    }

                    Vector3 target = trainWaypoints[trainCurrentIdx].position;
                    bool reached = MoveBodyLinearWithRot(trainRb, target, trainSpeed, trainArriveThreshold, trainRotationSpeed);
                    if (reached) trainCurrentIdx++;
                    break;
                }

            case TrainPhase.TeleportWait:
                {
                    trainRb.velocity = Vector3.zero;
                    if (Time.fixedUnscaledTime >= trainWaitUntil)
                    {
                        TeleportBodyTo(trainRb, trainWaypoints[trainFirstIdx].position);

                        if (trainHaltAtDeparture && trainDepIdx == trainFirstIdx)
                        {
                            trainRunRequested = false;
                            onTrainStoppedAtDeparture?.Invoke();
                            trainHaltAtDeparture = false;
                            trainPhase = TrainPhase.IdleAtDeparture;
                        }
                        else
                        {
                            trainCurrentIdx = trainFirstIdx;
                            trainPhase = TrainPhase.RunFirstToEnd;
                        }
                    }
                    break;
                }

            case TrainPhase.RunFirstToEnd:
                {
                    if (trainCurrentIdx > trainLastIdx)
                    {
                        // vuelta completa, volver a esperar siguiente trigger
                        trainPhase = TrainPhase.IdleAtDeparture;
                        break;
                    }

                    Vector3 target = trainWaypoints[trainCurrentIdx].position;
                    bool reached = MoveBodyLinearWithRot(trainRb, target, trainSpeed, trainArriveThreshold, trainRotationSpeed);

                    if (reached)
                    {
                        // stop en departure si se pidió
                        if (trainHaltAtDeparture && trainCurrentIdx == trainDepIdx)
                        {
                            trainRunRequested = false;
                            onTrainStoppedAtDeparture?.Invoke();
                            trainHaltAtDeparture = false;
                            trainPhase = TrainPhase.IdleAtDeparture;
                        }
                        else
                        {
                            trainCurrentIdx++;
                        }
                    }
                    break;
                }
        }
    }

    // --------- Helpers de movimiento ---------
    private bool MoveBodyLinear(Rigidbody rb, Vector3 targetPos, float speed, float arriveThreshold)
    {
        Vector3 toTarget = targetPos - rb.position;
        float dist = toTarget.magnitude;
        if (dist <= arriveThreshold)
        {
            rb.MovePosition(targetPos);
            rb.velocity = Vector3.zero;
            return true;
        }

        float s = Mathf.Max(0f, speed);
        float step = s * Time.fixedDeltaTime;
        Vector3 dir = (dist > 1e-5f) ? toTarget / dist : Vector3.zero;
        Vector3 next = (step >= dist) ? targetPos : rb.position + dir * step;

        rb.MovePosition(next);
        rb.velocity = (next - rb.position) / Time.fixedDeltaTime;
        return false;
    }

    private bool MoveBodyLinearWithRot(Rigidbody rb, Vector3 targetPos, float speed, float arriveThreshold, float rotSpeedDeg)
    {
        Vector3 toTarget = targetPos - rb.position;
        float dist = toTarget.magnitude;
        if (dist <= arriveThreshold)
        {
            rb.MovePosition(targetPos);
            rb.velocity = Vector3.zero;
            return true;
        }

        float s = Mathf.Max(0f, speed);
        float step = s * Time.fixedDeltaTime;
        Vector3 dir = (dist > 1e-5f) ? toTarget / dist : Vector3.forward;
        Vector3 next = (step >= dist) ? targetPos : rb.position + dir * step;

        if (dir.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
            Quaternion nextRot = Quaternion.RotateTowards(rb.rotation, targetRot, rotSpeedDeg * Time.fixedDeltaTime);
            rb.MoveRotation(nextRot);
        }

        rb.MovePosition(next);
        rb.velocity = (next - rb.position) / Time.fixedDeltaTime;
        return false;
    }

    private static bool IsNear(Vector3 a, Vector3 b, float eps) =>
        (a - b).sqrMagnitude <= eps * eps;

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
