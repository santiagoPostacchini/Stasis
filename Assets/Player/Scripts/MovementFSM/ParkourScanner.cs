using System;
using Player.Scripts.MovementFSM.MVC;
using UnityEngine;

namespace Player.Scripts.MovementFSM
{
    public enum ParkourAction
    {
        None,
        Vault,
        Climb,
        WallrunLeft,
        WallrunRight
    }

    [Serializable]
    public struct ParkourProbe
    {
        public ParkourAction action;

        public float playerRadius;
        public float playerHeight;

        // Datos comunes
        public Vector3 hitPoint;
        public Vector3 hitNormal;
        public float obstacleHeight;

        // VAULT
        public Vector3 vaultTopPoint;
        public Vector3 vaultLandPoint;
        public float vaultDistance;
        public Vector3 vaultStartPoint;
        public Vector3 vaultMidXZ;
        public float vaultArcApex;
        public Collider vaultObstacle;
        public Vector3 vaultForward;
        public bool vaultLandOnSameCollider;

        // CLIMB
        public Vector3 climbLedgePoint;
        public Vector3 climbStandPoint;
        public float climbHeight;

        // WALLRUN
        public Vector3 wallRunWallPoint;
        public Vector3 wallRunNormal;
        public int wallSide;

        public static ParkourProbe None => new ParkourProbe { action = ParkourAction.None };
    }

    [DefaultExecutionOrder(50)]
    public class ParkourScanner : MonoBehaviour
    {
        [Header("References")] public Rigidbody rb;
        public Transform cameraHolder;

        [Header("Layers")] public LayerMask environmentMask;
        public LayerMask groundMask;
        public LayerMask climbMask;

        [Header("General")] [Tooltip("Distancia máx. al obstáculo para iniciar (m).")]
        public float forwardCheckDistance = 1.2f;

        [Tooltip("Separación mínima a paredes/techos para considerar 'libre' (m).")]
        public float clearanceSkin = 0.06f;

        [Header("Grounding (Scanner)")] public CapsuleCollider capsule;
        public float groundCheckDistance = 0.2f;
        [Range(0f, 80f)] public float maxGroundSlopeDeg = 55f;

        private bool Grounded { get; set; }
        private RaycastHit GroundHit { get; set; }
        private float GroundSlopeDeg { get; set; }

        public bool IsGrounded() => Grounded;

        [Header("Vault")] public float vaultMinHeight = 0.4f;
        public float vaultMaxHeight = 1.2f;
        public float vaultTopClearance = 0.25f;
        public float vaultMinForward = 0.4f;
        public float vaultMaxForward = 2.2f;
        public float vaultDownCast = 2.5f;

        [Header("Climb")] public float climbMinHeight = 1.0f;
        public float climbMaxHeight = 2.2f;
        public float climbForwardProbe = 0.25f;
        public float climbTopClearance = 0.35f;
        public float climbStandForward = 0.35f;

        [Header("Wallrun")] public float wallCheckDistance = 0.9f;
        public float wallMinHeight = 1.4f;
        [Range(0f, 60f)] public float wallMaxSlopeDeg = 15f;
        [Range(0f, 70f)] public float wallToForwardMaxAngle = 55f;
        public float wallMinSpeed = 3.5f;

        [Header("Ground")] public bool requireAirForWallrun = true;

        [Header("Debug")] public bool drawGizmos = true;

        [Tooltip("Activa logs verbosos en consola.")]
        public bool verboseLogs = true;

        // Salida
        public ParkourProbe Probe { get; private set; }
        public event Action<ParkourProbe> OnProbeUpdated = delegate { };
        public event Action<bool, RaycastHit> OnGroundedChanged = delegate { };

        private bool _prevGrounded;
        private RaycastHit _prevGroundHit;

        float Radius => capsule ? Mathf.Max(0.05f, capsule.radius) : 0.3f;
        float Height => capsule ? capsule.height : 1.8f;
        
        public RaycastHit CurrentGroundHit => GroundHit;
        
        public float CurrentGroundSlopeDeg => GroundSlopeDeg;

        void Reset()
        {
            rb = GetComponent<Rigidbody>();
            capsule = GetComponent<CapsuleCollider>();
        }

