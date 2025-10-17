using Player.Scripts.MovementFSM.MVC;
using UnityEngine;

namespace Player.Scripts.MovementFSM
{
    public class S_Climb : IState
    {
        private readonly FSM _fsm;
        private readonly Model _m;

        Rigidbody _rb;
        Transform _orient;
        CapsuleCollider _capsule;

        bool _climbing;
        bool _hasFront;
        RaycastHit _frontHit;

        Vector3 _wallNormal;
        Vector3 _planarNormal;
        Vector3 _lastHitPoint;

        [Header("Detección (tags)")] private readonly float _climbDetectLength = 0.9f;
        private readonly float _climbSphereRadius = 0.35f;
        [Range(0, 85)] private readonly float _climbMaxLookAngle = 40f;
        [Range(0, 89)] private readonly float _climbMinWallSlopeDeg = 70f;

        [Header("Velocidades")] private readonly float _upSpeed = 3.0f;
        private readonly float _downSpeed = 3.0f;
        private readonly float _slideSpeed = 0.02f;

        [Header("Adherencia (XZ)")] private readonly float _standOff = 0.18f;
        private readonly float _stickKp = 220f;
        private readonly float _stickKd = 16f;
        private readonly float _maxIntoSpeedCancelPerFixed = 10f;

        private readonly float _maxPosCorrPerFixed = 0.04f;

        [Header("Debug")] private readonly bool _drawGizmos = false;

        public S_Climb(FSM fsm, Model model)
        {
            _fsm = fsm;
            _m = model;
        }

        // ReSharper disable Unity.PerformanceAnalysis
        public void OnEnter()
        {
            _rb = _m.rb;
            _orient = _m.cameraHolderTransform ? _m.cameraHolderTransform : _m.transform;
            _capsule = _m.GetComponent<CapsuleCollider>();

            _climbing = true;
            _m.canMove = false;
            _rb.useGravity = false;
            _rb.velocity = new Vector3(_rb.velocity.x, 0f, _rb.velocity.z); // limpiá vel.Y al entrar

            WallCheck(true); // primer sample estable

            //_m.OnClimbStart?.Invoke();
        }

        public void OnUpdate()
        {
            WallCheck(false);
            if (!_hasFront)
            {
                ExitToNext();
                return;
            }

            // Salto estilo wallrun
            if (_m.jumpDownThisFrame)
            {
                ClimbJump();
            }
        }

        public void OnFixedUpdate()
        {
            if (!_climbing || !_hasFront) return;

            float inputZ = Mathf.Abs(_m.rawZ) > 0.01f ? _m.rawZ : _m.zAxis;
            bool wantUp = (inputZ > 0.1f) || Input.GetKey(KeyCode.W);
            bool wantDown = (inputZ < -0.1f) || Input.GetKey(KeyCode.S) || Input.GetKey(_m.crouchKey);

            float vy = wantUp ? _upSpeed
                : wantDown ? -_downSpeed
                : -_slideSpeed;

            Vector3 v = _rb.velocity;
            Vector3 planarN = _planarNormal;
            float into = Vector3.Dot(v, -planarN);
            if (into > 0f)
            {
                float remove = Mathf.Min(into, _maxIntoSpeedCancelPerFixed * Time.fixedDeltaTime);
                v -= planarN * remove;
            }

            v.y = vy;
            _rb.velocity = v;

            Vector3 target = _lastHitPoint + planarN * _standOff;
            Vector3 delta = target - _rb.position;
            delta.y = 0f;

            float maxStep = _maxPosCorrPerFixed;
            if (delta.sqrMagnitude > (maxStep * maxStep))
                delta = delta.normalized * maxStep;

            if (delta.sqrMagnitude > 1e-8f)
                _rb.MovePosition(_rb.position + delta);
        }

