using Player.Scripts.MovementFSM.MVC;
using UnityEngine;

namespace Player.Scripts.MovementFSM
{
    public class S_Wallrun : IState
    {
        private readonly FSM _fsm;
        private readonly Model _model;

        private Rigidbody _rb;
        private Transform _orient;
        private Vector3 _wallNormal;
        private int _side;

        private float _timer;
        private bool _exiting;
        private float _exitTimer;
        private float _enterTime;
        private Vector3 _lastWallPoint;
        
        public S_Wallrun(FSM fsm, Model model)
        {
            _fsm = fsm;
            _model = model;
        }

        public void OnEnter()
        {
            _rb = _model.rb;
            _orient = _model.cameraHolderTransform ? _model.cameraHolderTransform : _model.transform;

            var p = _model.probe;
            ReadProbe(p);
            _lastWallPoint = p.wallRunWallPoint;
            
            _model.WallrunEvent(_side);
            
            _timer = _model.maxWallRunTime;
            _exiting = false;
            _enterTime = Time.time;
            
        }

        public void OnUpdate()
        {
            if (_model.IsGroundedNow())
            {
                _fsm.ChangeState(FSM.States.Grounded);
                return;
            }
            
            var p = _model.probe;
            
            if (p.action == ParkourAction.Climb &&
                (_model.zAxis > 0.1f || _model.jumpDownThisFrame))
            {
                _fsm.ChangeState(FSM.States.Climb);
                return;
            }
            
            if (p.action != ParkourAction.WallrunLeft && p.action != ParkourAction.WallrunRight)
            {
                _fsm.ChangeState(FSM.States.Air);
                return;
            }
            
            ReadProbe(p);
            _lastWallPoint = p.wallRunWallPoint;
            
            if (!_exiting)
            {
                _timer -= Time.deltaTime;
                if (_timer <= 0f)
                {
                    _exiting = true;
                    _exitTimer = _model.exitWallTime;
                }
            }
            else
            {
                _exitTimer -= Time.deltaTime;
                if (_exitTimer <= 0f) _exiting = false;
            }

            if (_model.jumpDownThisFrame && !_exiting)
            {
                WallJump();
                _fsm.ChangeState(FSM.States.Air);
            }
        }

        public void OnFixedUpdate()
        {
            if (_exiting) return;
            
            var p = _model.probe;
            if (p.action != ParkourAction.WallrunLeft && p.action != ParkourAction.WallrunRight)
            {
                _fsm.ChangeState(FSM.States.Air);
                return;
            }

            WallrunForces();
        }

        public void OnExit()
        {
            
        }

        private void ReadProbe(in ParkourProbe p)
        {
            if (p.action == ParkourAction.WallrunRight || p.action == ParkourAction.WallrunLeft)
            {
                _wallNormal = p.wallRunNormal;
                _side = p.wallSide;
            }
        }
        float EnterBlend01()
        {
            return Mathf.Clamp01((Time.time - _enterTime) / Mathf.Max(0.01f, _model.wallEnterBlendTime));
        }

        private void WallrunForces()
        {
            // --- 1) Tangente de la pared (elegir sentido más cercano al forward) ---
            Vector3 wallForward = Vector3.Cross(_wallNormal, Vector3.up);
            Vector3 fwd = _orient ? _orient.forward : _model.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f) fwd = _model.transform.forward;
            fwd.Normalize();
            if ((fwd - wallForward).sqrMagnitude > (fwd + wallForward).sqrMagnitude)
                wallForward = -wallForward;

            // Alinear suavemente la dirección objetivo (suaviza cambios bruscos de tangente)
            float alignT = 1f - Mathf.Exp(-_model.wallAlignLerp * Time.fixedDeltaTime);
            Vector3 vHoriz = new Vector3(_rb.velocity.x, 0f, _rb.velocity.z);
            if (vHoriz.sqrMagnitude > 1e-6f)
            {
                Vector3 curDir = vHoriz.normalized;
                Vector3 newDir = Vector3.Slerp(curDir, wallForward, alignT);
                float speed = vHoriz.magnitude;
                vHoriz = newDir * speed;
            }

