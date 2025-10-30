using Player.Scripts.MovementFSM.MVC;
using Player.Scripts.MovementFSM.Player.Scripts.MovementFSM;
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

        Vector3 _wallNormal; // normal real de la pared (puede tener Y)
        Vector3 _planarNormal; // normal proyectada en XZ (para adherencia)
        Vector3 _lastHitPoint;

        // -----------------------------
        // Parámetros
        // -----------------------------

        // Detección base
        readonly float _climbDetectLength = 0.9f;
        readonly float _climbSphereRadius = 0.35f;
        [Range(0, 85)] readonly float _climbMaxLookAngle = 80f; // mirar a los costados sin caerse
        [Range(0, 89)] readonly float _climbMinWallSlopeDeg = 70f;

        // Velocidades objetivo (m/s)
        readonly float _upSpeed = 3.2f;
        readonly float _downSpeed = 3.0f;
        readonly float _slideSpeed = 0.01f;

        // Control vertical por aceleración
        readonly float _maxUpAccel = 30f; // m/s^2
        readonly float _maxDownAccel = 30f;

        // Adherencia a la pared (control PD solo en XZ)
        readonly float _standOff = 0.18f; // separación desde la pared
        readonly float _stickKp = 90f; // resorte (probar 60–140)
        readonly float _stickKd = 12f; // amortiguación (probar 8–20)
        readonly float _maxStickAccel = 80f; // clamp de aceleración de adherencia

        // Cancelación de velocidad "hacia adentro" (no empujes la pared)
        readonly float _intoCancelMaxAccel = 120f;

        // Ledge / auto-mantle
        readonly float _mantleSpeed = 4.6f; // m/s del “pop” de subida
        readonly float _mantleAssistAccel = 24f; // empuje adicional durante el pop
        readonly float _mantleAssistTime = 0.22f; // s

        readonly float _maxOutSpeedBeforeMantle = 1.6f;
        readonly float _maxMantleSpeed = 5.0f;

        readonly float _mantleDragAdd = 0.6f;
        float _prevDrag;
        
        private float _enterTime;
        private Vector3 _entryVelocity;

        // Debug
        readonly bool _drawGizmos = false;

        // Estado interno de mantle
        bool _mantling;
        float _mantleUntil;
        Vector3 _mantleDirCached;
        Vector3 _mantleStandPoint;

        private ParkourScanner _scan;

        public S_Climb(FSM fsm, Model model)
        {
            _fsm = fsm;
            _m = model;
        }

        public void OnEnter()
        {
            _rb = _m.rb;
            _orient = _m.cameraHolderTransform ? _m.cameraHolderTransform : _m.transform;
            _capsule = _m.GetComponent<CapsuleCollider>();
            _scan = _m.GetComponent<ParkourScanner>();

            _climbing = true;
            _mantling = false;
            _m.canMove = false;
            _rb.useGravity = false;
            _enterTime = Time.time;
            _entryVelocity = _rb.velocity; // Guardar velocidad de entrada

            _m.ClimbStartEvent();

            // 1. Detección inicial
            WallCheck(true);

            // 2. Si detectamos pared, reposicionamos
            if (_hasFront)
            {
                // Cancelar SOLO la velocidad "hacia adentro" de la pared
                float vInto = Vector3.Dot(_rb.velocity, -_planarNormal);
                if (vInto > 0f)
                {
                    _rb.AddForce(_planarNormal * vInto, ForceMode.VelocityChange);
                }
            }
        }
        
        public void OnUpdate()
        {
            WallCheck(false);
            if (!_hasFront && !_mantling)
            {
                ExitToNext();
            }
        }


        // ReSharper disable Unity.PerformanceAnalysis
        public void OnFixedUpdate()
        {
            if (!_climbing) return;

            if (_mantling)
            {
                // empuje asistido breve
                if (Time.time <= _mantleUntil)
                    _rb.AddForce(_mantleDirCached * _mantleAssistAccel, ForceMode.Acceleration);

                // guía constante hacia el stand (suave)
                Vector3 toStand = _mantleStandPoint - _rb.position;
                Vector3 plan = Vector3.ProjectOnPlane(toStand, Vector3.up);
                float distPlan = plan.magnitude;
                if (distPlan > 0.01f)
                {
                    Vector3 guideDir = plan.normalized;
                    _rb.AddForce(guideDir * 20f, ForceMode.Acceleration); // “arrime” horizontal
                    _rb.AddForce((-_planarNormal) * 6f, ForceMode.Acceleration); // un poco “hacia adentro”
                }

                // cap global de speed durante el pop
                float spd = _rb.velocity.magnitude;
                if (spd > _maxMantleSpeed)
                    _rb.AddForce(-_rb.velocity.normalized * (spd - _maxMantleSpeed), ForceMode.VelocityChange);

                // cuando ya estás casi a la altura, asentar
                if (_rb.position.y >= _mantleStandPoint.y - 0.02f && distPlan < 0.12f)
                    _rb.AddForce(Vector3.down * 1.5f, ForceMode.VelocityChange);

                // salir en cuanto apoyás
                if (_m.IsGroundedNow())
                {
                    ExitToNext();
                }

                return;
            }
            
            if (!_hasFront) return;
            
            if (_m.jumpDownThisFrame)
            {
                ClimbJump();
                return;
            }
            
            float blend = EnterBlend01();
            
            float inputZ = Mathf.Abs(_m.rawZ) > 0.01f ? _m.rawZ : _m.zAxis;
            bool wantUp = (inputZ > 0.1f);
            bool wantDown = (inputZ < -0.1f);
            
            if (wantUp)
            {
                var p = _scan.Probe;
                if (p.action == ParkourAction.Climb && p.climbStandPoint != Vector3.zero)
                {
                    StartMantleFromStand(p.climbStandPoint);
                    return;
                }
                if (TryGetLedgeLocal(out var ledge, out var stand))
                {
                    StartMantleFromStand(stand);
                    return;
                }
            } else if (wantDown && _m.IsGroundedNow())
            {
                ExitToNext();
                return;
            }

            float vClimbTarget = wantUp ? _upSpeed : wantDown ? -_downSpeed : -_slideSpeed;

            float vEntryTarget = _entryVelocity.y;

            float vTargetY = Mathf.Lerp(vEntryTarget, vClimbTarget, blend);

            float vY = Vector3.Dot(_rb.velocity, Vector3.up);
            float dv = vTargetY - vY;
            float reqAy = dv / Time.fixedDeltaTime;
            float ay = Mathf.Clamp(reqAy, -_maxDownAccel, _maxUpAccel);
            
            _rb.AddForce(Vector3.up * ay, ForceMode.Acceleration);
    
            Vector3 vEntryHorizontal = Vector3.ProjectOnPlane(_entryVelocity, Vector3.up);
            if (vEntryHorizontal.sqrMagnitude > 1e-4f)
            {
                // Velocidad horizontal actual
                Vector3 vHorizontal = Vector3.ProjectOnPlane(_rb.velocity, Vector3.up);

                // Objetivo: pasar de la velocidad horizontal de entrada a CERO
                Vector3 vTargetHorizontal = Vector3.Lerp(vEntryHorizontal, Vector3.zero, blend);
        
                // Calculamos la fuerza necesaria para alcanzar ese objetivo
                Vector3 dvHorizontal = vTargetHorizontal - vHorizontal;
                Vector3 aHorizontal = dvHorizontal / Time.fixedDeltaTime; // F = m*a, a = dv/dt

                // Aplicamos la fuerza de amortiguación
                _rb.AddForce(aHorizontal, ForceMode.Acceleration);
            }

            float vInto = Vector3.Dot(_rb.velocity, -_planarNormal);
            if (vInto > 0f)
            {
                float aCancel = Mathf.Clamp(-vInto / Time.fixedDeltaTime, -_intoCancelMaxAccel, 0f);
                _rb.AddForce(_planarNormal * aCancel, ForceMode.Acceleration);
            }

            // -----------------------------
            // 3) Adherencia PD para mantener standOff en XZ
            // -----------------------------
            // Posición deseada: sobre el último punto impactado, separado _standOff por la normal planar
            Vector3 pos = _rb.position;
            Vector3 target = _lastHitPoint + _planarNormal * _standOff;

            // Error SOLO en XZ (no tocamos Y aquí)
            Vector3 delta = target - pos;
            delta.y = 0f;
            float errAlongN = Vector3.Dot(delta, _planarNormal);
            float vAlongN = Vector3.Dot(_rb.velocity, _planarNormal);

            float aStick = _stickKp * errAlongN - _stickKd * vAlongN;
            aStick = Mathf.Clamp(aStick, -_maxStickAccel, _maxStickAccel);
            _rb.AddForce(_planarNormal * (aStick * blend), ForceMode.Acceleration);
        }

        void ClimbJump()
        {
            _m.blockClimbUntil = Time.time + _m.climbRegrabCooldown;
            _m.lastWallDetachTime = Time.time;

            // Impulso (arriba + “hacia afuera”)
            Vector3 impulse = Vector3.up * _m.wallJumpUpForce + _wallNormal * _m.wallJumpSideForce;

            // Cancelar velocidad hacia adentro sin tocar velocity directamente (usamos VelocityChange)
            float vInto = Vector3.Dot(_rb.velocity, -_planarNormal);
            if (vInto > 0f) _rb.AddForce(_planarNormal * vInto, ForceMode.VelocityChange);

            // Limpieza componente vertical acumulada
            float vy = Vector3.Dot(_rb.velocity, Vector3.up);
            if (vy > 0f) _rb.AddForce(Vector3.down * Mathf.Min(vy, 2f), ForceMode.VelocityChange);

            _rb.AddForce(impulse, ForceMode.Impulse);
            ExitToNext();
        }

        public void OnExit()
        {
            _climbing = false;
            _mantling = false;
            _m.canMove = true;
            _rb.useGravity = true;
            _rb.drag = _prevDrag;

            _m.blockClimbUntil = Time.time + _m.climbRegrabCooldown;
            _m.ClimbEndEvent();
        }


        public void OnLateUpdate()
        {
        }

        bool TryGetLedgeLocal(out Vector3 ledgePoint, out Vector3 standPoint)
        {
            ledgePoint = Vector3.zero;
            standPoint = Vector3.zero;
            if (!_capsule) return false;

            float h = _capsule.height;
            float r = _capsule.radius;
            Vector3 pos = _m.transform.position;

            // “cabeza” + un toque hacia adentro de la pared
            float headY = Mathf.Clamp(h * 0.92f, 1.3f, 1.9f);
            Vector3 headOrigin = pos + Vector3.up * headY;
            Vector3 inward = (-_planarNormal).sqrMagnitude > 1e-6f ? (-_planarNormal).normalized : Vector3.forward;

            // si a nivel cabeza ya NO hay pared, puede haber borde
            bool headHits = Physics.SphereCast(headOrigin, r * 0.95f, _orient.forward, out _, _climbDetectLength * 0.9f,
                ~0, QueryTriggerInteraction.Ignore);
            if (headHits) return false;

            // downcast desde un poco adentro y arriba para hallar la tapa
            Vector3 downFrom = headOrigin + inward * (r + 0.08f) + Vector3.up * 0.40f;
            if (Physics.Raycast(downFrom, Vector3.down, out var topHit, 2.2f, ~0, QueryTriggerInteraction.Ignore))
            {
                // debe ser razonablemente horizontal
                if (Vector3.Dot(topHit.normal.normalized, Vector3.up) >= Mathf.Cos(32f * Mathf.Deg2Rad))
                {
                    ledgePoint = topHit.point;
                    // stand un pelín adentro y elevado al radio
                    Vector3 stand = topHit.point + inward * (r + 0.06f) + Vector3.up * (r + 0.06f);

                    // clearance real del cápsule en destino
                    float heightSeg = h - 2f * r;
                    Vector3 p1 = stand + Vector3.up * (r + 0.02f);
                    Vector3 p2 = p1 + Vector3.up * heightSeg;
                    bool blocked = Physics.CheckCapsule(p1, p2, Mathf.Max(0.05f, r * 0.95f), ~0,
                        QueryTriggerInteraction.Ignore);
                    if (!blocked)
                    {
                        standPoint = stand;
                        return true;
                    }
                }
            }

            return false;
        }
        
        float EnterBlend01()
        {
            return Mathf.Clamp01((Time.time - _enterTime) / Mathf.Max(0.01f, 0.2f));
        }
        
        void StartMantleFromStand(Vector3 standP)
        {
            _mantling = true;
            _mantleStandPoint = standP;

            // gravedad ON para que el arco no “flote”
            _rb.useGravity = true;

            // neutralizar horizontal al iniciar el pop
            Vector3 vPlanar = Vector3.ProjectOnPlane(_rb.velocity, Vector3.up);
            if (vPlanar.sqrMagnitude > 1e-6f)
                _rb.AddForce(-vPlanar, ForceMode.VelocityChange);

            // limitar salida hacia afuera de la pared
            float vOut = Vector3.Dot(_rb.velocity, _planarNormal);
            if (vOut > _maxOutSpeedBeforeMantle)
                _rb.AddForce(-_planarNormal * (vOut - _maxOutSpeedBeforeMantle), ForceMode.VelocityChange);

            // dirección principal: hacia stand + up
            Vector3 toStand = (_mantleStandPoint - _rb.position);
            Vector3 planToStand = Vector3.ProjectOnPlane(toStand, Vector3.up);
            Vector3 dir = (planToStand.normalized * 0.7f + Vector3.up * 0.9f).normalized;
            _mantleDirCached = dir;

            // impulso Δv ≈ _mantleSpeed
            _rb.AddForce(dir * (_rb.mass * _mantleSpeed), ForceMode.Impulse);

            // drag temporal para amortiguar
            _prevDrag = _rb.drag;
            _rb.drag = _prevDrag + _mantleDragAdd;

            _mantleUntil = Time.time + _mantleAssistTime;
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

            // SphereCast adelante
            if (Physics.SphereCast(origin, _climbSphereRadius, fwd, out _frontHit,
                    _climbDetectLength, ~0, QueryTriggerInteraction.Ignore))
            {
                if (_frontHit.collider && _frontHit.collider.CompareTag(_m.tagClimb))
                {
                    float slopeDeg = Vector3.Angle(_frontHit.normal, Vector3.up);
                    if (slopeDeg >= _climbMinWallSlopeDeg)
                    {
                        float lookAng = Vector3.Angle(fwd, -_frontHit.normal);
                        if (lookAng <= _climbMaxLookAngle)
                        {
                            _hasFront = true;
                            _wallNormal = _frontHit.normal.normalized;
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

            // Pequeño “grace” si perdimos la pared un frame
            if (!_hasFront && !forceFirst)
            {
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

        void StartMantleFromProbe(Vector3 standP)
        {
            _mantling = true;

            // activar gravedad para que el arco no flote
            _rb.useGravity = true;

            // 1) cap de velocidad saliente antes del pop (sobre normal de pared)
            float vOut = Vector3.Dot(_rb.velocity, _planarNormal);
            if (vOut > _maxOutSpeedBeforeMantle)
                _rb.AddForce(-_planarNormal * (vOut - _maxOutSpeedBeforeMantle), ForceMode.VelocityChange);

            // 2) dirección del pop: hacia el punto de stand + algo de “up”
            Vector3 toStand = (standP - _rb.position);
            Vector3 planToStand = Vector3.ProjectOnPlane(toStand, Vector3.up);
            Vector3 dir = (planToStand.normalized * 0.7f + Vector3.up * 0.9f).normalized;
            _mantleDirCached = dir;

            // 3) impulso principal (Δv ≈ _mantleSpeed)
            _rb.AddForce(dir * (_rb.mass * _mantleSpeed), ForceMode.Impulse);

            // 4) drag temporal para amortiguar (se restaura en OnExit)
            _prevDrag = _rb.drag;
            _rb.drag = _prevDrag + _mantleDragAdd;

            // 5) asistencia breve
            _mantleUntil = Time.time + _mantleAssistTime;
        }

        void ExitToNext()
        {
            _fsm.ChangeState(_m.IsGroundedNow() ? FSM.States.Grounded : FSM.States.Air);
        }
    }
}