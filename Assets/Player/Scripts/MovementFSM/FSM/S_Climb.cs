using Player.Scripts.MovementFSM.MVC;
using UnityEngine;

namespace Player.Scripts.MovementFSM
{
    public class S_Climb : IState
    {
        // ======================
        // 1) Referencias
        // ======================
        private readonly FSM _fsm;
        private readonly Model _m;

        private Rigidbody _rb;
        private CapsuleCollider _capsule;
        private Transform _orient; // cámara si existe; si no, el transform del player
        private ParkourScanner _scan;

        // ======================
        // 2) Máscaras / entorno (cargadas en OnEnter desde el scanner)
        // ======================
        private LayerMask _maskWall; // paredes trepables (climbMask o environment si no hay)
        private LayerMask _maskTopAndClear; // tapa y clearance = climbMask | environment | ground
        private float _standForward; // cuánto avanzar al pararse en la tapa

        // ======================
        // 3) Tuning de movimiento en pared
        // ======================
        private readonly float _climbSpeed = 2.8f; // vel. vertical al trepar (m/s)
        private readonly float _maxLookAngleDeg = 35f; // ángulo máx entre forward y -normal para permitir
        private readonly float _minNormalChangeDeg = 10f; // reset temporizador si cambia mucho la normal
        private readonly float _detectLength = 0.8f; // alcance del ray/sphere hacia delante
        private readonly float _sphereCastRadius = 0.35f; // radio para detectar pared “gruesa”
        private const float ForwardInputThreshold = 0.1f;

        // ======================
        // 4) Climb jump (impulso de salida)
        // ======================
        private readonly float _climbJumpUpForce = 7.5f;
        private readonly float _climbJumpBackForce = 5.0f;
        private readonly int _climbJumpsMax = 1;
        private int _climbJumpsLeft;

        // ======================
        // 5) Auto-mantle (arrastre físico, sin MovePosition)
        // ======================
        private bool _mantling;
        private Vector3 _mantleTarget;
        private float _mantleTimer;
        private readonly float _mantleDur = 0.22f; // segundos
        private readonly float _mantleKp = 80f;
        private readonly float _mantleKd = 14f;
        private readonly float _mantleMaxAccel = 180f;
        private readonly float _ledgeLookUp = 0.9f;
        private readonly float _ledgeDownCast = 2.5f;
        private readonly float _autoMantleGap = 0.35f;
        private readonly float _clearanceSkin = 0.06f;

        // ======================
        // 6) Runtime state
        // ======================
        private bool _climbing;
        private bool _exitingWall;
        private float _exitWallTime = 0.25f;
        private float _exitTimer;

        private bool _wantsForward;

        private bool _wallFront;
        private float _wallLookAngle;
        private RaycastHit _frontWallHit;

        private Transform _lastWall;
        private Vector3 _lastWallNormal;

        // ======================
        // 7) Cache / flags físicos
        // ======================
        private bool _oldUseGravity;
        private RigidbodyInterpolation _oldInterp;

        // ------------------------------------------------------

        public S_Climb(FSM fsm, Model model)
        {
            _fsm = fsm;
            _m = model;
        }

        // ======================
        // Lifecycle
        // ======================
        public void OnEnter()
        {
            _rb = _m.rb;
            _capsule = _m.GetComponent<CapsuleCollider>();
            _orient = _m.cameraHolderTransform ? _m.cameraHolderTransform : _m.transform;
            _scan = _m.GetComponent<ParkourScanner>();

            _oldUseGravity = _rb.useGravity;
            _oldInterp = _rb.interpolation;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;

            // máscaras coherentes
            var climbMask = (_scan && _scan.climbMask.value != 0) ? _scan.climbMask : _scan.environmentMask;
            _maskWall = climbMask;
            _maskTopAndClear = climbMask | (_scan ? _scan.environmentMask : 0) | (_scan ? _scan.groundMask : 0);
            _standForward = _scan ? _scan.climbStandForward : 0.35f;

            _climbing = false;
            _exitingWall = false;
            _mantling = false;
            _mantleTimer = 0f;
            _climbJumpsLeft = _climbJumpsMax;
            _exitTimer = 0f;

            _lastWall = null;
            _lastWallNormal = Vector3.zero;

            _m.canMove = false; // bloquea locomoción normal
        }

