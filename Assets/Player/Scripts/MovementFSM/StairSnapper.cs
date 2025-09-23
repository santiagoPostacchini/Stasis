using Player.Scripts.MovementFSM.MVC;
using UnityEngine;

namespace Player.Scripts.MovementFSM
{
    public class StairStepper : MonoBehaviour
    {
        [Header("Mask & geometry")] public LayerMask walkableMask;
        public CapsuleCollider capsule; // si es null, lo busca
        public Rigidbody rb; // si es null, lo busca

        [Header("Step settings (CC-style)")] public float maxStepUp = 0.35f; // como stepOffset
        public float maxStepDown = 0.55f;
        public float checkForward = 0.45f; // alcance del sondeo
        public float ankleHeight = 0.12f; // altura del ray a riser
        public float kneeHeight = 0.60f; // altura del ray a clearance

        [Header("Forces")] public float liftDeltaVy = 0.75f; // Δv vertical instantáneo (m/s)
        public float assistAccelXZ = 1.2f; // empuje XZ mientras dura el step (m/s^2)
        public float maxUpVel = 1.3f; // límite v.y
        public float slideBlend = 0.7f; // 0=frontal, 1=total tangencial

        [Header("Gating")] [Range(0f, 1f)] public float approachDotMin = 0.35f; // dot(move,-normal)
        [Range(0f, 0.6f)] public float maxRiserNormalY = 0.27f; // riser “vertical”
        public float snapCooldown = 0.08f;

        [Header("Input (opcional)")] public Model model;
        public bool useRawAxes = true;

        [Header("Steer")] public float steerLerp = 10f;
        public float speedForcesRef = 6f;

        [Header("Input basis (opcional)")] public Transform cameraBasis;
        public float inputToMps = 4f; // z/x [-1..1] -> m/s
        
        [Header("Intent gating")]
        public bool requireMoveInput = true;     // exigir intención
        public float minInputMps     = 0.45f;    // m/s deseados para habilitar step
        public float minActualMps    = 0.35f;    // m/s reales (vel horizontal)
        public float intentGrace     = 0.20f;

        [Header("Debug")] public bool debugDraw;

        // internals
        const float Skin = 0.02f;
        float _lastSnapTime = -999f;
        Vector3 _stepDir;
        float _stepEndTime;
        bool _stepping;
        Vector3 _riserNormal;
        private float _lastMoveIntentTime = -999f;

        void Awake()
        {
            if (!rb) rb = GetComponent<Rigidbody>();
            if (!capsule) capsule = GetComponent<CapsuleCollider>();
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }

        void FixedUpdate()
        {
            Vector3 moveDir = GetWishDir(out var wishSpeed);
            
            if (wishSpeed >= minInputMps) _lastMoveIntentTime = Time.time;
            
            float actualMps = new Vector3(rb.velocity.x, 0f, rb.velocity.z).magnitude;
            bool hasRecentIntent = (Time.time - _lastMoveIntentTime) <= intentGrace;
            bool allowStep = (!requireMoveInput) || hasRecentIntent || (actualMps >= minActualMps);

            if (_stepping)
            {
                ContinueStep(moveDir);
                return;
            }

            if (!allowStep) { TrySnapDown(moveDir); return; }

            Vector3 foot = BottomSphereCenter();
            Vector3 ankle = foot + Vector3.up * (ankleHeight + Skin);
            Vector3 knee = foot + Vector3.up * (kneeHeight + Skin);

            if (!Physics.Raycast(ankle, moveDir, out RaycastHit riserHit, checkForward, walkableMask,
                    QueryTriggerInteraction.Ignore))
            {
                TrySnapDown(moveDir);
                return;
            }

            // descartamos pendientes no-riser
            if (riserHit.normal.y > maxRiserNormalY)
            {
                TrySnapDown(moveDir);
                return;
            }

            // debe venir “de frente”
            float approachDot = Vector3.Dot(moveDir, -riserHit.normal);
            if (approachDot < approachDotMin)
            {
                TrySnapDown(moveDir);
                return;
            }

            // 2) clearance a rodilla debe estar libre
            if (Physics.Raycast(knee, moveDir, checkForward, walkableMask, QueryTriggerInteraction.Ignore))
            {
                TrySnapDown(moveDir);
                return;
            }

            // 3) buscar la tapa por encima del borde
            Vector3 over = riserHit.point + moveDir * 0.16f + Vector3.up * (maxStepUp + Skin);
            if (!Physics.Raycast(over, Vector3.down, out RaycastHit top, maxStepUp + 2f * Skin, walkableMask,
                    QueryTriggerInteraction.Ignore))
            {
                TrySnapDown(moveDir);
                return;
            }

            // listo: hay step válido → subimos tipo CC
            BeginStep(moveDir, riserHit.normal, top.point.y + Skin);
        }

