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
        ParkourScanner _scanner;

        Vector3 _p0, _p1, _p2;
        Vector3 _enterForward;
        float _t, _dur;
        bool _oldUseGravity, _collisionsOff;
        RigidbodyInterpolation _oldInterp;
        bool _gravityRestored;
        bool _stepUpMode;
        float _entryHorizSpeed, _pathHorizSpeed;
        float _lockedYawDeg;

        // === Tunables ===
        const float ReleaseBlendStart = 0.60f;   // <- release al 60%
        const float Kp = 80f, Kd = 16f, MaxAccel = 120f;
        const bool UprightWhileVault = true;
        const float UprightSettleDegPerSec = 9999f;

        const float VaultMinTime = 0.22f, VaultMaxTime = 0.55f;
        const float ApexFactor = 0.5f, ApexExtra = 0.25f;
        const float NormalClearance = 0.06f, StartUpNudge = 0.02f;

        // step-up suavizado (sin impulso extra)
        const float StepUpInset   = 0.12f;
        const float StepUpExtraUp = 0.045f;
        const float StepUpApexCap = 0.26f;

        bool LogOn => !_scanner || _scanner.verboseLogs;

        public void OnEnter()
        {
            _rb = _m.rb;
            _capsule = _m.GetComponent<CapsuleCollider>();
            _scanner = _m.GetComponent<ParkourScanner>();
            var p = _m.probe;

            // vel de entrada (solo horizontal)
            Vector3 vIn = _rb.velocity; vIn.y = 0f;
            _entryHorizSpeed = vIn.magnitude;

            // physics setup
            _oldUseGravity = _rb.useGravity;
            _rb.useGravity = false;            // controlaremos cuándo vuelve
            _rb.velocity = Vector3.zero;       // arrancamos desde curva
            _rb.angularVelocity = Vector3.zero;

            _gravityRestored = false;

            _oldInterp = _rb.interpolation;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;

            _m.canMove = false;

            _lockedYawDeg = YawDeg(_rb.rotation);

            // *** SIEMPRE ignorar colisión con el obstáculo durante TODO el vault ***
            if (_capsule && p.vaultObstacle)
            {
                Physics.IgnoreCollision(_capsule, p.vaultObstacle, true);
                _ignoredObstacle = p.vaultObstacle;
                if (LogOn) Debug.Log("[VaultState] Ignoring obstacle collider for the whole vault.");
            }
            else
            {
                // fallback si no tenemos collider de obstáculo
                _rb.detectCollisions = false;
                _collisionsOff = true;
                if (LogOn) Debug.Log("[VaultState] detectCollisions OFF (no obstacle/capsule).");
            }

            BuildVaultCurve(p);

            if (UprightWhileVault)
            {
                var target = Quaternion.Euler(0f, _lockedYawDeg, 0f);
                _rb.MoveRotation(target);
            }

            _m.VaultStartEvent();
            _t = 0f;
        }

        public void OnUpdate() { }

        public void OnFixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            _t = Mathf.Min(1f, _t + dt / _dur);

            float sNow  = EaseInOutCubic(_t);
            float sNext = EaseInOutCubic(Mathf.Min(1f, _t + dt / _dur));

            Vector3 posNow  = Bezier2(_p0, _p1, _p2, sNow);
            Vector3 posNext = Bezier2(_p0, _p1, _p2, sNext);
            Vector3 velDesired = (posNext - posNow) / dt;

            // release desde el 60% de la curva
            float gain = 1f;
            if (_t >= ReleaseBlendStart)
            {
                if (!_gravityRestored)
                {
                    _rb.useGravity = true;     // cae “natural”
                    _gravityRestored = true;
                    if (LogOn) Debug.Log($"[VaultState] Gravity ON at t={_t:F2}");
                }

                float u = Mathf.InverseLerp(ReleaseBlendStart, 1f, _t);
                gain = 1f - (u * u * (3f - 2f * u)); // 1 - smoothstep(u)
            }

            // PD sin saturar
            Vector3 posErr = posNow - _rb.position;
            Vector3 velErr = velDesired - _rb.velocity;

            // Al final, no empujar hacia ARRIBA (evita “segunda patada”)
            if (_t >= 0.94f) velErr.y = Mathf.Min(velErr.y, 0f);

            Vector3 accel = (Kp * posErr + Kd * velErr) * gain;
            float aMag = accel.magnitude;
            if (aMag > MaxAccel) accel *= (MaxAccel / aMag);

            _rb.AddForce(accel, ForceMode.Acceleration);

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
            // Rehabilitar colisiones recién al salir del estado
            if (_ignoredObstacle && _capsule)
                Physics.IgnoreCollision(_capsule, _ignoredObstacle, false);

            if (_collisionsOff)
                _rb.detectCollisions = true;

            _rb.useGravity = _oldUseGravity;
            _rb.interpolation = _oldInterp;

            _m.canMove = true;
            _m.VaultEndEvent();

            if (LogOn)
                Debug.Log($"[VaultState] EXIT | endPos={_rb.position:F3} vel={_rb.velocity:F2}");
        }

        private void BuildVaultCurve(in ParkourProbe p)
        {
            float radius = _capsule ? _capsule.radius : (p.playerRadius > 0 ? p.playerRadius : 0.3f);

            _p0 = _rb.position + Vector3.up * StartUpNudge;
            _p2 = p.vaultLandPoint;

            // Step-up si es misma tapa o si excede el max forward
            _stepUpMode = p.vaultLandOnSameCollider ||
                          p.vaultDistance > (_m.Scanner ? _m.Scanner.vaultMaxForward : 2.2f);

            if (_stepUpMode)
            {
                Vector3 insetDir =
                    (p.vaultForward.sqrMagnitude > 0f)
                        ? new Vector3(p.vaultForward.x, 0f, p.vaultForward.z).normalized
                        : (_m.transform.forward - Vector3.Project(_m.transform.forward, Vector3.up)).normalized;

                float minStepUpY = p.vaultTopPoint.y + radius + NormalClearance + StepUpExtraUp;

                _p2 = p.vaultTopPoint + insetDir * StepUpInset;
                _p2.y = Mathf.Max(_p2.y, minStepUpY);
            }

            Vector3 intended = _p2 - _p0; intended.y = 0f;
            Vector3 fwdHint = p.vaultForward; fwdHint.y = 0f;
            if (intended.sqrMagnitude > 1e-6f && fwdHint.sqrMagnitude > 1e-6f &&
                Vector3.Dot(fwdHint, intended) < 0f)
                fwdHint = -fwdHint;

            Vector3 landDir = intended.sqrMagnitude > 1e-6f
                ? intended.normalized
                : (fwdHint.sqrMagnitude > 1e-6f
                    ? fwdHint.normalized
                    : (_m.cameraHolderTransform ? _m.cameraHolderTransform.forward : _m.transform.forward));
            landDir.y = 0f; landDir.Normalize();
            _enterForward = landDir;

            float dxz = Vector3.Distance(
                new Vector3(_p0.x, 0, _p0.z),
                new Vector3(_p2.x, 0, _p2.z));

            // velocidad objetivo del trayecto
            float minVaultSpeed = Mathf.Max(1f, _m.walkingSpeed * 0.85f);
            float maxVaultSpeed = Mathf.Max(minVaultSpeed + 0.1f, _m.runningSpeed * 1.25f);
            _pathHorizSpeed = Mathf.Clamp(_entryHorizSpeed, minVaultSpeed, maxVaultSpeed);

            _dur = Mathf.Clamp(dxz / Mathf.Max(0.1f, _pathHorizSpeed), VaultMinTime, VaultMaxTime);

            // apex: más bajo en step-up
            float apexY = Mathf.Max(0.15f, p.obstacleHeight * ApexFactor + ApexExtra);
            if (_stepUpMode) apexY = Mathf.Min(apexY, StepUpApexCap);

            float sep = radius + NormalClearance;

            Vector3 midXZ = p.vaultMidXZ;
            _p1 = midXZ + p.hitNormal * sep + Vector3.up * apexY;

            // Asegurar tangente inicial hacia adelante
            Vector3 tan0 = Bezier2Tangent(_p0, _p1, _p2, 0.001f);
            tan0.y = 0f;
            if (Vector3.Dot(tan0, landDir) <= 0f)
            {
                float ctrlAhead = Mathf.Max(0.35f, dxz * 0.35f);
                _p1 = _p0 + landDir * ctrlAhead + Vector3.up * apexY;
            }

            if (LogOn)
                Debug.Log($"[VaultState] Curve built | stepUp={_stepUpMode} dxz={dxz:F2} dur={_dur:F2} apexY={apexY:F2} p0={_p0:F3} p1={_p1:F3} p2={_p2:F3}");
        }

        private void ExitToGrounded()
        {
            // No añadimos “turbo” ni tocamos vel.y; solo aseguramos gravedad ON
            _rb.useGravity = true;

            // (opcional) limpiar micro-zigzags numéricos
            Vector3 v = _rb.velocity;
            if (float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z))
                v = Vector3.zero;
            _rb.velocity = v;

            _m.blockVaultUntil = Time.time + _m.vaultRegrabCooldown;
            _fsm.ChangeState(FSM.States.Grounded);
        }

        static float YawDeg(Quaternion q)
        {
            Vector3 f = q * Vector3.forward; f.y = 0f;
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