        public void OnUpdate()
        {
            _wantsForward = _m.zAxis > ForwardInputThreshold;

            WallCheck();

            // FSM local: trepar mientras hay pared de frente, input y mirada correcta
            if (_wallFront && _wantsForward && _wallLookAngle < _maxLookAngleDeg && !_exitingWall && !_mantling)
            {
                if (!_climbing) StartClimbing();
            }
            else if (!_mantling)
            {
                if (_climbing) StopClimbing();
            }

            // Salto de trepa
            if (_wallFront && _m.jumpDownThisFrame && _climbJumpsLeft > 0 && !_mantling)
                ClimbJump();

            // Auto-mantle si estamos muy cerca de la tapa
            if (_climbing && !_exitingWall && !_mantling && TryAutoMantle(out var stand))
                BeginMantle(stand);

            // Si no estamos ni trepando ni mantling ni en “exit”, salimos del estado
            if (!_climbing && !_mantling && !_exitingWall)
            {
                _fsm.ChangeState(_m.IsGroundedNow() ? FSM.States.Grounded : FSM.States.Air);
            }
        }

        public void OnFixedUpdate()
        {
            if (_mantling)
            {
                // Arrastre PD físico hacia el target
                _rb.useGravity = true;

                _mantleTimer = Mathf.Min(_mantleDur, _mantleTimer + Time.fixedDeltaTime);

                Vector3 posErr = _mantleTarget - _rb.position;
                Vector3 velErr = -_rb.velocity;
                Vector3 accel = _mantleKp * posErr + _mantleKd * velErr;

                float aMag = accel.magnitude;
                if (aMag > _mantleMaxAccel) accel *= (_mantleMaxAccel / aMag);

                _rb.AddForce(accel, ForceMode.Acceleration);

                bool closeXY = new Vector2(posErr.x, posErr.z).sqrMagnitude < (0.04f * 0.04f);
                bool closeY = Mathf.Abs(posErr.y) < 0.04f;
                if ((closeXY && closeY) || (_mantleTimer >= _mantleDur))
                {
                    EndMantle();
                }

                return;
            }

            if (_climbing && !_exitingWall)
            {
                _rb.useGravity = false;
                Vector3 v = _rb.velocity;
                v.y = _climbSpeed;
                _rb.velocity = v;
            }
            else
            {
                _rb.useGravity = _oldUseGravity;
            }
        }

        public void OnExit()
        {
            _rb.useGravity = _oldUseGravity;
            _rb.interpolation = _oldInterp;

            _m.canMove = true;
            _m.blockClimbUntil = Time.time + _m.climbRegrabCooldown;
        }

        public void OnLateUpdate()
        {
            throw new System.NotImplementedException();
        }

        // ======================
        // Detección pared frontal
        // ======================
        private void WallCheck()
        {
            _wallFront = false;
            _wallLookAngle = 180f;

            float r = _capsule ? _capsule.radius : 0.3f;
            float h = _capsule ? _capsule.height : 1.8f;
            float chest = Mathf.Clamp(h * 0.55f, 0.8f, 1.1f);

            Vector3 origin = _m.transform.position + Vector3.up * chest;
            Vector3 fwd = _orient ? _orient.forward : _m.transform.forward;

            // SphereCast más tolerante que un Raycast
            if (Physics.SphereCast(origin, _sphereCastRadius, fwd, out _frontWallHit,
                    _detectLength, _maskWall, QueryTriggerInteraction.Ignore))
            {
                _wallFront = true;
                _wallLookAngle = Vector3.Angle(fwd, -_frontWallHit.normal);

                bool newWall = _frontWallHit.transform != _lastWall
                               || Mathf.Abs(Vector3.Angle(_lastWallNormal, _frontWallHit.normal)) > _minNormalChangeDeg;

                if ((_wallFront && newWall) || _m.IsGroundedNow())
                {
                    _climbJumpsLeft = _climbJumpsMax;
                    _lastWall = _frontWallHit.transform;
                    _lastWallNormal = _frontWallHit.normal;
                }
            }
        }

