using UnityEngine;
using Player.Scripts.MovementFSM.MVC;

namespace Player.Scripts.MovementFSM
{
    public class S_Climb : IState
    {
        private readonly FSM _fsm;
        private readonly Model _m;

        public S_Climb(FSM fsm, Model model) { _fsm = fsm; _m = model; }
        
        Rigidbody _rb;
        CapsuleCollider _capsule;
        Transform _orientation;
        
        LayerMask _climbMask;

        // Climbing
        private readonly float _climbSpeed         = 2.8f;      // m/s vertical
        private readonly float _maxClimbTime       = 1.25f;     // s trepando continuo
        private float _climbTimer;

        // Climb Jump
        private readonly float _climbJumpUpForce   = 7.5f;      // impulso hacia arriba
        private readonly float _climbJumpBackForce = 5.0f;      // empuje alejándose de la pared
        private readonly int   _climbJumps         = 1;         // cantidad de saltos desde la pared
        private int   _climbJumpsLeft;

        // Detección
        private readonly float _detectionLength    = 0.8f;      // cuánto “buscar” hacia adelante
        private float sphereCastRadius   = 0.35f;
        float maxWallLookAngle   = 30f;       // grados entre forward y -normal pared para permitir trepa

        float minWallNormalAngleChange = 10f; // para resetear temporizador al cambiar mucho de normal

        // Exiting (cooldown de salida)
        float exitWallTime       = 0.25f;
        float exitWallTimer;

        // ---- runtime ----
        bool  _climbing;
        bool  _exitingWall;
        bool  _wantsForward;

        bool  _wallFront;
        float _wallLookAngle;
        RaycastHit _frontWallHit;

        Transform _lastWall;
        Vector3   _lastWallNormal;

        bool _oldUseGravity;
        RigidbodyInterpolation _oldInterp;

        // thresholds
        const float ForwardInputThreshold = 0.1f;

        public void OnEnter()
        {
            
            _rb = _m.rb;
            _capsule = _m.GetComponent<CapsuleCollider>();
            _orientation = _m.cameraHolderTransform ? _m.cameraHolderTransform : _m.transform;

            var sc = _m.GetComponent<ParkourScanner>();
            _climbMask = (sc && sc.climbMask.value != 0) ? sc.climbMask : (sc ? sc.environmentMask : ~0);

            _oldUseGravity     = _rb.useGravity;
            _oldInterp         = _rb.interpolation;
            _rb.interpolation  = RigidbodyInterpolation.Interpolate;

            _climbing          = false;
            _exitingWall       = false;
            _lastWall          = null;
            _lastWallNormal    = Vector3.zero;

            // reset timers
            _climbTimer         = _maxClimbTime;
            _climbJumpsLeft     = _climbJumps;
            exitWallTimer      = 0f;

            _m.canMove         = false; // bloqueá input locomoción normal en este estado
        }

        public void OnUpdate()
        {
            // input hacia adelante (no usamos Input.GetKey, respetamos tu pipeline)
            _wantsForward = _m.zAxis > ForwardInputThreshold;

            // detección pared + ángulo de mirada
            WallCheck();

            // FSM local (idéntica a la del ejemplo)
            if (_wallFront && _wantsForward && _wallLookAngle < maxWallLookAngle && !_exitingWall)
            {
                if (!_climbing && _climbTimer > 0f) StartClimbing();

                if (_climbTimer > 0f) _climbTimer -= Time.deltaTime;
                if (_climbTimer <= 0f) StopClimbing(); // se agotó el tiempo
            }
            else if (_exitingWall)
            {
                if (_climbing) StopClimbing();

                if (exitWallTimer > 0f) exitWallTimer -= Time.deltaTime;
                if (exitWallTimer <= 0f) _exitingWall = false;
            }
            else
            {
                if (_climbing) StopClimbing();
            }

            // Climb Jump (como el ejemplo)
            if (_wallFront && _m.jumpDownThisFrame && _climbJumpsLeft > 0)
                ClimbJump();

            // Condiciones de salida del ESTADO Climb (a otros estados de tu FSM)
            if (!_climbing && !_exitingWall)
            {
                if (_m.IsGroundedNow()) _fsm.ChangeState(FSM.States.Grounded);
                else                     _fsm.ChangeState(FSM.States.Air);
            }
        }

        public void OnFixedUpdate()
        {
            // Movimiento vertical solo mientras estamos "climbing" activo
            if (_climbing && !_exitingWall)
            {
                // desactivamos gravedad y fijamos velocidad vertical
                _rb.useGravity = false;
                Vector3 v = _rb.velocity;
                v.y = _climbSpeed;
                _rb.velocity = v;
            }
            else
            {
                // si no estamos trepando, devolver gravedad (en exit/air/ground)
                _rb.useGravity = _oldUseGravity;
            }
        }

        public void OnExit()
        {
            _rb.useGravity     = _oldUseGravity;
            _rb.interpolation  = _oldInterp;

            _m.canMove = true;
            _m.blockClimbUntil = Time.time + _m.climbRegrabCooldown;
        }

        void WallCheck()
        {
            _wallFront = false;
            _wallLookAngle = 180f;

            float r = _capsule ? _capsule.radius : 0.3f;
            float h = _capsule ? _capsule.height : 1.8f;
            float chest = Mathf.Clamp(h * 0.55f, 0.8f, 1.1f);

            Vector3 origin = _m.transform.position + Vector3.up * chest;
            Vector3 fwd    = _orientation ? _orientation.forward : _m.transform.forward;
            
            if (Physics.SphereCast(origin, sphereCastRadius, fwd, out _frontWallHit, _detectionLength, _climbMask, QueryTriggerInteraction.Ignore))
            {
                _wallFront = true;
                _wallLookAngle = Vector3.Angle(fwd, -_frontWallHit.normal);
                
                bool newWall = _frontWallHit.transform != _lastWall
                               || Mathf.Abs(Vector3.Angle(_lastWallNormal, _frontWallHit.normal)) > minWallNormalAngleChange;

                if ((_wallFront && newWall) || _m.IsGroundedNow())
                {
                    _climbTimer     = _maxClimbTime;
                    _climbJumpsLeft = _climbJumps;
                    _lastWall      = _frontWallHit.transform;
                    _lastWallNormal= _frontWallHit.normal;
                }
            }
        }

        void StartClimbing()
        {
            _climbing = true;
            Vector3 v = _rb.velocity; v.y = 0f; _rb.velocity = v;
        }

        void StopClimbing()
        {
            _climbing = false;
        }

        void ClimbJump()
        {
            _exitingWall  = true;
            exitWallTimer = exitWallTime;
            
            Vector3 up    = Vector3.up * _climbJumpUpForce;
            Vector3 back  = _frontWallHit.normal * _climbJumpBackForce;
            Vector3 force = up + back;

            // resetear vel.y antes del impulso (como el ejemplo)
            Vector3 v = _rb.velocity; v.y = 0f; _rb.velocity = v;
            _rb.AddForce(force, ForceMode.Impulse);

            _climbJumpsLeft--;

            // pasamos al estado de aire (el impulso ya salió)
            _fsm.ChangeState(FSM.States.Air);
        }
    }
}