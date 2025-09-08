using Player.Scripts.MovementFSM.MVC;
using UnityEngine;

namespace Player.Scripts.MovementFSM
{
    public class StairStepper : MonoBehaviour
    {
        [Header("Máscara")]
        public LayerMask walkableMask; // 0 => usa model.groundMask

        [Header("Alturas")]
        public float maxStepUp   = 0.35f;
        public float maxStepDown = 0.55f;

        [Header("Sondeo (riser + tapa)")]
        public float ankleHeight    = 0.12f; // >= 0.06
        public float kneeHeight     = 0.60f; // 0.55–0.70
        public float checkForward   = 0.45f;
        public float riserOvershoot = 0.22f;

        [Header("Ascenso (fluidez)")]
        public float climbYRate = 1.2f;  // m/s vertical al subir
        public float maxUpVel   = 1.2f;  // cap de v.y positiva

        [Header("Movimiento de paso (feeling)")]
        [Tooltip("Impulso XZ único al iniciar el step (consuma el escalón)")]
        public float commitForward = 0.06f;          // m (una sola vez)
        [Tooltip("Arrastre XZ POR SEGUNDO mientras dura el step")]
        public float forwardWhileStepPerSec = 0.35f; // m/s (se multiplica por dt)
        public float settleDampTime = 0.06f;         // para bajar
        public float snapCooldown   = 0.12f;

        [Header("Filtros / Estados")]
        [Range(0f, 0.6f)] public float maxRiserNormalY = 0.27f;
        public float minDownGap  = 0.03f;
        public float deadbandY   = 0.012f;
        public float freeFallVy  = -2.0f;

        [Header("Requisito: caminar hacia el escalón")]
        [Range(0f,1f)] public float approachDotMin = 0.40f; // dot(moveDir,-normalRiser)
        public float inputSpeedMin = 0.6f;   // m/s desde input
        public float inputToMps    = 4.0f;   // ≈ walkingSpeed

        [Header("Base de movimiento (independiente del look)")]
        public Transform cameraBasis;
        public bool useRawAxes = true;

        [Header("Debug")] public bool debugDraw;

        // ---- internos
        Model _m; Rigidbody _rb; CapsuleCollider _cap;
        private const float Skin = 0.02f;
        float _cosMaxSlope;
        float _yVel; // bajar
        float _lastSnapTime = -999f;

        // Latch del step
        bool _isStepping, _didCommit;
        float _targetTopY, _stepStartTime;
        Vector3 _stepDir;
        public float stepTimeout = 0.6f;

        void Awake()
        {
            _m  = GetComponent<Model>();
            _rb = _m.rb;
            _cap = _m.capsule;

            if (walkableMask.value == 0) walkableMask = _m.groundMask;
            _cosMaxSlope = Mathf.Cos(Mathf.Deg2Rad * Mathf.Clamp(_m.maxGroundSlope, 0f, 89f));

            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }

        // LLAMAR al final de Model.FixedUpdate()
        public void ManualFixedStep()
        {
            bool grounded = _m.IsGroundedNow();
            if (_rb.velocity.y < freeFallVy && !grounded) { _isStepping = _didCommit = false; return; }

            GetMoveDirAndSpeedFromInput(out var moveDir, out var inputSpeed);

            if (_isStepping) { ContinueLatchedStep(); return; }

            bool walkingByInput = (moveDir != Vector3.zero) && (inputSpeed >= inputSpeedMin);
            if (walkingByInput && (Time.time - _lastSnapTime) > snapCooldown)
            {
                if (TryBeginLatchedStep(moveDir))
                {
                    _lastSnapTime = Time.time;
                    ContinueLatchedStep();
                    return;
                }
            }

            TrySnapDown(moveDir);
        }

        bool TryBeginLatchedStep(Vector3 moveDir)
        {
            Vector3 footBase = BottomSphereCenter();
            Vector3 ankle = footBase + Vector3.up * (ankleHeight + Skin);
            Vector3 knee  = footBase + Vector3.up * (kneeHeight  + Skin);

            if (!Physics.Raycast(ankle, moveDir, out RaycastHit riserHit, checkForward, walkableMask, QueryTriggerInteraction.Ignore))
                return false;
            if (riserHit.normal.y > maxRiserNormalY) return false;

            float approachDot = Vector3.Dot(moveDir, -riserHit.normal);
            if (approachDot < approachDotMin) return false;

            if (Physics.Raycast(knee, moveDir, out _, checkForward, walkableMask, QueryTriggerInteraction.Ignore))
                return false;

            Vector3 overEdge = riserHit.point + moveDir * riserOvershoot + Vector3.up * (maxStepUp + Skin);
            if (!Physics.Raycast(overEdge, Vector3.down, out RaycastHit top, maxStepUp + 2f * Skin, walkableMask, QueryTriggerInteraction.Ignore))
                return false;
            if (top.normal.y < _cosMaxSlope) return false;

            float targetY  = top.point.y + Skin;
            if (Mathf.Abs(targetY - _rb.position.y) <= deadbandY) return false;

            _isStepping   = true;
            _didCommit    = false;
            _targetTopY   = targetY;
            _stepDir      = moveDir;
            _stepStartTime = Time.time;

            if (debugDraw)
            {
                float d = 0.12f;
                Debug.DrawLine(ankle, riserHit.point, Color.cyan, d);
                Debug.DrawLine(overEdge, top.point, Color.yellow, d);
                Debug.DrawRay(top.point, top.normal * 0.25f, Color.green, d);
            }
            return true;
        }