        // ======================
        // Auto-mantle (sin MovePosition)
        // ======================
        private bool TryAutoMantle(out Vector3 standPoint)
        {
            standPoint = default;

            float r = _capsule ? _capsule.radius : 0.3f;
            float h = _capsule ? _capsule.height : 1.8f;

            Vector3 fwd = _orient ? _orient.forward : _m.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-6f) fwd = _m.transform.forward;
            fwd.Normalize();

            float chest = Mathf.Clamp(h * 0.55f, 0.8f, 1.1f);

            Vector3 handPos = _m.transform.position
                              + Vector3.up * (chest + _ledgeLookUp)
                              + fwd * (r + 0.25f);

            if (Physics.Raycast(handPos, Vector3.down, out var down, _ledgeDownCast,
                    _maskTopAndClear, QueryTriggerInteraction.Ignore))
            {
                float gap = handPos.y - down.point.y;
                if (gap <= _autoMantleGap)
                {
                    Vector3 stand = down.point
                                    + fwd * Mathf.Max(0.05f, _standForward)
                                    + Vector3.up * (r + _clearanceSkin);

                    if (HasClearanceCapsule(stand, h - r * 2f, _maskTopAndClear))
                    {
                        standPoint = stand;
                        return true;
                    }
                }
            }

            return false;
        }

        private void BeginMantle(Vector3 stand)
        {
            _climbing = false;
            _mantling = true;
            _mantleTarget = stand;
            _mantleTimer = 0f;

            // limpiamos Y pero mantenemos horizontal
            Vector3 v = _rb.velocity;
            v.y = 0f;
            _rb.velocity = v;
        }

        private void EndMantle()
        {
            _mantling = false;

            // soltamos (no pisamos vel.y)
            Vector3 v = _rb.velocity;
            Vector3 hz = new Vector3(v.x, 0f, v.z);
            float keep = Mathf.Max(hz.magnitude, _m.walkingSpeed * 0.5f);
            Vector3 fwd = _orient ? _orient.forward : _m.transform.forward;
            fwd.y = 0f;
            fwd.Normalize();
            if (keep > 0.01f)
            {
                v.x = fwd.x * keep;
                v.z = fwd.z * keep;
                _rb.velocity = v;
            }

            _m.blockClimbUntil = Time.time + _m.climbRegrabCooldown;
            _m.canMove = true;
            _fsm.ChangeState(FSM.States.Grounded);
        }

        // ======================
        // Acciones
        // ======================
        private void StartClimbing()
        {
            _climbing = true;
            Vector3 v = _rb.velocity;
            v.y = 0f;
            _rb.velocity = v;
        }

        private void StopClimbing()
        {
            _climbing = false;
        }

        private void ClimbJump()
        {
            _exitingWall = true;
            _exitTimer = _exitWallTime;

            Vector3 impulse = Vector3.up * _climbJumpUpForce + _frontWallHit.normal * _climbJumpBackForce;

            Vector3 v = _rb.velocity;
            v.y = 0f;
            _rb.velocity = v;
            _rb.AddForce(impulse, ForceMode.Impulse);

            _climbJumpsLeft--;

            _fsm.ChangeState(FSM.States.Air);
        }

        // ======================
        // Util: clearance con máscara
        // ======================
        private bool HasClearanceCapsule(Vector3 center, float heightSegment, LayerMask mask)
        {
            float r = (_capsule ? _capsule.radius : 0.3f) - _clearanceSkin * 0.5f;
            float h = Mathf.Max(r * 2f + 0.01f, heightSegment);
            float half = h * 0.5f - r;

            Vector3 top = center + Vector3.up * half;
            Vector3 bottom = center - Vector3.up * half;

            return !Physics.CheckCapsule(top, bottom, r, mask, QueryTriggerInteraction.Ignore);
        }
    }
}