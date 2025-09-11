using System.Collections.Generic;
using Player.Scripts.MovementFSM.MVC;
using UnityEngine;

namespace Player.Scripts.MovementFSM
{
    public class S_Grounded : IState
    {
        private readonly FSM _fsm;
        private readonly Model _model;
        private readonly Transform _moveBasis;

        private readonly List<System.Func<float>> _speedOverrides = new List<System.Func<float>>();

        private Ray _moveCheckRay;

        private int _lastJumpFrame = -1000;
        private readonly float _moveCheckDist = 0.75f;
        private LayerMask _moveCheckMask;
        private bool _wasGrounded;

        private bool _isRunning, _isStopping, _inAir;

        public S_Grounded(FSM fsm, Model model, Transform camHolder)
        {
            _fsm = fsm;
            _model = model;
            _moveBasis = camHolder;
        }

        public void OnEnter()
        {
            _model.OnJump += OnJumpPressed;
            _wasGrounded = _model.IsGroundedNow();
            
            bool canFire =
                _model.landedPending &&
                (_model.lastAirTime   >= _model.minAirTime) &&
                (_model.lastFallSpeed <= _model.landVelThreshold) &&
                ((Time.time - _model.lastLandingTime) > _model.landEventCooldown);

            if (canFire)
            {
                _model.LandedEvent();
                _model.lastLandingTime = Time.time;
            }

            _model.landedPending = false;
        }

        public void OnUpdate()
        {
            HandleStoppingLogic();

            bool grounded = _model.IsGroundedNow();
            if (!grounded && _wasGrounded)
            {
                _model.lastLeftGroundTime = Time.time;
                _model.airEnteredFromGround = true;
                _fsm.ChangeState(FSM.States.Air);
                _wasGrounded = false;
                return;
            }
            _wasGrounded = grounded;
            
            var p = _model.probe;

            // VAULT: adelante + hay vault
            if (p.action == ParkourAction.Vault && _model.zAxis > 0.1f)
            {
                _fsm.ChangeState(FSM.States.Vault);
                return;
            }

            // CLIMB: adelante/jump + hay ledge
            if (p.action == ParkourAction.Climb &&
                (_model.zAxis > 0.1f || _model.jumpDownThisFrame))
            {
                _fsm.ChangeState(FSM.States.Climb);
            }
        }

        public void OnFixedUpdate()
        {
            if (_model.canMove)
            {
                HandleRunning();
                HandleMovement();
            }
            else
            {
                _model.rb.velocity = new Vector3(0, _model.rb.velocity.y, 0);
            }
        }

        public void OnExit()
        {
            _model.OnJump -= OnJumpPressed;
        }

        private void GetPlanarBasis(out Vector3 f, out Vector3 r)
        {
            Transform basis = _moveBasis ? _moveBasis : _model.transform;
            f = basis.forward;
            f.y = 0f;
            f = f.sqrMagnitude > 0f ? f.normalized : Vector3.forward;
            r = basis.right;
            r.y = 0f;
            r = r.sqrMagnitude > 0f ? r.normalized : Vector3.right;
        }

        private void HandleMovement()
        {
            if (_model.canMove)
            {
                float targetSpeed = _model.runningKeyPressed ? _model.runningSpeed : _model.walkingSpeed;
                if (_speedOverrides.Count > 0)
                {
                    targetSpeed = _speedOverrides[^1]();
                }

                Vector2 inputDirection = new Vector2(_model.xAxis, _model.zAxis);
                if (inputDirection.magnitude > 1f) inputDirection.Normalize();

                GetPlanarBasis(out var f, out var r);
                Vector3 moveDir = (r * inputDirection.x + f * inputDirection.y);

                if (inputDirection.magnitude > 0 && !IsBlocked(moveDir))
                {
                    ApplyAcceleration(moveDir, targetSpeed);
                }
                else
                {
                    ApplyDeceleration();
                }

                ClampVelocity(targetSpeed);
            }
        }

        private void ApplyAcceleration(Vector3 direction, float targetSpeed)
        {
            Vector3 targetVelocity = direction * targetSpeed;
            Vector3 velocityChange = targetVelocity - new Vector3(_model.rb.velocity.x, 0, _model.rb.velocity.z);
            velocityChange = Vector3.ClampMagnitude(velocityChange, _model.acceleration * Time.fixedDeltaTime);
            _model.rb.AddForce(velocityChange, ForceMode.VelocityChange);
        }

        private void ApplyDeceleration()
        {
            Vector3 horizontalVelocity = new Vector3(_model.rb.velocity.x, 0, _model.rb.velocity.z);
            Vector3 decelerationForce = -horizontalVelocity * (_model.deceleration * Time.fixedDeltaTime);
            _model.rb.AddForce(decelerationForce, ForceMode.VelocityChange);

            if (horizontalVelocity.magnitude < 0.1f)
            {
                _model.rb.velocity = new Vector3(0, _model.rb.velocity.y, 0);
            }
        }

        private void ClampVelocity(float maxSpeed)
        {
            Vector3 clampedVelocity =
                Vector3.ClampMagnitude(new Vector3(_model.rb.velocity.x, 0, _model.rb.velocity.z), maxSpeed);
            _model.rb.velocity = new Vector3(clampedVelocity.x, _model.rb.velocity.y, clampedVelocity.z);
        }

        private void HandleRunning()
        {
            if (_model.canMove)
            {
                _isRunning = _model.canRun && _model.runningKeyPressed &&
                             (Mathf.Abs(_model.xAxis) > 0.1f || Mathf.Abs(_model.zAxis) > 0.1f);
            }

            _model.UpdateIsRunning(_isRunning);
        }

        private bool IsBlocked(Vector3 moveDir)
        {
            Vector3 origin = _model.transform.position + Vector3.up * 0.1f;
            _moveCheckRay = new Ray(origin, moveDir);
            return Physics.Raycast(_moveCheckRay, _moveCheckDist, _moveCheckMask);
        }

        private void HandleStoppingLogic()
        {
            float rawMag = new Vector2(_model.rawX, _model.rawZ).magnitude;

            bool inputIsZero = rawMag < _model.stopThreshold;
            bool hadInputBefore = _model.wasMovingByInput;
            bool hasInputNow = rawMag > _model.moveThreshold;

            if (hadInputBefore && inputIsZero && _isStopping)
            {
                _isStopping = true;
                _model.stopTimer = _model.stopCooldown;
            }

            if (_isStopping)
            {
                _model.stopTimer -= Time.deltaTime;
                if (_model.stopTimer <= 0f)
                {
                    _isStopping = false;
                }
            }

            _model.wasMovingByInput = hasInputNow;

            _model.UpdateStopping(_isStopping);
        }

        private void OnJumpPressed()
        {
            _model.BufferJumpNow();
            if (!_model.canMove) return;
            if (!_model.IsGroundedNow()) return;

            PerformJumpAndGoAir();
        }

        private void PerformJumpAndGoAir()
        {
            if (Time.frameCount == _lastJumpFrame) return;
            _lastJumpFrame = Time.frameCount;

            float g = Physics.gravity.y;
            float h = Mathf.Max(0.01f, _model.jumpHeight);
            float jumpVel = Mathf.Sqrt(2f * Mathf.Abs(g) * h);

            var v = _model.rb.velocity;
            v.y = 0f;
            _model.rb.velocity = v;

            _model.rb.AddForce(Vector3.up * jumpVel, ForceMode.VelocityChange);

            _model.airEnteredFromGround = false;
            _fsm.ChangeState(FSM.States.Air);
        }
    }
}