using UnityEngine;

namespace Player.Scripts.MovementFSM
{
    [DefaultExecutionOrder(50)]
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public class StairStepper : MonoBehaviour
    {
        [Header("Mask & Refs")] public LayerMask walkableMask;
        public Rigidbody rb;
        public CapsuleCollider capsule;

        [Header("Step geometry")]
        [Tooltip("Altura máxima escalón (m).")]
        public float maxStepUp = 0.35f;

        [Tooltip("Altura mínima que consideramos 'step' (m). Permite microescalones reales.")]
        public float minStepUp = 0.005f; // NUEVO (antes 0.02f implícito)

        [Tooltip("Caída suave máxima (m).")]
        public float maxStepDown = 0.55f;

        [Tooltip("Alcance base hacia adelante (m).")]
        public float checkForward = 0.45f;

        [Tooltip("Altura ankle del probe (m).")]
        public float ankleHeight = 0.12f;

        [Tooltip("Altura knee del probe (m).")]
        public float kneeHeight = 0.60f;

        [Tooltip("Radio de los probes (Sphere/Capsule).")]
        public float probeRadius = 0.06f;

        [Header("Riser gating / ángulos")]
        [Tooltip("dot(move,-normal) mínimo (encarar el escalón).")]
        [Range(0f, 1f)] public float approachDotMin = 0.35f;

        [Tooltip("Descarta pendientes poco verticales (riser). Máx normal.y aceptada.")]
        [Range(0f, 0.6f)] public float maxRiserNormalY = 0.27f;

        [Tooltip("Sesgo hacia abajo para evitar pegarle a la tapa (edge de BoxCollider).")]
        [Min(0f)] public float edgeBiasDown = 0.02f; // NUEVO

        [Header("Detección robusta")]
        public bool enableVerticalRiserSweep = true; // CapsuleCast entre ankle y knee

        [Tooltip("Cuánto el alcance se incrementa con la velocidad.")]
        public float speedCompForward = 6f; // m/s * dt * factor

        [Tooltip("Usar transform.forward si la velocidad es menor a…")]
        public float probeUseVelMin = 0.15f; // m/s

        [Tooltip("Radio relativo del upper-clearance (knee).")]
        [Range(0.4f, 1f)] public float kneeClearanceRadiusScale = 0.6f;

        [Header("Ledge suspendido")]
        public bool enableLedgeProbe = true;

        [Tooltip("Offset adelante base (m) para muestrear la tapa.")]
        public float ledgeAhead = 0.28f;

        [Tooltip("Cuánto más arriba casteamos el ray down.")]
        public float ledgeDownFromUp = 0.25f;

        [Range(0f, 1f)] public float topMinNormalY = 0.25f;

        [Header("Ascent (tiempo constante)")]
        [Tooltip("Duración fija de subida (s), independiente de la altura.")]
        public float stepTime = 0.12f;

        [Tooltip("Pequeña ayuda XZ durante el step (m/s^2).")]
        public float assistAccelXZ = 1.0f;

        [Tooltip("Velocidad vertical máxima mientras sube (m/s).")]
        public float clampUpVel = 2.2f;

        [Header("Snap-down suave")] public float snapTime = 0.06f;
        public float snapSpring = 45f;
        public float snapDamp = 10f;

        [Header("Ascent servo (suaviza los altos)")]
        public float climbKp = 80f;
        public float climbKv = 26f;
        public float maxClimbAccel = 55f;

        [Header("Debug")] public bool debugDraw;
        public float assistEase = 0.6f;
        
        public bool IsStepping => _stepping;

        const float Skin = 0.02f;
        bool _stepping;
        float _t0, _t1, _y0, _y1;
        Vector3 _riserNormal, _moveDirAtStart;

        bool _snapActive;
        float _snapEndTime, _snapTargetY;

        void Reset()
        {
            rb = GetComponent<Rigidbody>();
            capsule = GetComponent<CapsuleCollider>();
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }

        void Awake()
        {
            if (!rb) rb = GetComponent<Rigidbody>();
            if (!capsule) capsule = GetComponent<CapsuleCollider>();
        }

        void FixedUpdate()
        {
            if (_stepping) { ContinueStep(); return; }
            if (_snapActive) DoSnapDownSmooth();

            // Dirección robusta para probes
            Vector3 hv = rb.velocity; hv.y = 0f;
            float speed = hv.magnitude;
            Vector3 basisDir = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 moveDir = speed > probeUseVelMin ? hv / Mathf.Max(speed, 1e-5f) : basisDir;

            // 1) riser (cara vertical) en frente/±45°
            if (TryRiserBlock(moveDir, speed, out var topY, out var riserN))
            { BeginStep(moveDir, riserN, topY); return; }

            // 1b) cresta de rampa tratada como escalón
            if (TryRampCrestAsStep(moveDir, out topY, out riserN))
            { BeginStep(moveDir, riserN, topY); return; }

            // 2) ledge suspendido (tapa flotante)
            if (enableLedgeProbe && TryLedgeBlock(moveDir, speed, out topY, out riserN))
            { BeginStep(moveDir, riserN, topY); return; }

            // 3) snap-down suave si hay bajada por delante
            TrySnapDown(moveDir, speed);
        }

        // ---------- RISER ----------
        bool TryRiserBlock(Vector3 moveDir, float speed, out float topY, out Vector3 riserNormal)
        {
            topY = 0f; riserNormal = default;
            if (CheckRiserOneDir(moveDir, speed, out topY, out riserNormal)) return true;

            Vector3 d45 = Quaternion.AngleAxis(45f, Vector3.up) * moveDir;
            if (CheckRiserOneDir(d45, speed, out topY, out riserNormal)) return true;

            Vector3 dm45 = Quaternion.AngleAxis(-45f, Vector3.up) * moveDir;
            if (CheckRiserOneDir(dm45, speed, out topY, out riserNormal)) return true;

            return false;
        }

        bool CheckRiserOneDir(Vector3 dir, float speed, out float topY, out Vector3 riserNormal)
        {
            topY = 0f; riserNormal = default;

            Vector3 foot = BottomSphereCenter();
            // Sesgo hacia abajo para evitar “tocar” la tapa en bordes de box
            float bias = Mathf.Min(edgeBiasDown, Mathf.Max(0f, ankleHeight * 0.5f));
            Vector3 ankle = new Vector3(foot.x, foot.y + ankleHeight + Skin - bias, foot.z);
            Vector3 knee  = new Vector3(foot.x, foot.y + kneeHeight  + Skin - bias, foot.z);

            float fwd = checkForward + Mathf.Min(0.5f, speed * Time.fixedDeltaTime * speedCompForward);
            bool gotRiser = enableVerticalRiserSweep
                ? Physics.CapsuleCast(ankle, knee, probeRadius, dir, out var hit, fwd, walkableMask, QueryTriggerInteraction.Ignore)
                : Physics.SphereCast (ankle,      probeRadius, dir, out hit, fwd, walkableMask, QueryTriggerInteraction.Ignore);
            if (!gotRiser) return false;

            // Si la normal.y es alta, puede ser TAPA (edge de box). Intento de rescate:
            if (hit.normal.y > maxRiserNormalY)
            {
                // Re-casteo más bajo y con radio un poco menor para “enganchar” la cara vertical.
                Vector3 ankleLow = ankle - Vector3.up * Mathf.Min(edgeBiasDown * 0.75f, 0.03f);
                float rSmall = probeRadius * 0.9f;
                if (Physics.SphereCast(ankleLow, rSmall, dir, out var faceHit, fwd, walkableMask, QueryTriggerInteraction.Ignore))
                {
                    // Acepto si ahora parece una cara (normal.y baja) y está encarada.
                    if (faceHit.normal.y <= maxRiserNormalY &&
                        Vector3.Dot(dir, -faceHit.normal) >= approachDotMin)
                    {
                        hit = faceHit;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }

            if (Vector3.Dot(dir, -hit.normal) < approachDotMin) return false; // no encarado

            // Clearance superior (knee) indulgente
            float kneeR = probeRadius * kneeClearanceRadiusScale;
            if (Physics.SphereCast(knee, kneeR, dir, out _, fwd, walkableMask, QueryTriggerInteraction.Ignore))
                return false;

            // Ray down para la tapa: origen un poco más adelante y arriba
            float overAhead = Mathf.Lerp(0.12f, 0.22f,
                Mathf.Clamp01(maxStepUp <= 0f ? 0f : (hit.point.y + maxStepUp - rb.position.y) / maxStepUp));
            Vector3 over = hit.point + dir * overAhead + Vector3.up * (maxStepUp + Skin);

            if (!Physics.Raycast(over, Vector3.down, out var top, maxStepUp + 2f * Skin, walkableMask, QueryTriggerInteraction.Ignore))
                return false;

            float candidateTopY = top.point.y + Skin;
            float dy = candidateTopY - rb.position.y;

            // Aceptamos micro-escalones reales
            if (dy < minStepUp || dy > maxStepUp + 0.001f) return false;

            // Si el escalón es MUY bajo, permito una tapa un poco más inclinada
            float requiredTopNormalY = Mathf.Lerp(0.05f, topMinNormalY, Mathf.InverseLerp(minStepUp, maxStepUp, dy));
            if (top.normal.y < requiredTopNormalY) return false;

            if (!HasHeadClearance(candidateTopY)) return false;

            // OK
            topY = candidateTopY;
            riserNormal = hit.normal;

            if (debugDraw)
            {
                Debug.DrawLine(ankle, ankle + dir * fwd, Color.cyan, 0.05f);
                Debug.DrawRay(hit.point, hit.normal * 0.25f, Color.magenta, 0.1f);
                Debug.DrawRay(top.point, Vector3.up * 0.1f, Color.green, 0.1f);
            }

            return true;
        }

        // ---------- LEDGE suspendido ----------
        bool TryLedgeBlock(Vector3 moveDir, float speed, out float topY, out Vector3 fakeRiserNormal)
        {
            topY = 0f; fakeRiserNormal = default;

            Vector3 foot = BottomSphereCenter();
            float ahead = Mathf.Clamp(ledgeAhead + Mathf.Min(0.4f, speed * Time.fixedDeltaTime * speedCompForward), 0.08f, checkForward + 0.5f);
            Vector3 aheadXZ = foot + moveDir * ahead;

            Vector3 downFrom = new Vector3(aheadXZ.x, foot.y + maxStepUp + ledgeDownFromUp, aheadXZ.z);
            if (!Physics.Raycast(downFrom, Vector3.down, out var top, maxStepUp + ledgeDownFromUp + 0.1f, walkableMask, QueryTriggerInteraction.Ignore))
                return false;

            float candidateTopY = top.point.y + Skin;
            float dy = candidateTopY - rb.position.y;
            if (dy < minStepUp || dy > maxStepUp + 0.001f) return false;

            // tapa razonable según altura
            float requiredTopNormalY = Mathf.Lerp(0.05f, topMinNormalY, Mathf.InverseLerp(minStepUp, maxStepUp, dy));
            if (top.normal.y < requiredTopNormalY) return false;

            if (!HasHeadClearance(candidateTopY)) return false;

            Vector3 knee = new Vector3(foot.x, foot.y + kneeHeight + Skin, foot.z);
            float kneeR = probeRadius * (kneeClearanceRadiusScale * 0.95f); // leve reducción para evitar falsos positivos
            if (Physics.SphereCast(knee, kneeR, moveDir, out _, ahead, walkableMask, QueryTriggerInteraction.Ignore))
                return false;

            fakeRiserNormal = Vector3.ProjectOnPlane(-moveDir, Vector3.up).normalized;
            if (fakeRiserNormal.sqrMagnitude < 1e-4f) fakeRiserNormal = Vector3.forward;

            topY = candidateTopY;

            if (debugDraw)
            {
                Debug.DrawLine(downFrom, downFrom + Vector3.down * (maxStepUp + ledgeDownFromUp), Color.yellow, 0.05f);
                Debug.DrawRay(top.point, Vector3.up * 0.1f, Color.green, 0.1f);
            }

            return true;
        }

        // ---------- CRES­TA de RAMPA ----------
        bool TryRampCrestAsStep(Vector3 moveDir, out float topY, out Vector3 fakeRiserNormal)
        {
            topY = 0f; fakeRiserNormal = default;
            Vector3 foot = BottomSphereCenter();

            float ahead = Mathf.Clamp(ledgeAhead * 0.75f, 0.12f, checkForward);
            Vector3 aheadXZ = foot + moveDir * ahead;

            float up = Mathf.Max(ledgeDownFromUp, maxStepUp * 0.8f);
            Vector3 downFrom = new Vector3(aheadXZ.x, foot.y + maxStepUp + up, aheadXZ.z);

            if (!Physics.Raycast(downFrom, Vector3.down, out var topHit, maxStepUp + up + 0.1f, walkableMask, QueryTriggerInteraction.Ignore))
                return false;

            float candidateTopY = topHit.point.y + Skin;
            float dy = candidateTopY - rb.position.y;
            if (dy < minStepUp || dy > maxStepUp + 0.001f) return false;

            float requiredTopNormalY = Mathf.Lerp(0.05f, Mathf.Min(topMinNormalY, 0.2f), Mathf.InverseLerp(minStepUp, maxStepUp, dy));
            if (topHit.normal.y < requiredTopNormalY) return false;

            Vector3 knee = new Vector3(foot.x, foot.y + kneeHeight + Skin, foot.z);
            if (Physics.SphereCast(knee, probeRadius * 0.85f, moveDir, out _, ahead, walkableMask, QueryTriggerInteraction.Ignore))
                return false;

            Vector3 ankle = new Vector3(foot.x, foot.y + ankleHeight + Skin, foot.z);
            if (!Physics.SphereCast(ankle, probeRadius * 0.75f, moveDir, out var mid, ahead, walkableMask, QueryTriggerInteraction.Ignore))
                return false;

            if (mid.normal.y <= maxRiserNormalY) return false;
            if (!HasHeadClearance(candidateTopY)) return false;

            fakeRiserNormal = Vector3.ProjectOnPlane(-moveDir, Vector3.up).normalized;
            if (fakeRiserNormal.sqrMagnitude < 1e-4f) fakeRiserNormal = Vector3.forward;

            topY = candidateTopY;

            if (debugDraw)
            {
                Debug.DrawLine(downFrom, topHit.point, Color.yellow, 0.08f);
                Debug.DrawRay(topHit.point, Vector3.up * 0.08f, Color.green, 0.08f);
                Debug.DrawRay(mid.point, mid.normal * 0.25f, Color.cyan, 0.1f);
            }

            return true;
        }

        // ---------- Step (servo a tiempo constante, gravedad ON) ----------
        void BeginStep(Vector3 moveDir, Vector3 riserN, float topY)
        {
            _stepping = true;
            _t0 = Time.time;
            _t1 = _t0 + Mathf.Max(0.04f, stepTime);
            _y0 = rb.position.y;
            _y1 = topY;
            _riserNormal = riserN;
            _moveDirAtStart = moveDir;

            // evitar pelear con caída
            Vector3 v = rb.velocity;
            if (v.y < 0f) v.y = 0f;
            rb.velocity = v;
        }

        void ContinueStep()
        {
            float T = Mathf.Max(0.04f, stepTime);
            float tN = Mathf.Clamp01(Mathf.InverseLerp(_t0, _t1, Time.time));

            // s(t) suave
            float s = tN * tN * (3f - 2f * tN);
            float ds = (6f * tN * (1f - tN)) / T;

            float dy = _y1 - _y0;
            float yTarget = _y0 + dy * s;
            float vyTarget = dy * ds;

            float yNow = rb.position.y;
            float vyNow = rb.velocity.y;
            float eY = yTarget - yNow;
            float eV = vyTarget - vyNow;

            float ay = Mathf.Clamp(climbKp * eY + climbKv * eV, -maxClimbAccel, maxClimbAccel);
            rb.AddForce(new Vector3(0f, ay, 0f), ForceMode.Acceleration);

            float assistScale = assistEase > 0f ? (1f - Mathf.Abs(1f - 2f * tN)) * assistEase + (1f - assistEase) : 1f;
            rb.AddForce(_moveDirAtStart * (assistAccelXZ * assistScale), ForceMode.Acceleration);

            var v = rb.velocity;
            if (v.y > clampUpVel) { v.y = clampUpVel; rb.velocity = v; }

            if (Time.time >= _t1 - 1e-4f)
            {
                _stepping = false;
                _snapActive = true;
                _snapTargetY = _y1;
                _snapEndTime = Time.time + snapTime;
            }
        }

        // ---------- Snap-down suave ----------
        void TrySnapDown(Vector3 moveDir, float speed)
        {
            Vector3 foot = BottomSphereCenter();
            float fwd = checkForward * 0.6f + Mathf.Min(0.35f, speed * Time.fixedDeltaTime * speedCompForward * 0.6f);
            Vector3 ahead = foot + moveDir * fwd + Vector3.up * (maxStepDown + Skin);

            if (Physics.Raycast(ahead, Vector3.down, out var hit, maxStepDown + 2f * Skin, walkableMask, QueryTriggerInteraction.Ignore))
            {
                float targetY = hit.point.y + Skin;
                float dy = targetY - rb.position.y;
                if (dy < -0.03f)
                {
                    _snapActive = true;
                    _snapTargetY = targetY;
                    _snapEndTime = Time.time + snapTime;
                }
            }
        }

        void DoSnapDownSmooth()
        {
            Vector3 pos = rb.position;
            float y = pos.y;
            float targetY = _snapTargetY;

            float err = targetY - y;
            float ay = snapSpring * err - snapDamp * rb.velocity.y;

            rb.AddForce(new Vector3(0f, ay, 0f), ForceMode.Acceleration);

            if (Time.time >= _snapEndTime || Mathf.Abs(err) < 0.004f)
                _snapActive = false;
        }

        // ---------- Helpers ----------
        Vector3 BottomSphereCenter()
        {
            float r = capsule.radius;
            float half = capsule.height * 0.5f - r;
            Vector3 c = transform.TransformPoint(capsule.center);
            return new Vector3(c.x, c.y - half, c.z);
        }

        bool HasHeadClearance(float targetTopY)
        {
            float r = capsule.radius;
            float half = capsule.height * 0.5f - r;
            float futureY = targetTopY + half + Skin;
            Vector3 c = transform.TransformPoint(capsule.center);

            Vector3 headFrom = new Vector3(c.x, c.y + half, c.z);
            float upDist = (futureY - headFrom.y) + 0.06f;
            if (upDist <= 0f) return true;

            return !Physics.SphereCast(headFrom, r * 0.95f, Vector3.up, out _, upDist, walkableMask, QueryTriggerInteraction.Ignore);
        }
        
        public void CancelStep()
        {
            _stepping = false;
            _snapActive = false;
        }

        void OnValidate()
        {
            if (!capsule) capsule = GetComponent<CapsuleCollider>();
            maxStepDown = Mathf.Max(maxStepDown, maxStepUp + 0.05f);
            checkForward = Mathf.Max(0.1f, checkForward);
            ankleHeight = Mathf.Clamp(ankleHeight, -0.3f, 1f);
            kneeHeight = Mathf.Max(ankleHeight + 0.2f, kneeHeight);
            stepTime = Mathf.Max(0.04f, stepTime);
            probeRadius = Mathf.Max(0.02f, probeRadius);
            ledgeAhead = Mathf.Max(0.05f, ledgeAhead);
            snapTime = Mathf.Max(0.01f, snapTime);
            kneeClearanceRadiusScale = Mathf.Clamp(kneeClearanceRadiusScale, 0.4f, 1f);
            minStepUp = Mathf.Clamp(minStepUp, 0.001f, maxStepUp * 0.5f);
            edgeBiasDown = Mathf.Clamp(edgeBiasDown, 0f, 0.05f);
        }
    }
}
