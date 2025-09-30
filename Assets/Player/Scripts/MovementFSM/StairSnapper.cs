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
        
        [Header("Low ramp-as-step")]
        public bool enableLowRampStep = true;
        [Tooltip("Qué tan adelante muestreamos la altura de 'tapa'")]
        public float rampProbeAhead = 0.28f;
        [Tooltip("Altura desde la cual casteamos hacia abajo para hallar 'tapa'")]
        public float rampTopProbeUp = 0.25f;
        [Tooltip("Normal.y mínima aceptable para considerar la 'tapa' caminable")]
        [Range(0f,1f)] public float topMinNormalY = 0.25f; // 0.25 ~ 75°

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

        [Header("Intent gating")] public bool requireMoveInput = true; // exigir intención
        public float minInputMps = 0.45f; // m/s deseados para habilitar step
        public float minActualMps = 0.35f; // m/s reales (vel horizontal)
        public float intentGrace = 0.20f;

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
            if (!model) model = GetComponent<Model>();
            if (model) SyncFromModel(model);
        }
        
        void FixedUpdate()
        {
            Vector3 moveDir = GetWishDir(out var wishSpeed);

            if (wishSpeed >= minInputMps) _lastMoveIntentTime = Time.time;

            float actualMps = new Vector3(rb.velocity.x, 0f, rb.velocity.z).magnitude;
            bool hasRecentIntent = (Time.time - _lastMoveIntentTime) <= intentGrace;
            bool allowStep = (!requireMoveInput) || hasRecentIntent || (actualMps >= minActualMps);
            
            if (enableLowRampStep && TryLowRampAsStep(moveDir, out var topY, out var fakeRiserNormal))
            {
                BeginStep(moveDir, fakeRiserNormal, topY);
                return;
            }

            if (_stepping)
            {
                ContinueStep(moveDir);
                return;
            }

            if (!allowStep)
            {
                TrySnapDown(moveDir);
                return;
            }

            Vector3 foot = BottomSphereCenter();
            Vector3 ankle = foot + Vector3.up * (ankleHeight + Skin);
            Vector3 knee = foot + Vector3.up * (kneeHeight + Skin);

            const float riserProbeRadius = 0.05f; // pequeño, evita falsos positivos
            if (!Physics.SphereCast(ankle, riserProbeRadius, moveDir, out RaycastHit riserHit, checkForward, walkableMask,
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
        
        bool TryLowRampAsStep(Vector3 moveDir, out float topY, out Vector3 fakeRiserNormal)
        {
            topY = 0f;
            fakeRiserNormal = Vector3.zero;

            // 1) punto adelante en XZ donde “queremos” estar
            Vector3 foot = BottomSphereCenter();
            Vector3 aheadXZ = foot + moveDir.normalized * Mathf.Clamp(rampProbeAhead, 0.1f, checkForward);

            // 2) ray hacia abajo para encontrar la “tapa” (aunque sea inclinada)
            Vector3 downOrigin = new Vector3(aheadXZ.x, foot.y + maxStepUp + rampTopProbeUp, aheadXZ.z);
            if (!Physics.Raycast(downOrigin, Vector3.down, out RaycastHit topHit,
                    maxStepUp + rampTopProbeUp + 0.1f, walkableMask,
                    QueryTriggerInteraction.Ignore))
                return false;

            // 3) que la “tapa” sea físicamente alcanzable como step
            float candidateTopY = topHit.point.y + Skin;
            float dy = candidateTopY - rb.position.y;
            if (dy < 0.02f || dy > maxStepUp + 0.001f) return false;

            // 4) y que no sea una pared total (aceptamos inclinadas fuertes, pero no techo)
            if (topHit.normal.y < topMinNormalY) return false;

            // 5) clearance a rodilla entre acá y ahead (evitar clavarnos en la cara de la rampa)
            Vector3 knee = foot + Vector3.up * (kneeHeight + Skin);
            if (Physics.Raycast(knee, moveDir, rampProbeAhead, walkableMask, QueryTriggerInteraction.Ignore))
                return false;

            // 6) "riser" sintético: usamos la componente frontal opuesta a la marcha
            // para construir un plano tangencial donde deslizar durante el step
            fakeRiserNormal = Vector3.ProjectOnPlane(-moveDir, Vector3.up).normalized;
            if (fakeRiserNormal.sqrMagnitude < 1e-5f) fakeRiserNormal = Vector3.forward; // fallback estable

            topY = candidateTopY;
            return true;
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
        
        public bool IsStepping => _stepping;
        public float LastSnapTime => _lastSnapTime;

        public void SyncFromModel(Model m)
        {
            model = m;
            if (!cameraBasis) cameraBasis = m ? m.cameraHolderTransform : null;
            if (walkableMask.value == 0 && m) walkableMask = m.groundMask;
            if (!rb) rb = m ? m.rb : GetComponent<Rigidbody>();
            if (!capsule) capsule = GetComponent<CapsuleCollider>();
        }

        public bool RecentlySnapped(float graceSeconds = 0.06f)
            => (Time.time - _lastSnapTime) <= Mathf.Max(0f, graceSeconds);


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