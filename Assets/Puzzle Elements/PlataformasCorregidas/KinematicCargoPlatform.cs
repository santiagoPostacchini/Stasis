using UnityEngine;
using UnityEngine.Events;

/// Mueve la plataforma del pistón arriba/abajo con perfil trapezoidal.
/// Ideal para un pistón donde la base es fija y la plataforma sube y baja.
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class KinematicCargoPlatform : MonoBehaviour
{
    public enum Mode { PingPong, Loop, Once }
    private enum Phase { Idle, Accel, Cruise, Decel, Dwell }

    [Header("Waypoints")]
    public Transform pointA;  // posición baja
    public Transform pointB;  // posición alta

    [Header("Movimiento")]
    public float cruiseSpeed = 2f;
    public float acceleration = 6f;
    public float dwellTime = 0.25f;
    public float arriveEpsilon = 0.005f;
    public Mode mode = Mode.PingPong;
    public bool autoStart = true;
    public bool startAtA = true;

    [Header("Eventos")]
    public UnityEvent onReachA;
    public UnityEvent onReachB;

    Rigidbody _rb;
    Phase _phase = Phase.Idle;
    Vector3 _from, _to, _dirN;
    float _distanceTotal, _travelled, _velocity, _tDwell;
    bool _headingUp;
    Phase _lastPhase;
    [SerializeField] private ElevatorShipmentTrain _elevatorShipmentTrain;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.isKinematic = true;
        _elevatorShipmentTrain = GetComponentInParent<ElevatorShipmentTrain>();
    }
    
    void OnEnable()
    {
        if (!pointA || !pointB)
        {
            Debug.LogError("[PlatformMoverTrapezoid] Asigna pointA/pointB.");
            enabled = false; return;
        }

        _rb.position = startAtA ? pointA.position : pointB.position;
        _headingUp = startAtA;
        PrepareSegment();
        if (autoStart) StartMove();
    }

    void PrepareSegment()
    {
        _from = _headingUp ? pointA.position : pointB.position;
        _to = _headingUp ? pointB.position : pointA.position;
        _dirN = (_to - _from).normalized;
        _distanceTotal = Vector3.Distance(_from, _to);
        _travelled = 0f;
        _velocity = 0f;
    }

    public void StartMove()
    {
        if (_distanceTotal > 0.001f) _phase = Phase.Accel;
    }
    public void StopMove()
    {
        _phase = Phase.Idle;
    }
    public void Desestasear()
    {
        _phase = _lastPhase;
        
    }

    public void stasear()
    {
        if (_elevatorShipmentTrain == null || _elevatorShipmentTrain.IsFreezed)
        {
            _lastPhase = _phase;
            _phase = Phase.Idle;
            
            return;
        }
        
    }
    public void ActivateKinematic()
    {
        _rb.isKinematic = true;
    }
    public void DesactivateKinematic()
    {
        _rb.isKinematic = false;
    }
    void FixedUpdate()
    {
        
        if (_phase == Phase.Idle) return;
        float dt = Time.fixedDeltaTime;

        float dAccel = (cruiseSpeed * cruiseSpeed) / (2f * Mathf.Max(acceleration, 1e-4f));
        float dDecel = dAccel;
        float dMin = dAccel + dDecel;

        bool triangular = _distanceTotal <= dMin;
        float vTarget = triangular ? Mathf.Sqrt(acceleration * _distanceTotal) : cruiseSpeed;

        switch (_phase)
        {
            case Phase.Accel:
                _velocity = Mathf.MoveTowards(_velocity, vTarget, acceleration * dt);
                Step(_velocity * dt);
                if (Reached()) { Arrive(); break; }
                if (triangular && _travelled >= _distanceTotal * 0.5f) _phase = Phase.Decel;
                else if (!triangular && _travelled >= dAccel) _phase = Phase.Cruise;
                break;

            case Phase.Cruise:
                _velocity = vTarget;
                float remaining = _distanceTotal - _travelled;
                if (remaining <= dDecel) { _phase = Phase.Decel; break; }
                Step(_velocity * dt);
                if (Reached()) Arrive();
                break;

            case Phase.Decel:
              
                remaining = _distanceTotal - _travelled;
                float vStop = Mathf.Sqrt(Mathf.Max(0f, 2f * acceleration * remaining));
                _velocity = Mathf.Min(_velocity, vStop);
                _velocity = Mathf.MoveTowards(_velocity, 0f, acceleration * dt);
                Step(_velocity * dt);
                if (remaining <= arriveEpsilon || _velocity <= 1e-3f) Arrive();
                break;

            case Phase.Dwell:
                _tDwell -= dt;
                if (_tDwell <= 0f)
                {
                    if (mode == Mode.Once && _headingUp) { _phase = Phase.Idle; break; }
                    if (mode == Mode.Loop)
                    {
                        _headingUp = true;
                        _rb.position = pointA.position;
                    }
                    else
                    {
                        _headingUp = !_headingUp;
                    }
                    PrepareSegment();
                    _phase = Phase.Accel;
                }
                break;
        }
    }

    void Step(float step)
    {
        float remaining = _distanceTotal - _travelled;
        step = Mathf.Min(step, remaining);
        _rb.MovePosition(_rb.position + _dirN * step);
        _travelled += step;
    }

    bool Reached() => (_distanceTotal - _travelled) <= arriveEpsilon;

    void Arrive()
    {
        _rb.MovePosition(_to);
        _phase = Phase.Dwell;
        _tDwell = dwellTime;
        _velocity = 0f;

        if (_headingUp) onReachB?.Invoke();
        else onReachA?.Invoke();
    }
}