        void Update()
        {
            UpdateGrounding();
            if (Grounded != _prevGrounded)
            {
                if (verboseLogs)
                    Debug.Log($"[Scanner] Grounded={Grounded} Slope={GroundSlopeDeg:F1} pos={transform.position:F3}");
                OnGroundedChanged(Grounded, GroundHit);
                _prevGrounded = Grounded;
                _prevGroundHit = GroundHit;
            }

            Probe = Evaluate();
            OnProbeUpdated(Probe);
        }

        ParkourProbe Evaluate()
        {
            if (!capsule || !cameraHolder)
            {
                Debug.LogWarning($"[Scanner] Falta referencia: capsule={capsule}, cameraHolder={cameraHolder}");
                return ParkourProbe.None;
            }

            if (TryDetectVault(out var vault))
            {
                if (verboseLogs)
                    Debug.Log(
                        $"[Scanner] VAULT ok | h={vault.obstacleHeight:F2} dist={vault.vaultDistance:F2} sameTop={vault.vaultLandOnSameCollider} land={vault.vaultLandPoint:F3}");
                return vault;
            }

            if (TryDetectClimb(out var climb))
            {
                if (verboseLogs)
                    Debug.Log($"[Scanner] CLIMB ok | height={climb.climbHeight:F2} ledge={climb.climbLedgePoint:F3}");
                return climb;
            }

            if (TryDetectWallrun(+1, out var wrRight))
            {
                if (verboseLogs)
                    Debug.Log($"[Scanner] WALLRUN RIGHT ok | wall={wrRight.wallRunWallPoint:F3}");
                return wrRight;
            }

            if (TryDetectWallrun(-1, out var wrLeft))
            {
                if (verboseLogs)
                    Debug.Log($"[Scanner] WALLRUN LEFT ok | wall={wrLeft.wallRunWallPoint:F3}");
                return wrLeft;
            }

            return ParkourProbe.None;
        }

        #region GROUNDING

        [Header("Grounding (Tuning)")] [Tooltip("Distancia máx. a la superficie para 'pegar' al suelo (m).")]
        public float groundSnapDistance = 0.08f;

        [Tooltip("Aumenta el alcance del cast según la caída.")]
        public float fallProbeVelocityMul = 0.035f;

        [Header("Grounding Stability")]
        [Tooltip("Tiempo que debe sostenerse el contacto antes de reportar Grounded=true.")]
        public float groundEnterStability = 0.05f; // 50 ms va bien (0.04–0.07)

        [Tooltip("Retraso al soltar suelo. 0 para salida inmediata.")]
        public float groundExitStability = 0.00f; // dejalo en 0

        private bool _rawGrounded;
        private float _rawChangeTime;
        private RaycastHit _rawHit;
        private float _rawSlope;

        private float _lastGroundTrueTime;

        void GetCapsuleWorld(out Vector3 top, out Vector3 bottom, out float r)
        {
            const float skin = 0.02f;
            r = Mathf.Max(0.01f, (capsule ? capsule.radius : 0.3f) - skin);
            Vector3 center = transform.TransformPoint(capsule ? capsule.center : Vector3.zero);
            float height = capsule ? capsule.height : 1.8f;
            float half = height * 0.5f - r;
            top = center + Vector3.up * half;
            bottom = center - Vector3.up * half;
        }

        bool IsValidGroundHit(in RaycastHit hit)
        {
            if (hit.collider == null) return false;
            float slope = Vector3.Angle(hit.normal, Vector3.up);
            if (slope > maxGroundSlopeDeg) return false;

            // Debe estar “debajo” razonablemente cerca
            float dy = (transform.position.y) - hit.point.y;
            if (dy < -0.05f) return false; // golpe por arriba (techo) no es suelo
            return true;
        }

