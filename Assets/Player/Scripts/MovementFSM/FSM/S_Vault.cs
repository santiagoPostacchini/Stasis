using Player.Scripts.MovementFSM.MVC;
using UnityEngine;

namespace Player.Scripts.MovementFSM
{
    public class S_Vault : IState
    {
        private readonly FSM _fsm;
        private readonly Model _m;

        public S_Vault(FSM fsm, Model model)
        {
            _fsm = fsm;
            _m = model;
        }

        Rigidbody _rb;
        CapsuleCollider _capsule;
        Collider _ignoredObstacle;

        Vector3 _p0, _p1, _p2;
        Vector3 _enterForward;
        float _t, _dur;
        bool _oldUseGravity, _collisionsOff;
        RigidbodyInterpolation _oldInterp;

        bool _gravityRestored;

        const float ReleaseBlendStart = 0.85f;

        const float Kp = 80f;
        const float Kd = 16f;
        const float MaxAccel = 120f;

        float _lockedYawDeg;
        const bool UprightWhileVault = true;
        const float UprightSettleDegPerSec = 9999f;

        const float VaultSpeed = 6.0f;
        const float VaultMinTime = 0.22f;
        const float VaultMaxTime = 0.55f;
        const float ApexFactor = 0.5f;
        const float ApexExtra = 0.25f;
        const float NormalClearance = 0.06f;
        const float StartUpNudge = 0.02f;
        float _entryHorizSpeed;
        float _pathHorizSpeed;


        public void OnEnter()
        {
            _rb = _m.rb;
            _capsule = _m.GetComponent<CapsuleCollider>();
            var p = _m.probe;
            
            Vector3 vIn = _rb.velocity; vIn.y = 0f;
            _entryHorizSpeed = vIn.magnitude;

            _oldUseGravity = _rb.useGravity;
            _rb.useGravity = false;
            _rb.velocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;

            _gravityRestored = false;

            _oldInterp = _rb.interpolation;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;

            _m.canMove = false;

            // Bloquear yaw actual
            _lockedYawDeg = YawDeg(_rb.rotation);

            BuildVaultCurve(p);

            // Postura inicial (NO tocamos yaw, solo enderezar si querés)
            if (UprightWhileVault)
            {
                var target = Quaternion.Euler(0f, _lockedYawDeg, 0f);
                _rb.MoveRotation(target);
            }

            // Colisiones
            if (_capsule && p.vaultObstacle)
            {
                Physics.IgnoreCollision(_capsule, p.vaultObstacle, true);
                _ignoredObstacle = p.vaultObstacle;
            }
            else
            {
                _rb.detectCollisions = false;
                _collisionsOff = true;
            }

            _m.VaultStartEvent();
            _t = 0f; // el tiempo avanza en FixedUpdate
        }

        public void OnUpdate()
        {
            /* vacío, todo en FixedUpdate */
        }

        public void OnFixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            _t = Mathf.Min(1f, _t + dt / _dur);

            // s actual y próximo para vel deseada
            float sNow = EaseInOutCubic(_t);
            float sNext = EaseInOutCubic(Mathf.Min(1f, _t + dt / _dur));

            Vector3 posNow = Bezier2(_p0, _p1, _p2, sNow);
            Vector3 posNext = Bezier2(_p0, _p1, _p2, sNext);
            Vector3 velDesired = (posNext - posNow) / dt;

            // --- FADE-OUT DEL CONTROL ---
            float gain = 1f;
            if (_t >= ReleaseBlendStart)
            {
                // 0 en ReleaseStart, 1 en el final; suavizamos cúbico
                float u = Mathf.InverseLerp(ReleaseBlendStart, 1f, _t);
                gain = 1f - (u * u * (3f - 2f * u)); // 1 - smoothstep(u)

                // restaurar gravedad una sola vez al entrar en release
                if (!_gravityRestored)
                {
                    _rb.useGravity = true;
                    _gravityRestored = true;
                }
            }

            // PD con ganancia escalada
            Vector3 posErr = posNow - _rb.position;
            Vector3 velErr = velDesired - _rb.velocity;
            Vector3 accel = (Kp * posErr + Kd * velErr) * gain;

            // limitar
            float aMag = accel.magnitude;
            if (aMag > MaxAccel) accel *= (MaxAccel / aMag);

            // empuje físico
            _rb.AddForce(accel, ForceMode.Acceleration);

            // Mantener yaw (solo upright opcional)
            if (UprightWhileVault)
            {
                var curr = _rb.rotation;
                var goal = Quaternion.Euler(0f, _lockedYawDeg, 0f);
                var next = Quaternion.RotateTowards(curr, goal, UprightSettleDegPerSec * dt);
                _rb.MoveRotation(next);
            }

            if (_t >= 1f)
                ExitToGrounded();
        }