            // --- 2) Objetivo de velocidad a lo largo de la pared (usa tu lógica de cruise) ---
            Vector3 camF = _orient ? _orient.forward : _model.transform.forward;
            camF.y = 0f;
            camF.Normalize();
            Vector3 camR = _orient ? _orient.right : _model.transform.right;
            camR.y = 0f;
            camR.Normalize();
            Vector3 wishDir = (camR * _model.xAxis + camF * _model.zAxis);
            if (wishDir.sqrMagnitude > 1e-4f) wishDir.Normalize();

            float inputAlong = Vector3.Dot(wishDir, wallForward);
            bool pushingForward = inputAlong > _model.wallInputThreshold;

            float curAlong = Vector3.Dot(vHoriz, wallForward);
            float targetAlong = pushingForward
                ? _model.wallRunMaxSpeed
                : Mathf.Max(_model.wallCruiseSpeed, curAlong);

            float accel = pushingForward ? _model.wallRunAccel : _model.wallCruiseAccel;
            float delta = Mathf.Clamp(targetAlong - curAlong,
                -accel * Time.fixedDeltaTime,
                accel * Time.fixedDeltaTime);

            Vector3 addAlong = wallForward * delta;
            _rb.AddForce(addAlong, ForceMode.VelocityChange);

            // --- 3) Adherencia SUAVE: PD hacia standOff (no snap duro) ---
            // Medimos distancia actual a la pared a lo largo de la normal (punto más reciente)
            // _lastWallPoint viene del probe de Update (si querés, refrescalo ahí)
            Vector3 fromWall = _rb.position - _lastWallPoint;
            float dist = Vector3.Dot(fromWall, -_wallNormal); // distancia positiva si “dentro”
            float err = (_model.wallStandOff - dist); // >0 queremos alejarnos, <0 acercarnos

            // Velocidad "hacia la pared" (componente contra la normal)
            float vInto = Vector3.Dot(_rb.velocity, -_wallNormal);

            // PD (resorte + amortiguador)
            float blend = EnterBlend01(); // rampa 0→1 al entrar
            float stick = blend * _model.wallStickKp * err - _model.wallStickKd * vInto;

            _rb.AddForce(-_wallNormal * stick, ForceMode.Force);

            // --- 4) Atenuar la gravedad con blend-in ---
            if (_model.wallUseGravity)
            {
                _rb.AddForce(Vector3.up * (_model.gravityCounterForce * blend), ForceMode.Force);
            }

            // --- 5) Y vertical: subir/bajar/glide (igual que antes) ---
            bool upwards = _model.runningKeyPressed;
            bool downwards = Input.GetKey(_model.crouchKey);
            float vy = _rb.velocity.y;

            if (upwards)
                vy = Mathf.MoveTowards(vy, _model.wallClimbSpeed, 12f * Time.fixedDeltaTime);
            else if (downwards)
                vy = Mathf.MoveTowards(vy, -_model.wallDescendSpeed, 12f * Time.fixedDeltaTime);
            else if (!pushingForward)
                vy = Mathf.MoveTowards(vy, -_model.wallGlideDownSpeed, 8f * Time.fixedDeltaTime);
            else
                vy = Mathf.MoveTowards(vy, 0f, 8f * Time.fixedDeltaTime);

            // Aplicar vy
            _rb.velocity = new Vector3(vHoriz.x + addAlong.x, vy, vHoriz.z + addAlong.z);

            // --- 6) NO elimines de golpe la componente que se mete en la pared: disípala suave ---
            float into = Vector3.Dot(_rb.velocity, -_wallNormal);
            if (into > 0f)
            {
                float remove = Mathf.Min(into, _model.wallIntoDamp * Time.fixedDeltaTime);
                _rb.velocity -= _wallNormal * remove;
            }
        }
        
        private void WallJump()
        {
            _exiting = true;
            _exitTimer = _model.exitWallTime;

            // bloquear reenganche por scanner por un ratito
            _model.blockWallrunUntil = Time.time + _model.wallRegrabCooldown;

            Vector3 impulse = Vector3.up * _model.wallJumpUpForce + _wallNormal * _model.wallJumpSideForce;

            // limpiar componente hacia la pared + reset y
            Vector3 v = _rb.velocity;
            float into = Vector3.Dot(v, -_wallNormal);
            if (into > 0f) v -= _wallNormal * into;
            v.y = 0f;
            _rb.velocity = v;

            _rb.AddForce(impulse, ForceMode.Impulse);
        }
    }
}