        void UpdateGrounding()
        {
            // 1) Medimos "raw" sin histéresis (igual que antes)
            bool raw = false;
            RaycastHit rawHit = default;
            float rawSlope = 0f;

            if (!capsule)
            {
                Grounded = false;
                return;
            }

            GetCapsuleWorld(out var top, out var bottom, out var r);
            Vector3 sphereOrigin = bottom + Vector3.up * 0.01f;

            float extraByFall = (rb && rb.velocity.y < 0f)
                ? Mathf.Clamp(-rb.velocity.y * fallProbeVelocityMul, 0f, 0.3f)
                : 0f;

            float castDist = Mathf.Max(groundCheckDistance, groundSnapDistance) + extraByFall + 0.02f;

            if (Physics.SphereCast(sphereOrigin, r, Vector3.down, out var hitS, castDist, groundMask,
                    QueryTriggerInteraction.Ignore)
                && IsValidGroundHit(hitS))
            {
                float dist = Mathf.Max(0f, (sphereOrigin.y - r) - hitS.point.y);
                if (dist <= groundSnapDistance + extraByFall)
                {
                    raw = true;
                    rawHit = hitS;
                    rawSlope = Vector3.Angle(hitS.normal, Vector3.up);
                }
            }

            if (!raw)
            {
                if (Physics.CapsuleCast(top, bottom, r, Vector3.down, out var hitC, castDist, groundMask,
                        QueryTriggerInteraction.Ignore)
                    && IsValidGroundHit(hitC))
                {
                    float baseY = (bottom.y - r);
                    float dist = Mathf.Max(0f, baseY - hitC.point.y);
                    if (dist <= groundSnapDistance + extraByFall)
                    {
                        raw = true;
                        rawHit = hitC;
                        rawSlope = Vector3.Angle(hitC.normal, Vector3.up);
                    }
                }
            }

            // 2) Histéresis temporal
            if (raw != _rawGrounded)
            {
                _rawGrounded = raw;
                _rawChangeTime = Time.time;
                if (raw)
                {
                    _rawHit = rawHit;
                    _rawSlope = rawSlope;
                } // guardamos el último hit bueno
            }

            bool want = Grounded; // estado estable actual

            if (_rawGrounded)
            {
                // Para entrar a true, exigir estabilidad temporal
                if (!Grounded && (Time.time - _rawChangeTime) >= groundEnterStability)
                {
                    want = true;
                    GroundHit = _rawHit;
                    GroundSlopeDeg = _rawSlope;
                }
                else if (Grounded)
                {
                    // ya estamos en true: refrescamos datos
                    GroundHit = _rawHit;
                    GroundSlopeDeg = _rawSlope;
                }
            }
            else
            {
                // Salida a false (con opcional groundExitStability)
                if (Grounded && (Time.time - _rawChangeTime) >= groundExitStability)
                {
                    want = false;
                    GroundHit = default;
                    GroundSlopeDeg = 0f;
                }
            }

            Grounded = want;
        }

        #endregion

        #region VAULT

