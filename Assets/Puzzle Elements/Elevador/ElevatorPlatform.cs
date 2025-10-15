using UnityEngine;

public class ElevatorPlatform : MonoBehaviour
{
    [Header("Elevator (Rigidbody)")]
    public Rigidbody elevatorRb;
    public Transform elevatorWP1;
    public Transform elevatorWP2;

    [Tooltip("Max travel speed (units/sec).")]
    public float elevatorSpeed = 2f;

    [Tooltip("Acceleration used to ramp up/down (units/sec^2).")]
    public float elevatorAcceleration = 20f;

    [Tooltip("Seconds to wait at each end.")]
    public float elevatorWaitSeconds = 2f;

    [Tooltip("How close is considered 'arrived' to a waypoint.")]
    public float elevatorArriveThreshold = 0.02f;

    [Tooltip("If true, starts moving automatically on Enable.")]
    public bool autoStart = true;

    // Waypoints resueltos
    private Transform elevatorLow;
    private Transform elevatorHigh;

    // Estado (id�ntico al del TrainSystem)
    private enum ElevatorState { Idle, ToA, WaitAtA, ToB, WaitAtB }
    private ElevatorState elevState = ElevatorState.Idle;
    private Vector3 elevTarget;
    private float elevCurrentSpeed;
    private float elevWaitUntil;
    private bool elevConfigured;
    private bool systemEnabled;

    void Awake()
    {
        ResolveHeights();
        PrepareElevator();
    }

    void OnEnable()
    {
        ResolveHeights();
        PrepareElevator();
        if (autoStart) StartElevator();
    }

    void OnDisable()
    {
        StopElevator();
    }

    public void StartElevator()
    {
        systemEnabled = true;
        ResolveHeights();
        PrepareElevator(true);
    }

    public void StopElevator()
    {
        systemEnabled = false;
        if (elevatorRb)
        {
            elevatorRb.velocity = Vector3.zero;
            elevatorRb.angularVelocity = Vector3.zero;
        }
        elevState = ElevatorState.Idle;
    }

    void FixedUpdate()
    {
        elevConfigured = IsElevatorConfigured();

        if (!systemEnabled)
        {
            if (elevatorRb)
            {

                elevatorRb.isKinematic = false;

            elevatorRb.velocity = Vector3.zero;
                elevatorRb.angularVelocity = Vector3.zero;
            }
            return;
        }

        TickElevator();
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
    }

    private bool IsElevatorConfigured() =>
        elevatorRb != null && elevatorLow != null && elevatorHigh != null;

    private void PrepareElevator(bool snapIfNeeded = false)
    {
        elevCurrentSpeed = 0f;

        if (IsElevatorConfigured())
        {
            Vector3 p = elevatorRb.position;
            float dLow = Vector3.Distance(p, elevatorLow.position);
            float dHigh = Vector3.Distance(p, elevatorHigh.position);

            Transform first = (dLow <= dHigh) ? elevatorHigh : elevatorLow;
            elevTarget = first.position;
            elevState = ElevatorState.ToA;
            elevWaitUntil = 0f;

            if (snapIfNeeded)
            {
                // no teleporta salvo que quieras alinear al arranque
                // ac� dejamos la posici�n actual
            }
        }
        else
        {
            elevState = ElevatorState.Idle;
        }
    }

    // --------- L�gica (id�ntica a TrainSystem) ---------
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

        // v_max para poder detenerse a tiempo: v <= sqrt(2*a*d)
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

    private static bool IsNear(Vector3 a, Vector3 b, float eps) =>
        (a - b).sqrMagnitude <= eps * eps;

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (elevatorWP1 != null && elevatorWP2 != null)
        {
            Transform low = (elevatorWP1.position.y <= elevatorWP2.position.y) ? elevatorWP1 : elevatorWP2;
            Transform high = (low == elevatorWP1) ? elevatorWP2 : elevatorWP1;

            Gizmos.color = Color.cyan;   // low
            Gizmos.DrawSphere(low.position, 0.08f);

            Gizmos.color = Color.magenta; // high
            Gizmos.DrawCube(high.position, Vector3.one * 0.14f);
        }
    }
#endif
}