        void ClimbJump()
        {
            _m.blockClimbUntil = Time.time + _m.climbRegrabCooldown;
            _m.lastWallDetachTime = Time.time;

            Vector3 impulse = Vector3.up * _m.wallJumpUpForce + _wallNormal * _m.wallJumpSideForce;

            Vector3 v = _rb.velocity;
            float into = Vector3.Dot(v, -_planarNormal);
            if (into > 0f) v -= _planarNormal * into;
            v.y = 0f;
            _rb.velocity = v;

            _rb.AddForce(impulse, ForceMode.Impulse);

            ExitToNext();
        }

        public void OnExit()
        {
            _climbing = false;
            _m.canMove = true;
            _rb.useGravity = true;

            _m.blockClimbUntil = Time.time + _m.climbRegrabCooldown;
            //_m.OnClimbEnd?.Invoke();
        }

        public void OnLateUpdate()
        {
        }
        
        void WallCheck(bool forceFirst)
        {
            _hasFront = false;

            float h = _capsule ? _capsule.height : 1.8f;
            float chest = Mathf.Clamp(h * 0.55f, 0.8f, 1.1f);
            Vector3 origin = _m.transform.position + Vector3.up * chest;

            Vector3 fwd = _orient ? _orient.forward : _m.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-6f) fwd = _m.transform.forward;
            fwd.Normalize();

            // SphereCast al frente contra cualquier cosa y filtramos por TAG
            if (Physics.SphereCast(origin, _climbSphereRadius, fwd, out _frontHit,
                    _climbDetectLength, ~0, QueryTriggerInteraction.Ignore))
            {
                if (_frontHit.collider && _frontHit.collider.CompareTag(_m.tagClimb))
                {
                    // Slope ≈ pared (>=70°)
                    float slopeDeg = Vector3.Angle(_frontHit.normal, Vector3.up);
                    if (slopeDeg >= _climbMinWallSlopeDeg)
                    {
                        float lookAng = Vector3.Angle(fwd, -_frontHit.normal);
                        if (lookAng <= _climbMaxLookAngle)
                        {
                            _hasFront = true;
                            _wallNormal = _frontHit.normal.normalized;

                            // normal planar para adherencia (sin componente Y)
                            _planarNormal = Vector3.ProjectOnPlane(_wallNormal, Vector3.up);
                            if (_planarNormal.sqrMagnitude < 1e-6f)
                                _planarNormal = new Vector3(_wallNormal.x, 0f, _wallNormal.z);
                            _planarNormal.Normalize();

                            _lastHitPoint = _frontHit.point;

                            if (_drawGizmos)
                            {
                                Debug.DrawRay(_frontHit.point, _wallNormal * 0.4f, Color.cyan, 0.02f, false);
                                Debug.DrawRay(_frontHit.point, _planarNormal * 0.4f, Color.yellow, 0.02f, false);
                            }
                        }
                    }
                }
            }

            if (!_hasFront && !forceFirst)
            {
                // pequeño refinamiento: si perdiste pared por un frame, probá con overlap
                Vector3 ahead = origin + fwd * Mathf.Max(0.05f, _climbSphereRadius * 0.5f);
                var cols = Physics.OverlapSphere(ahead, _climbSphereRadius * 0.9f, ~0, QueryTriggerInteraction.Ignore);
                foreach (var c in cols)
                {
                    if (!c || !c.CompareTag(_m.tagClimb)) continue;
                    Vector3 p = c.ClosestPoint(origin);
                    Vector3 n = (origin - p).sqrMagnitude > 1e-6f ? (origin - p).normalized : -fwd;

                    float slopeDeg = Vector3.Angle(n, Vector3.up);
                    float lookAng = Vector3.Angle(fwd, -n);

                    if (slopeDeg >= _climbMinWallSlopeDeg && lookAng <= _climbMaxLookAngle)
                    {
                        _hasFront = true;
                        _wallNormal = n;
                        _planarNormal = Vector3.ProjectOnPlane(n, Vector3.up).normalized;
                        _lastHitPoint = p;
                        break;
                    }
                }
            }
        }

        void ExitToNext()
        {
            _fsm.ChangeState(_m.IsGroundedNow() ? FSM.States.Grounded : FSM.States.Air);
        }
    }
}