        bool TryDetectVault(out ParkourProbe result)
        {
            result = ParkourProbe.None;
            var m = GetComponent<Model>();
            bool runHeld = m && m.runningKeyPressed;
            if (!runHeld)
            {
                if (verboseLogs) Debug.Log("[VaultProbe] BLOCKED: runningKeyPressed == false");
                return false;
            }

            if (m && Time.time < m.blockVaultUntil)
            {
                if (verboseLogs)
                    Debug.Log($"[VaultProbe] BLOCKED by cooldown ({Time.time:F2} < {m.blockVaultUntil:F2})");
                return false;
            }

            float r = Radius;
            float h = Height;

            Vector3 start = rb ? rb.position : transform.position;
            Vector3 pos = transform.position;
            Vector3 fwd = GetPlanarForward();
            float chest = Mathf.Clamp(h * 0.55f, 0.8f, 1.1f);
            Vector3 chestOrigin = pos + Vector3.up * chest;

            if (!Physics.Raycast(chestOrigin, fwd, out var hitFront, forwardCheckDistance, environmentMask,
                    QueryTriggerInteraction.Ignore))
                return false;

            if (!TopFromHit(hitFront, out float topY))
            {
                if (verboseLogs)
                    Debug.Log($"[VaultProbe] No top from hit. hit={hitFront.point:F3} normal={hitFront.normal:F3}");
                return false;
            }

            float feetY = (pos + Vector3.up * r).y;
            float obsHeight = topY - feetY;
            if (verboseLogs)
                Debug.Log($"[VaultProbe] ObsHeight={obsHeight:F2} (min={vaultMinHeight:F2} max={vaultMaxHeight:F2})");
            if (obsHeight < vaultMinHeight || obsHeight > vaultMaxHeight) return false;

            Vector3 topProbeStart = new Vector3(hitFront.point.x, topY + 0.02f, hitFront.point.z);
            if (!Physics.Raycast(topProbeStart, Vector3.down, out var topHit, 1f, environmentMask,
                    QueryTriggerInteraction.Ignore))
            {
                if (verboseLogs) Debug.Log($"[VaultProbe] No topHit from above.");
                return false;
            }

            Vector3 topPoint = topHit.point + Vector3.up * clearanceSkin;

            Vector3 midXZ = new Vector3(topPoint.x, topPoint.y, topPoint.z) + fwd * Mathf.Max(r * 0.6f, 0.2f);
            midXZ.y = topPoint.y;

            // (seguimos pidiendo clearance sobre la tapa, pero esto se ignora en el fallback)
            if (!HasClearanceCapsule(topPoint + Vector3.up * (vaultTopClearance * 0.5f), vaultTopClearance))
            {
                if (verboseLogs) Debug.Log($"[VaultProbe] No clearance above top. top={topPoint:F3}");
                // no retornamos aquí: un obstáculo grueso puede forzar step-up
            }

            float minF = Mathf.Max(vaultMinForward, r * 1.2f);
            float maxF = Mathf.Max(minF + 0.3f, vaultMaxForward);
            LayerMask groundOrEnv = groundMask.value != 0 ? groundMask : environmentMask;
            LayerMask maskTop = environmentMask | groundMask;

            Vector3 land = Vector3.zero;
            bool foundLand = false;
            bool foundSameTop = false;
            Vector3 landOnSameTop = Vector3.zero;

            // --- MISMA TAPA ---
            for (float f = 0.05f; f <= maxF + 0.0001f; f += 0.1f)
            {
                Vector3 over = topPoint + fwd * f + Vector3.up * 0.05f;
                if (Physics.Raycast(over, Vector3.down, out var downHit, vaultDownCast, maskTop,
                        QueryTriggerInteraction.Ignore))
                {
                    if (downHit.collider == hitFront.collider)
                    {
                        Vector3 stand = downHit.point + Vector3.up * (r + clearanceSkin);
                        if (HasClearanceCapsule(stand, h - r * 2f))
                        {
                            landOnSameTop = stand;
                            foundSameTop = true;
                            if (verboseLogs) Debug.Log($"[VaultProbe] SameTop at f={f:F2} stand={stand:F3}");
                            break;
                        }
                        else if (verboseLogs) Debug.Log($"[VaultProbe] SameTop blocked clearance at f={f:F2}");
                    }
                }
            }

            // --- OTRO LADO ---
            Vector3 landFallback = Vector3.zero;
            bool foundFallback = false;
            if (!foundSameTop)
            {
                for (float f = minF; f <= maxF + 0.0001f; f += 0.1f)
                {
                    Vector3 over = topPoint + fwd * f + Vector3.up * 0.05f;
                    if (Physics.Raycast(over, Vector3.down, out var downHit, vaultDownCast, groundOrEnv,
                            QueryTriggerInteraction.Ignore))
                    {
                        Vector3 stand = downHit.point + Vector3.up * (r + clearanceSkin);
                        if (HasClearanceCapsule(stand, h - r * 2f))
                        {
                            landFallback = stand;
                            foundFallback = true;
                            if (verboseLogs) Debug.Log($"[VaultProbe] FarSide at f={f:F2} stand={stand:F3}");
                            break;
                        }
                        else if (verboseLogs) Debug.Log($"[VaultProbe] FarSide clearance blocked at f={f:F2}");
                    }
                }
            }

            // --- ¿obstáculo más grueso que maxF? (chequeo para habilitar fallback sin clearance) ---
            bool thickTopLikely = false;
            {
                Vector3 overMax = topPoint + fwd * (maxF + 0.05f) + Vector3.up * 0.2f;
                if (Physics.Raycast(overMax, Vector3.down, out var dTopMax, vaultDownCast, environmentMask,
                        QueryTriggerInteraction.Ignore))
                    thickTopLikely = (dTopMax.collider == hitFront.collider);
            }

            // --- FALLBACK FUERZA STEP-UP (ignora clearance) ---
            if (!foundSameTop && !foundFallback && thickTopLikely)
            {
                landOnSameTop = topPoint + Vector3.up * (r + clearanceSkin);
                foundSameTop = true;
                if (verboseLogs)
                    Debug.Log($"[VaultProbe] FORCE STEP-UP (thickTop) -> land={landOnSameTop:F3} (ignoring clearance)");
            }

            if (foundSameTop)
            {
                land = landOnSameTop;
                foundLand = true;
            }
            else if (foundFallback)
            {
                land = landFallback;
                foundLand = true;
            }

            if (!foundLand)
            {
                if (verboseLogs) Debug.Log($"[VaultProbe] No land found.");
                return false;
            }

            // forward final y corrección de signo
            Vector3 vaultFwd = GetPlanarForward();
            if (vaultFwd.sqrMagnitude > 0f) vaultFwd.Normalize();

            Vector3 desired = land - start;
            desired.y = 0f;
            if (desired.sqrMagnitude > 1e-6f)
            {
                desired.Normalize();
                Vector3 vf = vaultFwd;
                vf.y = 0f;
                if (vf.sqrMagnitude > 1e-6f && Vector3.Dot(vf.normalized, desired) < 0f)
                    vaultFwd = -vaultFwd;
            }

            var res = new ParkourProbe
            {
                action = ParkourAction.Vault,
                hitPoint = hitFront.point,
                hitNormal = hitFront.normal,
                obstacleHeight = obsHeight,
                vaultTopPoint = topPoint,
                vaultMidXZ = midXZ,
                vaultLandPoint = land,
                vaultDistance =
                    Vector3.Distance(new Vector3(topPoint.x, 0, topPoint.z), new Vector3(land.x, 0, land.z)),
                vaultStartPoint = start,
                playerRadius = r,
                playerHeight = h,
                vaultObstacle = hitFront.collider,
                vaultForward = vaultFwd,
                vaultLandOnSameCollider = foundSameTop
            };

            if (verboseLogs)
                Debug.Log(
                    $"[VaultProbe] OK action=Vault | h={res.obstacleHeight:F2} dist={res.vaultDistance:F2} sameTop={res.vaultLandOnSameCollider} land={res.vaultLandPoint:F3} thickTopLikely={thickTopLikely}");

            result = res;
            return true;
        }