        public void OnExit()
        {
            if (_ignoredObstacle && _capsule)
                Physics.IgnoreCollision(_capsule, _ignoredObstacle, false);

            if (_collisionsOff)
                _rb.detectCollisions = true;

            _rb.useGravity = _oldUseGravity;
            _rb.interpolation = _oldInterp;

            _m.canMove = true;
            _m.VaultEndEvent();
        }

        private void BuildVaultCurve(in ParkourProbe p)
        {
            float radius = _capsule ? _capsule.radius : (p.playerRadius > 0 ? p.playerRadius : 0.3f);

            _p0 = _rb.position + Vector3.up * StartUpNudge;
            _p2 = p.vaultLandPoint;

            // Forward geométrico (sólo para curva)
            Vector3 intended = _p2 - _p0;
            intended.y = 0f;
            Vector3 fwdHint = p.vaultForward;
            fwdHint.y = 0f;
            if (intended.sqrMagnitude > 1e-6f && fwdHint.sqrMagnitude > 1e-6f &&
                Vector3.Dot(fwdHint, intended) < 0f)
                fwdHint = -fwdHint;

            Vector3 landDir = intended.sqrMagnitude > 1e-6f
                ? intended.normalized
                : (fwdHint.sqrMagnitude > 1e-6f
                    ? fwdHint.normalized
                    : (_m.cameraHolderTransform ? _m.cameraHolderTransform.forward : _m.transform.forward));
            landDir.y = 0f;
            landDir.Normalize();
            _enterForward = landDir;

            // Duración por distancia horizontal
            float dxz = Vector3.Distance(new Vector3(_p0.x, 0, _p0.z), new Vector3(_p2.x, 0, _p2.z));
            _dur = Mathf.Clamp(dxz / Mathf.Max(0.1f, VaultSpeed), VaultMinTime, VaultMaxTime);
            
            float minVaultSpeed = Mathf.Max(1f, _m.walkingSpeed * 0.85f);
            float maxVaultSpeed = Mathf.Max(minVaultSpeed + 0.1f, _m.runningSpeed * 1.25f);
            
            _pathHorizSpeed = Mathf.Clamp(_entryHorizSpeed, minVaultSpeed, maxVaultSpeed);

            // evitar vault eterno/instantáneo con clamps de tiempo
            _dur = Mathf.Clamp(dxz / Mathf.Max(0.1f, _pathHorizSpeed), VaultMinTime, VaultMaxTime);
            
            // Control point
            float apexY = Mathf.Max(0.15f, p.obstacleHeight * ApexFactor + ApexExtra);
            float sep = radius + NormalClearance;

            Vector3 midXZ = p.vaultMidXZ;
            _p1 = midXZ + p.hitNormal * sep + Vector3.up * apexY;

            // Garantizar tangente inicial hacia delante
            Vector3 tan0 = Bezier2Tangent(_p0, _p1, _p2, 0.001f);
            tan0.y = 0f;
            if (Vector3.Dot(tan0, landDir) <= 0f)
            {
                float ctrlAhead = Mathf.Max(0.35f, dxz * 0.35f);
                _p1 = _p0 + landDir * ctrlAhead + Vector3.up * apexY;
            }
        }

        private void ExitToGrounded()
        {
            // asegurá gravedad ON (si no se restauró durante el release)
            _rb.useGravity = true;

            Vector3 vel = _rb.velocity;
            Vector3 tan = Bezier2Tangent(_p0, _p1, _p2, 1f); tan.y = 0f;

            Vector3 dir = tan.sqrMagnitude > 1e-6f ? tan.normalized
                : (new Vector3(vel.x,0,vel.z).sqrMagnitude > 1e-6f
                    ? new Vector3(vel.x,0,vel.z).normalized
                    : _enterForward);

            float currentHoriz = new Vector3(vel.x,0,vel.z).magnitude;

            // piso de velocidad horizontal: conserva al menos la del trayecto o el walk
            float targetHoriz = Mathf.Max(currentHoriz, _pathHorizSpeed * 0.95f, _m.walkingSpeed);

            Vector3 horiz = dir * targetHoriz;
            vel.x = horiz.x; vel.z = horiz.z; // NO tocamos vel.y (lo soltamos)
            _rb.velocity = vel;

            _m.blockVaultUntil = Time.time + _m.vaultRegrabCooldown;
            _fsm.ChangeState(FSM.States.Grounded);
        }

        static float YawDeg(Quaternion q)
        {
            Vector3 f = q * Vector3.forward;
            f.y = 0f;
            if (f.sqrMagnitude < 1e-6f) return 0f;
            f.Normalize();
            return Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
        }

        static float EaseInOutCubic(float t)
            => (t < 0.5f) ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;

        static Vector3 Bezier2(Vector3 p0, Vector3 p1, Vector3 p2, float s)
        {
            float omt = 1f - s;
            return (omt * omt) * p0 + (2f * omt * s) * p1 + (s * s) * p2;
        }

        static Vector3 Bezier2Tangent(Vector3 p0, Vector3 p1, Vector3 p2, float s)
        {
            return 2f * (1f - s) * (p1 - p0) + 2f * s * (p2 - p1);
        }
    }
}