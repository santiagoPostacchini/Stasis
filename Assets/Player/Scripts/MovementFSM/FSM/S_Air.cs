using Player.Scripts.MovementFSM.MVC;
using UnityEngine;

namespace Player.Scripts.MovementFSM
{
    public class S_Air : IState
    {
        private readonly FSM _fsm;
        private readonly Model _model;
        private readonly Transform _moveBasis;

        private bool _enteredFromGround;
        private float _airTime;

        public S_Air(FSM fsm, Model model, Transform camHolder)
        {
            _fsm = fsm;
            _model = model;
            _moveBasis = camHolder;
        }

        public void OnEnter()
        {
            _model.OnJump += OnJumpPressed;
            _model.airEnteredFromGround = false;
            _model.ClearJumpBuffer();
            _airTime = 0f;
        }

        public void OnUpdate()
        {
            _airTime += Time.deltaTime;
            
            TryConsumeCoyoteOrBuffer();
            
            var p = _model.probe;
            if (p.action == ParkourAction.WallrunLeft ||
                p.action == ParkourAction.WallrunRight)
            {
                _model.ClearJumpBuffer();
                _fsm.ChangeState(FSM.States.Wallrun);
                return;
            }

            // 3) Aterrizar via Scanner
            if (_model.IsGroundedNow())
            {
                bool shouldLand = (_airTime >= _model.minAirTime) &&
                                  (_model.rb.velocity.y <= _model.landVelThreshold);
                
                _model.ClearJumpBuffer();
                _model.airEnteredFromGround = false;
                _fsm.ChangeState(FSM.States.Grounded);
            }
        }

        public void OnFixedUpdate()
        {
            if (!_model.canMove) return;

            // Control aéreo
            Vector2 input = new Vector2(_model.xAxis, _model.zAxis);
            if (input.sqrMagnitude > 1f) input.Normalize();

            Vector3 f = (_moveBasis ? _moveBasis.forward : _model.transform.forward); f.y = 0f; f = f.sqrMagnitude > 0f ? f.normalized : Vector3.forward;
            Vector3 r = (_moveBasis ? _moveBasis.right   : _model.transform.right);   r.y = 0f; r = r.sqrMagnitude > 0f ? r.normalized : Vector3.right;

            Vector3 wishDir = (r * input.x + f * input.y);
            Vector3 horizVel = new Vector3(_model.rb.velocity.x, 0f, _model.rb.velocity.z);

            Vector3 targetVel = wishDir * _model.airMaxSpeed;
            Vector3 delta = targetVel - horizVel;
            float maxDelta = _model.airAcceleration * Time.fixedDeltaTime;
            Vector3 add = Vector3.ClampMagnitude(delta, maxDelta);

            _model.rb.AddForce(new Vector3(add.x, 0f, add.z), ForceMode.VelocityChange);
        }

        public void OnExit()
        {
            _model.OnJump -= OnJumpPressed;
            
            _model.lastAirTime   = _airTime;
            _model.lastFallSpeed = _model.rb.velocity.y;
            _model.landedPending = true;
        }

        private void OnJumpPressed()
        {
            _model.BufferJumpNow();
        }

        private void TryConsumeCoyoteOrBuffer()
        {
            bool coyoteValid = _enteredFromGround && (Time.time - _model.lastLeftGroundTime) <= _model.coyoteTime;
            
            bool pressedNow  = _model.jumpDownThisFrame;
            bool bufferedNow = _model.HasJumpBufferedAfterLeftGround();
            bool inputOk     = pressedNow || bufferedNow;

            if (!(coyoteValid && inputOk)) return;

            PerformAirJump();
            _model.ClearJumpBuffer();
            _enteredFromGround = false;
        }

        private void PerformAirJump()
        {
            float g = Physics.gravity.y;
            float h = Mathf.Max(0.01f, _model.jumpHeight);
            float jumpVel = Mathf.Sqrt(2f * Mathf.Abs(g) * h);

            var v = _model.rb.velocity; v.y = 0f; _model.rb.velocity = v;
            _model.rb.AddForce(Vector3.up * jumpVel, ForceMode.VelocityChange);

            _airTime = 0f;
        }
        
    }
}