        #endregion

        #region CLIMB

        bool TryDetectClimb(out ParkourProbe result)
        {
            var m = GetComponent<Model>();
            if (m && Time.time < m.blockClimbUntil)
            {
                if (verboseLogs) Debug.Log($"[ClimbProbe] BLOCKED by cooldown.");
                result = ParkourProbe.None;
                return false;
            }

            result = ParkourProbe.None;

            Vector3 forward = GetPlanarForward();
            LayerMask maskClimb = (climbMask.value != 0) ? climbMask : environmentMask;
            LayerMask maskTopAndClearance = maskClimb | environmentMask | groundMask;

            float chest = Mathf.Clamp(Height * 0.55f, 0.8f, 1.1f);
            Vector3 chestOrigin = transform.position + Vector3.up * chest;

            if (!Physics.Raycast(chestOrigin, forward, out RaycastHit wallHit,
                    forwardCheckDistance, maskClimb, QueryTriggerInteraction.Ignore))
            {
                //if (verboseLogs) Debug.Log($"[ClimbProbe] No wall hit.");
                return false;
            }

            float minY = transform.position.y + Radius + climbMinHeight;
            float probeUpMax = Mathf.Max(climbMaxHeight, 2.5f);
            float maxY = transform.position.y + Radius + probeUpMax;

            const int steps = 8;
            for (int i = 0; i <= steps; i++)
            {
                float y = Mathf.Lerp(minY, maxY, i / (float)steps);
                Vector3 probeStart = new Vector3(transform.position.x, y, transform.position.z)
                                     + forward * (Radius + climbForwardProbe);

                if (Physics.Raycast(probeStart, Vector3.down, out RaycastHit down,
                        probeUpMax + 1.0f, maskTopAndClearance, QueryTriggerInteraction.Ignore))
                {
                    float climbH = down.point.y - (transform.position.y + Radius);
                    if (climbH < climbMinHeight)
                    {
                        if (verboseLogs) Debug.Log($"[ClimbProbe] Ledge too low at y={y:F2} (h={climbH:F2})");
                        continue;
                    }

                    Vector3 headSpace = down.point + Vector3.up * (Radius + climbTopClearance);
                    if (!HasClearanceCapsule(headSpace, Height - Radius * 2f, maskTopAndClearance))
                    {
                        if (verboseLogs) Debug.Log($"[ClimbProbe] No head clearance at y={y:F2}");
                        continue;
                    }

                    Vector3 stand = down.point + forward * Mathf.Max(0.05f, climbStandForward)
                                               + Vector3.up * (Radius + clearanceSkin);
                    if (!HasClearanceCapsule(stand, Height - Radius * 2f, maskTopAndClearance))
                    {
                        if (verboseLogs) Debug.Log($"[ClimbProbe] No stand clearance at y={y:F2}");
                        continue;
                    }

                    result = new ParkourProbe
                    {
                        action = ParkourAction.Climb,
                        hitPoint = wallHit.point,
                        hitNormal = wallHit.normal,
                        obstacleHeight = climbH,
                        climbLedgePoint = down.point,
                        climbStandPoint = stand,
                        climbHeight = climbH
                    };
                    if (verboseLogs) Debug.Log($"[ClimbProbe] OK height={climbH:F2} ledge={down.point:F3}");
                    return true;
                }
            }

            if (verboseLogs) Debug.Log($"[ClimbProbe] No climb found.");
            return false;
        }

