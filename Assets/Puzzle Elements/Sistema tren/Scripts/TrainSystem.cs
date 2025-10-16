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

    public enum TrainPathMode { Loop, PingPong }

    [Header("Train (Rigidbody + Waypoints)")]
    public Rigidbody trainRb;
    public List<Transform> trainWaypoints = new List<Transform>();
    public Transform departure;
    public float trainSpeed = 4f;
    public float trainArriveThreshold = 0.03f;
    [Tooltip("Solo se usa en modo Loop (salto del último al primero).")]
    public float trainTeleportWaitSeconds = 2f;
    [Tooltip("Loop = salta del último al primero; PingPong = va y vuelve sin teletransportar.")]
    public TrainPathMode trainPathMode = TrainPathMode.Loop;
    public float trainRotationSpeed = 180f; // legacy, no usado

    [Header("Events")]
    public UnityEvent onTrainStarted;
    public UnityEvent onTrainStoppedAtDeparture;

    private bool systemEnabled;
    private bool trainRunRequested;
    private bool trainHaltAtDeparture;

    private Transform elevatorLow;
    private Transform elevatorHigh;
    private Transform barricadeDown;
    private Transform barricadeUp;

    private enum ElevatorState { Idle, ToA, WaitAtA, ToB, WaitAtB }
    private ElevatorState elevState = ElevatorState.Idle;
    private Vector3 elevTarget;
    private float elevCurrentSpeed;
    private float elevWaitUntil;
    private bool elevConfigured;

    private enum BarricadeState { Idle, ToDown, ToUp, HoldUp, HoldDown }
    private BarricadeState barrState = BarricadeState.Idle;
    private Vector3 barrTarget;
    private bool barrConfigured;
    private bool barricadeForceDown;

    // --- Nuevo modelo de tren (soporta Loop y PingPong) ---
    private bool trainConfigured;
    private int trainFirstIdx;
    private int trainLastIdx;
    private int trainDepIdx;
    private int trainCurrentIdx;
    private int trainDir = +1; // +1 adelante, -1 atrás (para PingPong)
    private float trainWaitUntil;
    private enum TrainState { IdleAtDeparture, Running, LoopTeleportWait }
    private TrainState trainState = TrainState.IdleAtDeparture;

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

    // API
    public void StartAllMovement()
    {
        systemEnabled = true;
        trainHaltAtDeparture = false;
        barricadeForceDown = false;
        ResolveHeights();
        PrepareSystems(true);
    }

    public void EnableSystem() => StartAllMovement();

    public void RequestTrainHaltAtDeparture()
    {
        if (!trainConfigured) return;
        trainHaltAtDeparture = true;
        if (trainState == TrainState.IdleAtDeparture)
            trainRunRequested = true;
    }

    public void DisableSystem()
    {
        RequestTrainHaltAtDeparture();
        barricadeForceDown = true;
        if (barrConfigured && barricadeRb != null && barricadeDown != null)
        {
            barrState = BarricadeState.ToDown;
            barrTarget = barricadeDown.position;
        }
    }

    public void HardStopAll()
    {
        systemEnabled = false;
        // Kinematic: no tocar velocity/angularVelocity.
    }

    void FixedUpdate()
    {
        elevConfigured = IsElevatorConfigured();
        barrConfigured = IsBarricadeConfigured();
        trainConfigured = ValidateTrainSetup();

        TickElevator();
        TickBarricade();
        TickTrain();
    }

    // Setup
    private void HookContainer(bool add)
    {
        if (container == null) return;
        if (add)
        {
            container.onPlaced.AddListener(EnableSystem);
            container.onRemoved.AddListener(RequestTrainHaltAtDeparture);
        }
        else
        {
            container.onPlaced.RemoveListener(EnableSystem);
            container.onRemoved.RemoveListener(RequestTrainHaltAtDeparture);
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
            if (barricadeForceDown)
            {
                barrState = BarricadeState.ToDown;
                barrTarget = barricadeDown.position;
            }
            else
            {
                barrState = BarricadeState.ToUp;
                barrTarget = barricadeUp.position;
            }
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

            trainState = TrainState.IdleAtDeparture;
            trainRunRequested = false;
            trainDir = +1; // arrancamos hacia adelante
            trainCurrentIdx = NextIndexFrom(trainDepIdx, +1);
        }
        else trainState = TrainState.IdleAtDeparture;
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

    // Elevator
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

        elevatorRb.MovePosition(next);
        return false;
    }

    // Barricade
    private void TickBarricade()
    {
        if (!barrConfigured) return;

        if (barricadeForceDown && barrState != BarricadeState.HoldDown)
        {
            barrState = BarricadeState.ToDown;
            barrTarget = barricadeDown.position;
        }

        switch (barrState)
        {
            case BarricadeState.ToDown:
                {
                    barrTarget = barricadeDown.position;
                    bool reached = MoveBodyLinear(barricadeRb, barrTarget, barricadeSpeed, barricadeArriveThreshold);
                    if (reached)
                    {
                        barrState = barricadeForceDown ? BarricadeState.HoldDown : BarricadeState.ToUp;
                    }
                    break;
                }
            case BarricadeState.ToUp:
                {
                    if (barricadeForceDown)
                    {
                        barrState = BarricadeState.ToDown;
                        break;
                    }

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
                break;
            case BarricadeState.HoldDown:
                break;
            case BarricadeState.Idle:
                break;
        }
    }

    private void RequestStartTrain()
    {
        if (!systemEnabled || !trainConfigured) return;
        trainRunRequested = true;
        onTrainStarted?.Invoke();
    }

    // Train (Loop / PingPong)
    private void TickTrain()
    {
        if (!trainConfigured) return;

        switch (trainState)
        {
            case TrainState.IdleAtDeparture:
                {
                    Vector3 depPos = trainWaypoints[trainDepIdx].position;
                    if (!IsNear(trainRb.position, depPos, trainArriveThreshold))
                        TeleportBodyTo(trainRb, depPos);

                    if (trainRunRequested)
                    {
                        trainDir = +1; // arrancamos hacia adelante
                        trainCurrentIdx = NextIndexFrom(trainDepIdx, trainDir);
                        trainState = TrainState.Running;
                    }
                    break;
                }

            case TrainState.Running:
                {
                    // Objetivo actual
                    Vector3 target = trainWaypoints[trainCurrentIdx].position;
                    bool reached = MoveBodyLinear(trainRb, target, trainSpeed, trainArriveThreshold);

                    if (reached)
                    {
                        // ¿Debemos frenar en departure?
                        if (trainHaltAtDeparture && trainCurrentIdx == trainDepIdx)
                        {
                            trainRunRequested = false;
                            onTrainStoppedAtDeparture?.Invoke();
                            trainHaltAtDeparture = false;
                            trainState = TrainState.IdleAtDeparture;
                            break;
                        }

                        // Avanzar al siguiente índice según el modo
                        if (trainPathMode == TrainPathMode.Loop)
                        {
                            int next = trainCurrentIdx + 1;
                            if (next > trainLastIdx)
                            {
                                // fin de lista -> espera y teletransporta al primero
                                trainWaitUntil = Time.fixedUnscaledTime + trainTeleportWaitSeconds;
                                trainState = TrainState.LoopTeleportWait;
                            }
                            else
                            {
                                trainCurrentIdx = next;
                            }
                        }
                        else // PingPong
                        {
                            int next = trainCurrentIdx + trainDir;

                            // ¿Llegó a un extremo?
                            if (next > trainLastIdx || next < trainFirstIdx)
                            {
                                // Invertimos la dirección
                                trainDir *= -1;

                                // Espera antes de volver (usa el mismo sistema de espera que el modo Loop)
                                trainWaitUntil = Time.fixedUnscaledTime + trainTeleportWaitSeconds;
                                trainState = TrainState.LoopTeleportWait;
                            }
                            else
                            {
                                trainCurrentIdx = next;
                            }
                        }
                    }
                    break;
                }

            case TrainState.LoopTeleportWait:
                {
                    if (Time.fixedUnscaledTime >= trainWaitUntil)
                    {
                        TeleportBodyTo(trainRb, trainWaypoints[trainFirstIdx].position);
                        trainCurrentIdx = trainFirstIdx;
                        // Si hay que frenar en departure y el primero es el departure, paramos.
                        if (trainHaltAtDeparture && trainDepIdx == trainFirstIdx)
                        {
                            trainRunRequested = false;
                            onTrainStoppedAtDeparture?.Invoke();
                            trainHaltAtDeparture = false;
                            trainState = TrainState.IdleAtDeparture;
                        }
                        else
                        {
                            // seguimos corriendo
                            int next = trainCurrentIdx + 1;
                            trainCurrentIdx = Mathf.Clamp(next, trainFirstIdx, trainLastIdx);
                            trainState = TrainState.Running;
                        }
                    }
                    break;
                }
        }
    }

    private int NextIndexFrom(int idx, int dir)
    {
        int next = idx + Mathf.Clamp(dir, -1, 1);
        return Mathf.Clamp(next, 0, (trainWaypoints.Count - 1));
    }

    // Helpers
    private bool MoveBodyLinear(Rigidbody rb, Vector3 targetPos, float speed, float arriveThreshold)
    {
        Vector3 toTarget = targetPos - rb.position;
        float dist = toTarget.magnitude;
        if (dist <= arriveThreshold)
        {
            rb.MovePosition(targetPos);
            return true;
        }

        float s = Mathf.Max(0f, speed);
        float step = s * Time.fixedDeltaTime;
        Vector3 dir = (dist > 1e-5f) ? (toTarget / dist) : Vector3.zero;
        Vector3 next = (step >= dist) ? targetPos : rb.position + dir * step;

        rb.MovePosition(next);
        return false;
    }

    private static bool IsNear(Vector3 a, Vector3 b, float eps) =>
        (a - b).sqrMagnitude <= eps * eps;

    private void TeleportBodyTo(Rigidbody rb, Vector3 pos)
    {
        if (rb == null) return;
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
