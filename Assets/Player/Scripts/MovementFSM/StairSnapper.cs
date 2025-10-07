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

        [Header("Riser vertical sweep")] public bool enableVerticalRiserSweep = true;

        [Tooltip("Radio del probe para el CapsuleCast entre ankle y knee")]
        public float riserCapsuleRadius = 0.06f;

        [Header("Low ramp-as-step")] public bool enableLowRampStep = true;

        [Tooltip("Qué tan adelante muestreamos la altura de 'tapa'")]
        public float rampProbeAhead = 0.28f;

        [Tooltip("Altura desde la cual casteamos hacia abajo para hallar 'tapa'")]
        public float rampTopProbeUp = 0.25f;

        [Tooltip("Normal.y mínima aceptable para considerar la 'tapa' caminable")] [Range(0f, 1f)]
        public float topMinNormalY = 0.25f; // 0.25 ~ 75°

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

        [Header("Height-invariant rise")] public bool heightInvariantRise = true;

        [Tooltip("Duración fija de la subida, sin importar la altura del escalón")] [Range(0.06f, 0.30f)]
        public float riseTime = 0.12f;

        [Tooltip("Desactivar gravedad durante la subida para trayectoria exacta")]
        public bool disableGravityDuringRise = true;

        [Tooltip("Ajuste final de posición vertical al terminar la subida")]
        public float snapTopEpsilon = 0.01f;

        [Tooltip("Terminar la subida con vy=0 usando perfil bi-fase (recomendado)")]
        public bool zeroVyAtTop = true;
        
        [Header("Snap smoothing")]
        public bool smoothSnapDown = true;
        [Range(0.02f, 0.25f)] public float snapBlendTime = 0.10f; // duración del aterrizaje suave
        [Range(1f, 20f)] public float snapSpring = 10f;           // fuerza hacia la tapa
        [Range(0f, 2f)] public float snapDamping = 0.8f;   
        
        bool _snapBlending;
        float _snapTargetY;
        float _snapBlendUntil;
        float _riseAUp; // módulo de aceleración en la 1ª mitad; en la 2ª se aplica -_riseAUp
        float _riseMidTime; // fin de la 1ª mitad
        float _plannedHeight; // s = topY - y0 (para escalar asistencia XZ)
        float _riseA; // aceleración vertical (net) planificada
        float _riseEndTime; // fin de la subida
        float _targetTopY; // altura objetivo del escalón
        bool _rising; // estamos en fase de subida (subconjunto de _stepping)
        bool _prevUseGravity; // para restaurar gravedad
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
            
            if (_snapBlending)
            {
                float now = Time.time;
                
                float e = _snapTargetY - rb.position.y;

                float vy = rb.velocity.y;
                float a  = snapSpring * e - snapDamping * vy;

                a = Mathf.Clamp(a, -30f, 12f);

                rb.AddForce(Vector3.up * a, ForceMode.Acceleration);
                
                bool closeEnough = Mathf.Abs(e) < 0.005f;
                if (closeEnough || now >= _snapBlendUntil)
                    _snapBlending = false;

                return;
            }

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

            // --- NUEVA DETECCIÓN DE RISER ---
            if (!TryFindRiser(moveDir, ankle, knee, out RaycastHit riserHit))
            {
                TrySnapDown(moveDir);
                return;
            }

            // descartamos pendientes no-riser (no “pared”)
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

            // 3) buscar la tapa por encima del borde (igual que antes)
            Vector3 over = riserHit.point + moveDir * 0.16f + Vector3.up * (maxStepUp + Skin);
            if (!Physics.Raycast(over, Vector3.down, out RaycastHit top, maxStepUp + 2f * Skin, walkableMask,
                    QueryTriggerInteraction.Ignore))
            {
                // pequeño “ajuste” por si el punto está muy pegado a la cara
                Vector3 over2 = riserHit.point + moveDir * 0.10f + Vector3.up * (maxStepUp + Skin);
                if (!Physics.Raycast(over2, Vector3.down, out top, maxStepUp + 2f * Skin, walkableMask,
                        QueryTriggerInteraction.Ignore))
                {
                    TrySnapDown(moveDir);
                    return;
                }
            }

            // listo: hay step válido → subimos tipo CC (misma cinemática de siempre)
            BeginStep(moveDir, riserHit.normal, top.point.y + Skin);
        }

        // NEW: barrido vertical del riser entre ankle y knee.
        // Si está desactivado, cae al SphereCast clásico a la altura del ankle.
        bool TryFindRiser(Vector3 moveDir, Vector3 ankle, Vector3 knee, out RaycastHit riserHit)
        {
            if (enableVerticalRiserSweep)
            {
                // CapsuleCast vertical adelante — capta caras entre ankle y knee
                if (Physics.CapsuleCast(
                        ankle, knee, riserCapsuleRadius,
                        moveDir, out riserHit, checkForward, walkableMask,
                        QueryTriggerInteraction.Ignore))
                {
                    if (debugDraw)
                    {
                        Debug.DrawLine(ankle, knee, Color.yellow, 0.05f);
                        Debug.DrawRay((ankle + knee) * 0.5f, moveDir.normalized * Mathf.Min(checkForward, 0.4f),
                            Color.yellow, 0.05f);
                    }

                    return true;
                }
            }
            else
            {
                const float riserProbeRadius = 0.05f;
                if (Physics.SphereCast(ankle, riserProbeRadius, moveDir, out riserHit, checkForward, walkableMask,
                        QueryTriggerInteraction.Ignore))
                {
                    if (debugDraw)
                        Debug.DrawRay(ankle, moveDir.normalized * Mathf.Min(checkForward, 0.4f), Color.cyan, 0.05f);
                    return true;
                }
            }

            riserHit = default;
            return false;
        }

        void BeginStep(Vector3 moveDir, Vector3 riserNormal, float topY)
        {
            _riserNormal = riserNormal;
            _targetTopY = topY;

            // Dirección deslizante (como antes)
            Vector3 tangential = moveDir - Vector3.Project(moveDir, riserNormal);
            _stepDir = Vector3.Lerp(moveDir, tangential, slideBlend).normalized;

            // Ventana de asistencia XZ (como antes, pero guardamos altura planeada)
            _plannedHeight = Mathf.Max(0f, topY - rb.position.y);
            _stepEndTime = Time.time + Mathf.Clamp(_plannedHeight / Mathf.Max(0.08f, liftDeltaVy), 0.06f, 0.22f);

            if (heightInvariantRise && _plannedHeight > 0f)
            {
                float t = Mathf.Max(0.06f, riseTime);
                _prevUseGravity = rb.useGravity;
                if (disableGravityDuringRise) rb.useGravity = false;

                if (zeroVyAtTop)
                {
                    // Perfil bi-fase simétrico (+a, -a) que termina con vy=0 y recorre s en tiempo t:
                    // s = a * t^2 / 4  =>  a = 4*s/t^2
                    _riseAUp = 4f * _plannedHeight / (t * t);
                    _riseMidTime = Time.time + t * 0.5f;
                }
                else
                {
                    // Perfil de a constante (el anterior)
                    float vy0 = rb.velocity.y;
                    _riseAUp = 2f * (_plannedHeight - vy0 * t) / (t * t); // se usa como 'a' única
                    _riseMidTime = -1f; // no se usa
                }

                _riseEndTime = Time.time + t;
                _rising = true;
            }
            else
            {
                // Modo clásico
                rb.AddForce(Vector3.up * liftDeltaVy, ForceMode.VelocityChange);
                ClampUp();
                _rising = false;
            }

            _stepping = true;
            _lastSnapTime = Time.time;

            if (debugDraw)
            {
                Debug.DrawRay(BottomSphereCenter() + Vector3.up * (ankleHeight + 0.02f), _stepDir * 0.35f, Color.cyan,
                    0.1f);
                Debug.DrawRay(rb.position, riserNormal * 0.25f, Color.magenta, 0.15f);
            }
        }

        void ContinueStep(Vector3 moveDir)
        {
            // ---- Horizontal (igual que antes, pero con escala por altura) ----
            Vector3 tangentialInput = moveDir - Vector3.Project(moveDir, _riserNormal);
            Vector3 targetDir = tangentialInput.sqrMagnitude > 1e-5f ? tangentialInput.normalized : _stepDir;
            _stepDir = Vector3.Slerp(_stepDir, targetDir, Mathf.Clamp01(steerLerp * Time.fixedDeltaTime));

            GetWishDir(out var wishSpeed);

            // Escalá la asistencia según la altura planeada: escalón chico => menos empuje
            float h01 = Mathf.Clamp01(_plannedHeight / Mathf.Max(0.0001f, maxStepUp));
            float speed01 = Mathf.Clamp01(wishSpeed / Mathf.Max(0.1f, speedForcesRef));
            float accelXZ = assistAccelXZ * Mathf.Lerp(0.35f, 1f, h01) * Mathf.Lerp(0.5f, 1f, speed01);
            rb.AddForce(_stepDir * accelXZ, ForceMode.Acceleration);

            // ---- Vertical (perfil isócrono) ----
            if (_rising)
            {
                if (Time.time < _riseEndTime)
                {
                    if (zeroVyAtTop)
                    {
                        // 1ª mitad: +a ; 2ª mitad: -a  (compensando gravedad si sigue encendida)
                        bool firstHalf = Time.time < _riseMidTime;
                        float aNet = firstHalf ? _riseAUp : -_riseAUp;
                        float aCmd = disableGravityDuringRise ? aNet : (aNet - Physics.gravity.y);
                        rb.AddForce(Vector3.up * aCmd, ForceMode.Acceleration);
                    }
                    else
                    {
                        // a constante (versión anterior)
                        float aNet = _riseAUp;
                        float aCmd = disableGravityDuringRise ? aNet : (aNet - Physics.gravity.y);
                        rb.AddForce(Vector3.up * aCmd, ForceMode.Acceleration);
                    }
                }
                else
                {
                    // Fin de subida: posar suave en la tapa y “apagar” vy hacia arriba
                    if (disableGravityDuringRise) rb.useGravity = _prevUseGravity;

                    // Snap suave a top
                    Vector3 p = rb.position;
                    float dy = _targetTopY - p.y;
                    p.y = Mathf.Abs(dy) <= snapTopEpsilon ? _targetTopY : Mathf.MoveTowards(p.y, _targetTopY, Mathf.Abs(dy));
                    rb.position = p;

                    // Certeza de no “salir volando”
                    var v = rb.velocity;
                    if (v.y > 0f) v.y = 0f;
                    rb.velocity = v;

                    _rising = false;
                }
            }
            else
            {
                // Modo clásico
                ClampUp();
            }

            if (Time.time >= _stepEndTime)
                _stepping = _rising; // mantené mientras dure la subida
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

            _lastSnapTime = Time.time;

            if (smoothSnapDown)
            {
                _snapTargetY    = targetY;
                _snapBlendUntil = Time.time + Mathf.Max(0.02f, snapBlendTime);
                _snapBlending   = true;
            }
            else
            {
                float downDeltaVy = Mathf.Clamp(dy / Time.fixedDeltaTime, -2.5f, 0f);
                rb.AddForce(Vector3.up * downDeltaVy, ForceMode.VelocityChange);
            }
        }

        bool TryLowRampAsStep(Vector3 moveDir, out float topY, out Vector3 fakeRiserNormal)
        {
            topY = 0f;
            fakeRiserNormal = Vector3.zero;

            Vector3 foot = BottomSphereCenter();
            Vector3 aheadXZ = foot + moveDir.normalized * Mathf.Clamp(rampProbeAhead, 0.1f, checkForward);

            Vector3 downOrigin = new Vector3(aheadXZ.x, foot.y + maxStepUp + rampTopProbeUp, aheadXZ.z);
            if (!Physics.Raycast(downOrigin, Vector3.down, out RaycastHit topHit,
                    maxStepUp + rampTopProbeUp + 0.1f, walkableMask,
                    QueryTriggerInteraction.Ignore))
                return false;

            float candidateTopY = topHit.point.y + Skin;
            float dy = candidateTopY - rb.position.y;
            if (dy < 0.02f || dy > maxStepUp + 0.001f) return false;

            if (topHit.normal.y < topMinNormalY) return false;

            Vector3 knee = foot + Vector3.up * (kneeHeight + Skin);
            if (Physics.Raycast(knee, moveDir, rampProbeAhead, walkableMask, QueryTriggerInteraction.Ignore))
                return false;

            fakeRiserNormal = Vector3.ProjectOnPlane(-moveDir, Vector3.up).normalized;
            if (fakeRiserNormal.sqrMagnitude < 1e-5f) fakeRiserNormal = Vector3.forward;

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

        void OnDisable()
        {
            if (_rising && disableGravityDuringRise && rb) rb.useGravity = _prevUseGravity;
            _rising = false;
            _stepping = false;
        }

        void OnValidate()
        {
            maxStepDown = Mathf.Max(maxStepDown, maxStepUp + 0.05f);
            checkForward = Mathf.Max(0.1f, checkForward);
            ankleHeight = Mathf.Max(-0.3f, ankleHeight);
            kneeHeight = Mathf.Max(0.1f, kneeHeight);
            assistAccelXZ = Mathf.Max(0f, assistAccelXZ);
            liftDeltaVy = Mathf.Max(0.05f, liftDeltaVy);
            maxUpVel = Mathf.Max(0.6f, maxUpVel);
            riserCapsuleRadius = Mathf.Clamp(riserCapsuleRadius, 0.02f, 0.2f);
        }
    }
}