        // Overload con máscara
        bool HasClearanceCapsule(Vector3 center, float heightSegment, LayerMask mask)
        {
            float r = Radius - clearanceSkin * 0.5f;
            float h = Mathf.Max(r * 2f + 0.01f, heightSegment);
            float half = h * 0.5f - r;

            Vector3 top = center + Vector3.up * half;
            Vector3 bottom = center - Vector3.up * half;

            return !Physics.CheckCapsule(top, bottom, r, mask, QueryTriggerInteraction.Ignore);
        }

        #endregion

        #region WALLRUN

        bool TryDetectWallrun(int side, out ParkourProbe result)
        {
            result = ParkourProbe.None;

            var m = GetComponent<Model>();
            if (m && Time.time < m.blockWallrunUntil)
            {
                if (verboseLogs) Debug.Log($"[WallrunProbe] BLOCKED by cooldown.");
                return false;
            }

            if (requireAirForWallrun && Grounded)
            {
                //if (verboseLogs) Debug.Log($"[WallrunProbe] Requires air, but grounded.");
                return false;
            }

            Vector3 fwd = GetPlanarForward();
            Vector3 sideDir = (side < 0 ? -transform.right : transform.right);
            sideDir.y = 0f;
            sideDir.Normalize();

            float mid = Mathf.Clamp(Height * 0.5f, 0.8f, 1.0f);
            Vector3 origin = transform.position + Vector3.up * mid;

            if (!Physics.Raycast(origin, sideDir, out RaycastHit hit, wallCheckDistance, environmentMask,
                    QueryTriggerInteraction.Ignore))
            {
                if (verboseLogs) Debug.Log($"[WallrunProbe] No wall on side {side}.");
                return false;
            }

            float upDot = Vector3.Dot(hit.normal, Vector3.up);
            if (Mathf.Abs(upDot) > Mathf.Sin(wallMaxSlopeDeg * Mathf.Deg2Rad))
            {
                if (verboseLogs) Debug.Log($"[WallrunProbe] Wall too sloped.");
                return false;
            }

            Vector3 wallForward = Vector3.Cross(hit.normal, Vector3.up);
            if (Vector3.Dot(fwd, wallForward) < Vector3.Dot(fwd, -wallForward))
                wallForward = -wallForward;

            float ang = Vector3.Angle(fwd, wallForward);
            if (ang > wallToForwardMaxAngle)
            {
                if (verboseLogs) Debug.Log($"[WallrunProbe] Angle too wide ({ang:F1} > {wallToForwardMaxAngle:F1}).");
                return false;
            }

            Vector3 horizVel = rb ? new Vector3(rb.velocity.x, 0, rb.velocity.z) : Vector3.zero;
            if (horizVel.magnitude < wallMinSpeed)
            {
                if (verboseLogs)
                    Debug.Log($"[WallrunProbe] Speed too low ({horizVel.magnitude:F2} < {wallMinSpeed:F2}).");
                return false;
            }

            Vector3 head = origin + Vector3.up * wallMinHeight;
            if (!HasClearanceCapsule(head, Radius * 2f))
            {
                if (verboseLogs) Debug.Log($"[WallrunProbe] No head clearance.");
                return false;
            }

            Vector3 fwdPlanar = GetPlanarForward();
            Vector3 nPlanar = hit.normal;
            nPlanar.y = 0f;
            if (nPlanar.sqrMagnitude < 1e-6f)
            {
                if (verboseLogs) Debug.Log($"[WallrunProbe] nPlanar ~0.");
                return false;
            }

            nPlanar.Normalize();

            float sideSign = Vector3.Dot(Vector3.Cross(fwdPlanar, -nPlanar), Vector3.up);
            int resolvedSide = (sideSign >= 0f) ? +1 : -1;

            result = new ParkourProbe
            {
                action = resolvedSide > 0 ? ParkourAction.WallrunRight : ParkourAction.WallrunLeft,
                wallRunWallPoint = hit.point,
                wallRunNormal = hit.normal,
                wallSide = resolvedSide
            };

            if (verboseLogs)
                Debug.Log($"[WallrunProbe] OK side={(resolvedSide > 0 ? "Right" : "Left")} at {hit.point:F3}");
            return true;
        }