        void ContinueLatchedStep()
        {
            // Y sube a rate constante
            float y = Mathf.MoveTowards(_rb.position.y, _targetTopY, climbYRate * Time.fixedDeltaTime);

            // Commit XZ una sola vez
            Vector3 commitXZ = (!_didCommit && commitForward > 0f) ? _stepDir * commitForward : Vector3.zero;
            _didCommit = true;

            // Drift XZ por segundo (¡escalado por dt!)
            Vector3 driftXZ = _stepDir * (forwardWhileStepPerSec * Time.fixedDeltaTime);

            _rb.MovePosition(new Vector3(_rb.position.x, y, _rb.position.z) + commitXZ + driftXZ);

            // Permite algo de v.y positiva, con cap.
            Vector3 v = _rb.velocity; v.y = Mathf.Clamp(v.y, -0.5f, maxUpVel); _rb.velocity = v;

            if (Mathf.Abs(_targetTopY - y) <= deadbandY || (Time.time - _stepStartTime) > stepTimeout)
                _isStepping = _didCommit = false;
        }

        void TrySnapDown(Vector3 moveDir)
        {
            Vector3 lookAhead = (moveDir == Vector3.zero)
                ? Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized
                : moveDir;

            Vector3 footBase = BottomSphereCenter();
            Vector3 ahead = footBase + lookAhead * (checkForward * 0.6f) + Vector3.up * (maxStepDown + Skin);

            if (!Physics.Raycast(ahead, Vector3.down, out RaycastHit down, maxStepDown + 2f * Skin, walkableMask, QueryTriggerInteraction.Ignore))
                return;
            if (down.normal.y < _cosMaxSlope) return;

            float targetY  = down.point.y + Skin;
            float currentY = _rb.position.y;
            if ((currentY - targetY) <= minDownGap) return;

            float newY = Mathf.MoveTowards(currentY, targetY, (climbYRate * 1.25f) * Time.fixedDeltaTime);
            _rb.MovePosition(new Vector3(_rb.position.x, newY, _rb.position.z));

            Vector3 v = _rb.velocity; v.y = Mathf.Clamp(v.y, -2f, maxUpVel); _rb.velocity = v;

            if (debugDraw)
            {
                float d = 0.12f;
                Debug.DrawLine(ahead, down.point, Color.blue, d);
                Debug.DrawRay(down.point, down.normal * 0.25f, Color.green, d);
            }
        }

        Vector3 BottomSphereCenter()
        {
            float r = _cap.radius;
            float half = _cap.height * 0.5f - r;
            Vector3 c = transform.TransformPoint(_cap.center);
            return new Vector3(c.x, c.y - half, c.z);
        }

        void GetMoveDirAndSpeedFromInput(out Vector3 moveDir, out float inputSpeedMps)
        {
            float x = useRawAxes ? _m.rawX : _m.xAxis;
            float z = useRawAxes ? _m.rawZ : _m.zAxis;

            Transform basis = cameraBasis ? cameraBasis : transform;
            Vector3 f = Vector3.ProjectOnPlane(basis.forward, Vector3.up).normalized;
            Vector3 r = Vector3.ProjectOnPlane(basis.right,   Vector3.up).normalized;

            Vector3 wish = f * z + r * x; float mag = wish.magnitude;
            if (mag > 0.0001f) { moveDir = wish / mag; inputSpeedMps = mag * inputToMps; }
            else               { Vector3 hv = _rb.velocity; hv.y = 0f; moveDir = hv.sqrMagnitude>0.0001f? hv.normalized:Vector3.zero; inputSpeedMps = hv.magnitude; }
        }

        void OnValidate()
        {
            ankleHeight    = Mathf.Max(-0.3f, ankleHeight);
            kneeHeight     = Mathf.Max(0.30f, kneeHeight);
            checkForward   = Mathf.Max(0.1f, checkForward);
            riserOvershoot = Mathf.Max(0.05f, riserOvershoot);
            forwardWhileStepPerSec = Mathf.Clamp(forwardWhileStepPerSec, 0f, 1.0f);
            commitForward  = Mathf.Clamp(commitForward, 0f, 0.20f);
            maxStepDown    = Mathf.Max(maxStepUp + 0.05f, maxStepDown);
            deadbandY      = Mathf.Clamp(deadbandY, 0.005f, 0.03f);
            inputToMps     = Mathf.Max(0.1f, inputToMps);
        }
    }
}