        void BeginStep(Vector3 moveDir, Vector3 riserNormal, float topY)
        {
            // 1) micro-lift instantáneo
            rb.AddForce(Vector3.up * liftDeltaVy, ForceMode.VelocityChange);
            ClampUp();

            // 2) guardar dirección “deslizante” (proyectada sobre el plano de la riser)
            Vector3 tangential = moveDir - Vector3.Project(moveDir, riserNormal);
            _stepDir = Vector3.Lerp(moveDir, tangential, slideBlend).normalized;

            // 3) fijar una ventana corta de asistencia (como si CC te empujara un poco)
            _stepEndTime =
                Time.time + Mathf.Clamp((topY - rb.position.y) / Mathf.Max(0.08f, liftDeltaVy), 0.06f, 0.22f);
            _stepping = true;
            _lastSnapTime = Time.time;


            _riserNormal = riserNormal;

            if (debugDraw)
            {
                Debug.DrawRay(BottomSphereCenter() + Vector3.up * (ankleHeight + Skin), _stepDir * 0.35f, Color.cyan,
                    0.1f);
                Debug.DrawRay(rb.position, riserNormal * 0.25f, Color.magenta, 0.15f);
            }
        }

        void ContinueStep(Vector3 moveDir)
        {
            Vector3 tangentialInput = moveDir - Vector3.Project(moveDir, _riserNormal);
            Vector3 targetDir = tangentialInput.sqrMagnitude > 1e-5f ? tangentialInput.normalized : _stepDir;

            _stepDir = Vector3.Slerp(_stepDir, targetDir, Mathf.Clamp01(steerLerp * Time.fixedDeltaTime));
            
            GetWishDir(out var wishSpeed);
            float speed01 = Mathf.Clamp01(wishSpeed / Mathf.Max(0.1f, speedForcesRef));

            float accel = assistAccelXZ * Mathf.Lerp(0.5f, 1f, speed01);
            rb.AddForce(_stepDir * accel, ForceMode.Acceleration);
            
            ClampUp();
            
            if (Time.time >= _stepEndTime)
                _stepping = false;
        }

        void TrySnapDown(Vector3 moveDir)
        {
            if ((Time.time - _lastSnapTime) < snapCooldown) return;

            Vector3 foot = BottomSphereCenter();
            Vector3 ahead = foot + moveDir * (checkForward * 0.6f) + Vector3.up * (maxStepDown + Skin);
            if (!Physics.Raycast(ahead, Vector3.down, out RaycastHit hit, maxStepDown + 2f * Skin, walkableMask,
                    QueryTriggerInteraction.Ignore))
                return;

            float targetY = hit.point.y + Skin;
            float dy = targetY - rb.position.y;
            if (dy >= -0.03f) return;
            
            float downDeltaVy = Mathf.Clamp(dy / Time.fixedDeltaTime, -2.5f, 0f);
            rb.AddForce(Vector3.up * downDeltaVy, ForceMode.VelocityChange);
            _lastSnapTime = Time.time;
        }

        Vector3 BottomSphereCenter()
        {
            var cap = capsule ? capsule : GetComponent<CapsuleCollider>();
            float r = cap.radius;
            float half = cap.height * 0.5f - r;
            Vector3 c = transform.TransformPoint(cap.center);
            return new Vector3(c.x, c.y - half, c.z);
        }

        Vector3 GetWishDir(out float wishSpeed)
        {
            float x = 0f, z = 0f;

            if (model)
            {
                x = useRawAxes ? model.rawX : model.xAxis;
                z = useRawAxes ? model.rawZ : model.zAxis;
            }

            Transform basis = cameraBasis ? cameraBasis : transform;
            Vector3 f = Vector3.ProjectOnPlane(basis.forward, Vector3.up).normalized;
            Vector3 r = Vector3.ProjectOnPlane(basis.right, Vector3.up).normalized;

            Vector3 wish = f * z + r * x;
            if (wish.sqrMagnitude > 1e-5f)
            {
                wishSpeed = Mathf.Clamp01(wish.magnitude) * inputToMps;
                return wish.normalized;
            }
            
            Vector3 hv = rb.velocity;
            hv.y = 0f;
            if (hv.sqrMagnitude > 1e-5f)
            {
                wishSpeed = hv.magnitude;
                return hv.normalized;
            }

            wishSpeed = 0f;
            return basis.forward;
        }

        void ClampUp()
        {
            var v = rb.velocity;
            if (v.y > maxUpVel)
            {
                v.y = maxUpVel;
                rb.velocity = v;
            }
        }

        void OnValidate()
        {
            maxStepDown = Mathf.Max(maxStepDown, maxStepUp + 0.05f);
            checkForward = Mathf.Max(0.1f, checkForward);
            ankleHeight = Mathf.Max(-0.3f, ankleHeight);
            kneeHeight = Mathf.Max(0.5f, kneeHeight);
            assistAccelXZ = Mathf.Max(0f, assistAccelXZ);
            liftDeltaVy = Mathf.Max(0.05f, liftDeltaVy);
            maxUpVel = Mathf.Max(0.6f, maxUpVel);
        }
    }
}