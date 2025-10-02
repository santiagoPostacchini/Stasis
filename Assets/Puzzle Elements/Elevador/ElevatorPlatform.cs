using System.Collections;
using UnityEngine;

/// <summary>
/// Elevator platform that moves between two waypoints (auto-detects low/high by Y),
/// with smooth acceleration/deceleration, optional wait at ends, and runtime speed changes.
/// Exactly the same behavior as in your TrainSystem elevator, but isolated here.
/// </summary>
public class ElevatorPlatform : MonoBehaviour
{
    [Header("Elevator (Rigidbody)")]
    public Rigidbody elevatorRb;
    public Transform elevatorWP1;
    public Transform elevatorWP2;

    [Tooltip("Max travel speed (units/sec). You can change this at runtime.")]
    public float elevatorSpeed = 2f;

    [Tooltip("Acceleration used to ramp up/down (units/sec^2).")]
    public float elevatorAcceleration = 20f;

    [Tooltip("Seconds to wait at each end.")]
    public float elevatorWaitSeconds = 2f;

    [Tooltip("How close is considered 'arrived' to a waypoint.")]
    public float elevatorArriveThreshold = 0.02f;

    [Tooltip("If true, starts moving automatically on Enable.")]
    public bool autoStart = true;

    // Internal state
    private Transform elevatorLow;
    private Transform elevatorHigh;

    private Coroutine elevatorCo;
    private bool isEnabledFlag;
    private float elevatorCurrentSpeed = 0f;

    // -------------- Unity lifecycle --------------
    void Awake()
    {
        ResolveHeights();
    }

    void OnEnable()
    {
        if (autoStart) StartElevator();
    }

    void OnDisable()
    {
        StopElevator();
    }

    // -------------- Public API --------------
    public void StartElevator()
    {
        isEnabledFlag = true;

        ResolveHeights();

        if (elevatorCo != null) StopCoroutine(elevatorCo);
        if (IsElevatorConfigured())
            elevatorCo = StartCoroutine(ElevatorLoop());
    }

    public void StopElevator()
    {
        isEnabledFlag = false;

        if (elevatorCo != null)
        {
            StopCoroutine(elevatorCo);
            elevatorCo = null;
        }

        if (elevatorRb != null)
        {
            elevatorRb.velocity = Vector3.zero;
        }

        elevatorCurrentSpeed = 0f;
    }

    // -------------- Setup helpers --------------
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

    private bool IsElevatorConfigured()
    {
        return elevatorRb != null && elevatorLow != null && elevatorHigh != null;
    }

    // -------------- Main loop --------------
    private IEnumerator ElevatorLoop()
    {
        if (!IsElevatorConfigured()) yield break;

        float dLow = Vector3.Distance(elevatorRb.position, elevatorLow.position);
        float dHigh = Vector3.Distance(elevatorRb.position, elevatorHigh.position);
        Transform next = (dLow <= dHigh) ? elevatorHigh : elevatorLow;

        while (isEnabledFlag && IsElevatorConfigured())
        {
            Transform a = next;
            Transform b = (a == elevatorLow) ? elevatorHigh : elevatorLow;

            // Move to 'a' with acceleration ramp
            yield return MoveElevatorWithAccel(a.position);

            // Smooth stop and wait
            yield return DecelerateElevatorToZero();
            yield return WaitSecondsRealtime(elevatorWaitSeconds);
            if (!isEnabledFlag || !IsElevatorConfigured()) break;

            // Move to 'b' with acceleration ramp
            yield return MoveElevatorWithAccel(b.position);

            // Smooth stop and wait
            yield return DecelerateElevatorToZero();
            yield return WaitSecondsRealtime(elevatorWaitSeconds);
            if (!isEnabledFlag || !IsElevatorConfigured()) break;

            next = a; // ping-pong
        }

        if (elevatorRb != null)
        {
            elevatorRb.velocity = Vector3.zero;
        }
        elevatorCurrentSpeed = 0f;
    }

    // -------------- Motion helpers --------------
    private IEnumerator MoveElevatorWithAccel(Vector3 targetPos)
    {
        if (elevatorRb == null) yield break;
        WaitForFixedUpdate wait = new WaitForFixedUpdate();

        while (!IsNear(elevatorRb.position, targetPos, elevatorArriveThreshold))
        {
            float maxSpeed = Mathf.Max(0f, elevatorSpeed);
            float accel = Mathf.Max(0f, elevatorAcceleration);

            Vector3 toTarget = targetPos - elevatorRb.position;
            float distance = toTarget.magnitude;
            Vector3 dir = (distance > 1e-5f) ? toTarget / distance : Vector3.zero;

            // Ensure we can stop at the target: v <= sqrt(2*a*d)
            float maxSpeedForStop = Mathf.Sqrt(Mathf.Max(0f, 2f * accel * distance));
            float desiredSpeed = Mathf.Min(maxSpeed, maxSpeedForStop);

            // Smoothly adjust current speed by accel
            elevatorCurrentSpeed = Mathf.MoveTowards(elevatorCurrentSpeed, desiredSpeed, accel * Time.fixedDeltaTime);

            if (elevatorCurrentSpeed <= 1e-4f)
            {
                elevatorRb.velocity = Vector3.zero;
                yield return wait;
                continue;
            }

            float step = elevatorCurrentSpeed * Time.fixedDeltaTime;
            Vector3 nextPos = (step >= distance) ? targetPos : elevatorRb.position + dir * step;

            // Publish kinematic "velocity" for more stable contacts
            elevatorRb.velocity = (nextPos - elevatorRb.position) / Time.fixedDeltaTime;

            elevatorRb.MovePosition(nextPos);
            yield return wait;
        }

        elevatorRb.MovePosition(targetPos);
        elevatorRb.velocity = Vector3.zero;
    }

    private IEnumerator DecelerateElevatorToZero()
    {
        if (elevatorRb == null) yield break;
        WaitForFixedUpdate wait = new WaitForFixedUpdate();

        float accel = Mathf.Max(0f, elevatorAcceleration);
        while (elevatorCurrentSpeed > 1e-3f)
        {
            elevatorCurrentSpeed = Mathf.MoveTowards(elevatorCurrentSpeed, 0f, accel * Time.fixedDeltaTime);
            elevatorRb.velocity = Vector3.zero; // no extra push while stopping at end
            yield return wait;
        }

        elevatorCurrentSpeed = 0f;
        elevatorRb.velocity = Vector3.zero;
    }

    // -------------- Utility --------------
    private static bool IsNear(Vector3 a, Vector3 b, float eps)
    {
        return (a - b).sqrMagnitude <= eps * eps;
    }
    //
    private static IEnumerator WaitSecondsRealtime(float seconds)
    {
        float end = Time.unscaledTime + seconds;
        while (Time.unscaledTime < end) yield return null;
        
    }

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