        #endregion

        #region Helpers

        Vector3 GetPlanarForward()
        {
            Vector3 f = cameraHolder ? cameraHolder.forward : transform.forward;
            f.y = 0f;
            return f.sqrMagnitude > 0.0001f ? f.normalized : transform.forward;
        }

        bool TopFromHit(in RaycastHit frontHit, out float topY)
        {
            float up = Mathf.Max(vaultMaxHeight + 0.5f, 1.6f);
            Vector3 upStart = frontHit.point + Vector3.up * up;
            if (Physics.Raycast(upStart, Vector3.down, out RaycastHit down,
                    up + 0.5f, environmentMask, QueryTriggerInteraction.Ignore))
            {
                topY = down.point.y;
                return true;
            }

            topY = 0f;
            return false;
        }

        bool HasClearanceCapsule(Vector3 center, float heightSegment)
        {
            float r = Radius - clearanceSkin * 0.5f;
            float h = Mathf.Max(r * 2f + 0.01f, heightSegment);
            float half = h * 0.5f - r;

            Vector3 top = center + Vector3.up * half;
            Vector3 bottom = center - Vector3.up * half;

            return !Physics.CheckCapsule(top, bottom, r, environmentMask, QueryTriggerInteraction.Ignore);
        }

        void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;

            Gizmos.matrix = Matrix4x4.identity;

            Vector3 f = Application.isPlaying
                ? GetPlanarForward()
                : (transform.forward - Vector3.Project(transform.forward, Vector3.up)).normalized;
            Vector3 origin = transform.position +
                             Vector3.up * Mathf.Clamp((capsule ? capsule.height : 1.8f) * 0.55f, 0.8f, 1.1f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(origin, origin + f * forwardCheckDistance);

            var p = Probe;
            if (p.action != ParkourAction.None)
            {
                Color c = p.action switch
                {
                    ParkourAction.Vault => Color.green,
                    ParkourAction.Climb => new Color(0.2f, 0.9f, 1f),
                    ParkourAction.WallrunLeft => new Color(1f, 0.5f, 0.2f),
                    ParkourAction.WallrunRight => new Color(1f, 0.5f, 0.2f),
                    _ => Color.white
                };
                Gizmos.color = c;

                if (p.action == ParkourAction.Vault)
                {
                    Gizmos.DrawSphere(p.vaultTopPoint, 0.05f);
                    Gizmos.DrawSphere(p.vaultLandPoint, 0.05f);
                    Gizmos.DrawLine(p.vaultTopPoint, p.vaultLandPoint);

                    if (p.vaultLandOnSameCollider)
                    {
                        Gizmos.color = Color.magenta;
                        Gizmos.DrawWireSphere(p.vaultLandPoint, 0.06f);
                    }
                }
                else if (p.action == ParkourAction.Climb)
                {
                    Gizmos.DrawSphere(p.climbLedgePoint, 0.06f);
                    Gizmos.DrawSphere(p.climbStandPoint, 0.06f);
                    Gizmos.DrawLine(p.climbLedgePoint, p.climbStandPoint);
                }
                else
                {
                    Gizmos.DrawSphere(p.wallRunWallPoint, 0.06f);
                    Gizmos.DrawRay(p.wallRunWallPoint, p.wallRunNormal * 0.4f);
                }
            }

            float mid = Mathf.Clamp((capsule ? capsule.height : 1.8f) * 0.5f, 0.8f, 1.0f);
            Vector3 sideOrigin = transform.position + Vector3.up * mid;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(sideOrigin, sideOrigin + transform.right * wallCheckDistance);
            Gizmos.DrawLine(sideOrigin, sideOrigin - transform.right * wallCheckDistance);
        }

        #endregion
    }
}