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
        public Vector3 hitPoint; // punto principal de detección
        public Vector3 hitNormal; // normal del obstáculo
        public float obstacleHeight; // alto medido del obstáculo

        // Datos para VAULT
        public Vector3 vaultTopPoint; // punto en el "tapa" del obstáculo
        public Vector3 vaultLandPoint; // punto sugerido de aterrizaje
        public float vaultDistance; // dist. horizontal sobre el obstáculo
        public Vector3 vaultStartPoint; // opcional, suele ser rb.position
        public Vector3 vaultMidXZ; // centro xz de la tapa
        public float vaultArcApex; // altura sugerida (puede ser 0, se recalcula en estado)}
        public Collider vaultObstacle;
        public Vector3 vaultForward;

        // Datos para CLIMB
        public Vector3 climbLedgePoint; // borde/ledge donde agarrarse
        public Vector3 climbStandPoint; // punto arriba donde quedar parado
        public float climbHeight; // altura a trepar

        // Datos para WALLRUN
        public Vector3 wallRunWallPoint; // punto de contacto pared (lado)
        public Vector3 wallRunNormal; // normal pared
        public int wallSide; // -1 = izquierda, +1 = derecha

        public static ParkourProbe None => new ParkourProbe { action = ParkourAction.None };
    }

    [DefaultExecutionOrder(50)]
    public class ParkourScanner : MonoBehaviour
    {
        [Header("References")] public Rigidbody rb;
        public Transform cameraHolder;

        [Header("Layers")] public LayerMask environmentMask;

        public LayerMask groundMask;

        [Header("General")] [Tooltip("Distancia máx. al obstáculo para iniciar (m).")]
        public float forwardCheckDistance = 1.2f;

        [Tooltip("Separación mínima a paredes/techos para considerar 'libre' (m).")]
        public float clearanceSkin = 0.06f;

        [Header("Grounding (Scanner)")] public CapsuleCollider capsule;
        public float groundCheckDistance = 0.2f;
        [Range(0f, 80f)] public float maxGroundSlopeDeg = 55f;

        public bool Grounded { get; private set; }
        public RaycastHit GroundHit { get; private set; }
        public float GroundSlopeDeg { get; private set; }

        public bool IsGrounded() => Grounded;

        [Header("Vault")] public float vaultMinHeight = 0.4f; // altura mínima del obstáculo
        public float vaultMaxHeight = 1.2f; // altura máxima del obstáculo
        public float vaultTopClearance = 0.25f; // alto libre sobre la tapa
        public float vaultMinForward = 0.4f; // salto horizontal mínimo
        public float vaultMaxForward = 2.2f; // salto horizontal máximo
        public float vaultDownCast = 2.5f; // cuánto buscar suelo hacia abajo

        [Header("Climb")] public float climbMinHeight = 1.0f; // altura mínima del borde
        public float climbMaxHeight = 2.2f; // altura máxima alcanzable
        public float climbForwardProbe = 0.25f; // cuánto asomar la búsqueda del borde
        public float climbTopClearance = 0.35f; // espacio libre para la cabeza arriba
        public float climbStandForward = 0.35f; // cuanto avanzar sobre la tapa

        [Header("Wallrun")] public float wallCheckDistance = 0.9f; // ray lateral
        public float wallMinHeight = 1.4f; // altura libre encima para correr
        [Range(0f, 60f)] public float wallMaxSlopeDeg = 15f; // qué tan “vertical” debe ser
        [Range(0f, 70f)] public float wallToForwardMaxAngle = 55f; // ángulo máx entre dir. avance y pared
        public float wallMinSpeed = 3.5f; // vel horizontal mínima para habilitar

        [Header("Ground")] public bool requireAirForWallrun = true; // típico: wallrun cuando no estás grounded

        [Header("Debug")] public bool drawGizmos = true;

        // Salida
        public ParkourProbe Probe { get; private set; }
        public event Action<ParkourProbe> OnProbeUpdated = delegate { };

        // Cache
        float Radius => capsule ? Mathf.Max(0.05f, capsule.radius) : 0.3f;
        float Height => capsule ? capsule.height : 1.8f;

        void Reset()
        {
            rb = GetComponent<Rigidbody>();
            capsule = GetComponent<CapsuleCollider>();
        }

        void Update()
        {
            UpdateGrounding();
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

            if (TryDetectVault(out var vault)) return vault;

            if (TryDetectClimb(out var climb)) return climb;

            if (TryDetectWallrun(+1, out var wrRight)) return wrRight;
            if (TryDetectWallrun(-1, out var wrLeft)) return wrLeft;

            return ParkourProbe.None;
        }

        #region GROUNDING

        void UpdateGrounding()
        {
            Grounded = false;
            GroundSlopeDeg = 0f;

            if (!capsule) return;

            const float skin = 0.02f;
            float r = Mathf.Max(0.01f, capsule.radius - skin);
            Vector3 center = transform.TransformPoint(capsule.center);
            float half = capsule.height * 0.5f - r;

            Vector3 top = center + Vector3.up * half;
            Vector3 bottom = center - Vector3.up * half;

            // 1) Overlap + pendiente
            var cols = Physics.OverlapCapsule(top, bottom, r + skin, groundMask, QueryTriggerInteraction.Ignore);
            foreach (var col in cols)
            {
                if (!col) continue;
                if (Physics.Raycast(center, Vector3.down, out var rh, half + groundCheckDistance + 0.5f,
                        groundMask, QueryTriggerInteraction.Ignore))
                {
                    float slope = Vector3.Angle(rh.normal, Vector3.up);
                    if (slope <= maxGroundSlopeDeg)
                    {
                        Grounded = true;
                        GroundHit = rh;
                        GroundSlopeDeg = slope;
                        return;
                    }
                }
                else
                {
                    // sin normal confiable: aceptar como grounded
                    Grounded = true;
                    GroundHit = new RaycastHit { point = bottom, normal = Vector3.up };
                    GroundSlopeDeg = 0f;
                    return;
                }
            }

            // 2) SphereCast bajo los pies (gap pequeño)
            Vector3 bottomSphereCenter = bottom;
            float castDist = Mathf.Max(0.05f, groundCheckDistance);
            if (Physics.SphereCast(bottomSphereCenter + Vector3.up * 0.01f, r, Vector3.down,
                    out var hitS, castDist + 0.02f, groundMask, QueryTriggerInteraction.Ignore))
            {
                float slope = Vector3.Angle(hitS.normal, Vector3.up);
                if (slope <= maxGroundSlopeDeg)
                {
                    Grounded = true;
                    GroundHit = hitS;
                    GroundSlopeDeg = slope;
                    return;
                }
            }

            // 3) Fallback: CapsuleCast hacia abajo
            if (Physics.CapsuleCast(top, bottom, r, Vector3.down, out var hitC,
                    castDist, groundMask, QueryTriggerInteraction.Ignore))
            {
                float slope = Vector3.Angle(hitC.normal, Vector3.up);
                if (slope <= maxGroundSlopeDeg)
                {
                    Grounded = true;
                    GroundHit = hitC;
                    GroundSlopeDeg = slope;
                }
            }
        }

        #endregion

        #region VAULT

        bool TryDetectVault(out ParkourProbe result)
        {
            result = ParkourProbe.None;
            var m = GetComponent<Model>();
            if (m && Time.time < m.blockVaultUntil || !m.runningKeyPressed) return false;

            // Ajustes base
            float r = Radius;
            float h = Height;

            // (A) Guardamos un "start" coherente para el vault (sirve para corregir forward)
            Vector3 start = rb ? rb.position : transform.position;

            Vector3 pos = transform.position;
            Vector3 fwd = GetPlanarForward();
            float chest = Mathf.Clamp(h * 0.55f, 0.8f, 1.1f);
            Vector3 chestOrigin = pos + Vector3.up * chest;

            // 1) Ray frontal: encontrar “cara” de obstáculo
            if (!Physics.Raycast(chestOrigin, fwd, out var hitFront, forwardCheckDistance, environmentMask,
                    QueryTriggerInteraction.Ignore))
                return false;

            // 2) Encontrar Y de la tapa
            if (!TopFromHit(hitFront, out float topY))
                return false;

            // Altura útil
            float feetY = (pos + Vector3.up * r).y;
            float obsHeight = topY - feetY;
            if (obsHeight < vaultMinHeight || obsHeight > vaultMaxHeight)
                return false;

            // 3) Punto exacto en la tapa (bajar desde arriba del hit)
            Vector3 topProbeStart = new Vector3(hitFront.point.x, topY + 0.02f, hitFront.point.z);
            if (!Physics.Raycast(topProbeStart, Vector3.down, out var topHit, 1f, environmentMask,
                    QueryTriggerInteraction.Ignore))
                return false;
            Vector3 topPoint = topHit.point + Vector3.up * clearanceSkin;

            // 4) Centro XZ de la tapa
            Vector3 midXZ = new Vector3(topPoint.x, topPoint.y, topPoint.z) + fwd * Mathf.Max(r * 0.6f, 0.2f);
            midXZ.y = topPoint.y;

            // Clearance sobre la tapa
            if (!HasClearanceCapsule(topPoint + Vector3.up * (vaultTopClearance * 0.5f), vaultTopClearance))
                return false;

            // 5) Buscar aterrizaje al otro lado
            float minF = Mathf.Max(vaultMinForward, r * 1.2f);
            float maxF = Mathf.Max(minF + 0.3f, vaultMaxForward);
            LayerMask groundOrEnv = groundMask.value != 0 ? groundMask : environmentMask;

            Vector3 land = Vector3.zero;
            bool foundLand = false;
            for (float f = minF; f <= maxF + 0.0001f; f += 0.1f)
            {
                Vector3 over = topPoint + fwd * f + Vector3.up * 0.05f;
                if (Physics.Raycast(over, Vector3.down, out var downHit, vaultDownCast, groundOrEnv,
                        QueryTriggerInteraction.Ignore))
                {
                    Vector3 stand = downHit.point + Vector3.up * (r + clearanceSkin);
                    if (HasClearanceCapsule(stand, h - r * 2f))
                    {
                        land = stand;
                        foundLand = true;
                        break;
                    }
                }
            }

            if (!foundLand) return false;

            // (B) Forward base según tu cámara/cuerpo (como ya hacías)
            Vector3 vaultFwd = GetPlanarForward();
            if (vaultFwd.sqrMagnitude > 0f) vaultFwd.Normalize();

            // (C) ***CORRECCIÓN DE SIGNO***: asegurar que vaultForward apunte de start -> land (en XZ)
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

            result = new ParkourProbe
            {
                action = ParkourAction.Vault,
                hitPoint = hitFront.point,
                hitNormal = hitFront.normal,
                obstacleHeight = obsHeight,

                vaultTopPoint = topPoint,
                vaultMidXZ = midXZ,
                vaultLandPoint = land,
                vaultDistance = Vector3.Distance(
                    new Vector3(topPoint.x, 0, topPoint.z),
                    new Vector3(land.x, 0, land.z)
                ),

                // Importante: guardamos el start para que el estado pueda usarlo si quiere
                vaultStartPoint = start,

                playerRadius = r,
                playerHeight = h,
                vaultObstacle = hitFront.collider,

                // vaultForward ya corregido de signo
                vaultForward = vaultFwd
            };
            return true;
        }

        #endregion

        #region CLIMB

        bool TryDetectClimb(out ParkourProbe result)
        {
            result = ParkourProbe.None;

            Vector3 forward = GetPlanarForward();

            // Pared inmediatamente delante
            float chest = Mathf.Clamp(Height * 0.55f, 0.8f, 1.1f);
            Vector3 chestOrigin = transform.position + Vector3.up * chest;

            if (!Physics.Raycast(chestOrigin, forward, out RaycastHit wallHit,
                    forwardCheckDistance, environmentMask,
                    QueryTriggerInteraction.Ignore))
                return false;

            // Buscamos un ledge “arriba y un poco adelante”
            float minY = transform.position.y + Radius + climbMinHeight;
            float maxY = transform.position.y + Radius + climbMaxHeight;

            // Muestreamos varios puntos verticales para encontrar un "quiebre" (borde)
            const int steps = 6;
            for (int i = 0; i <= steps; i++)
            {
                float y = Mathf.Lerp(minY, maxY, i / (float)steps);
                Vector3 probeStart = new Vector3(transform.position.x, y, transform.position.z)
                                     + forward * (Radius + climbForwardProbe);

                // Bajar para encontrar “tapa” hacia abajo
                if (Physics.Raycast(probeStart, Vector3.down, out RaycastHit down,
                        climbMaxHeight + 1.0f, environmentMask,
                        QueryTriggerInteraction.Ignore))
                {
                    float climbH = down.point.y - (transform.position.y + Radius);
                    if (climbH < climbMinHeight || climbH > climbMaxHeight) continue;

                    // Clearance donde quedará la cabeza arriba
                    Vector3 headSpace = down.point + Vector3.up * (Radius + climbTopClearance);
                    if (!HasClearanceCapsule(headSpace, Height - Radius * 2f))
                        continue;

                    // Punto sugerido para pararse
                    Vector3 stand = down.point + forward * Mathf.Max(0.05f, climbStandForward)
                                               + Vector3.up * (Radius + clearanceSkin);
                    if (!HasClearanceCapsule(stand, Height - Radius * 2f))
                        continue;

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
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region WALLRUN

        bool TryDetectWallrun(int side, out ParkourProbe result)
        {
            result = ParkourProbe.None;

            var m = GetComponent<Model>();
            if (m && Time.time < m.blockWallrunUntil) return false;

            if (requireAirForWallrun && Grounded) return false;

            Vector3 fwd = GetPlanarForward();
            Vector3 sideDir = (side < 0 ? -transform.right : transform.right);
            sideDir.y = 0f;
            sideDir.Normalize();

            float mid = Mathf.Clamp(Height * 0.5f, 0.8f, 1.0f);
            Vector3 origin = transform.position + Vector3.up * mid;

            if (!Physics.Raycast(origin, sideDir, out RaycastHit hit, wallCheckDistance, environmentMask,
                    QueryTriggerInteraction.Ignore))
                return false;

            float upDot = Vector3.Dot(hit.normal, Vector3.up);
            if (Mathf.Abs(upDot) > Mathf.Sin(wallMaxSlopeDeg * Mathf.Deg2Rad))
                return false;

            Vector3 wallForward = Vector3.Cross(hit.normal, Vector3.up);
            if (Vector3.Dot(fwd, wallForward) < Vector3.Dot(fwd, -wallForward))
                wallForward = -wallForward;

            float ang = Vector3.Angle(fwd, wallForward);
            if (ang > wallToForwardMaxAngle) return false;

            Vector3 horizVel = rb ? new Vector3(rb.velocity.x, 0, rb.velocity.z) : Vector3.zero;
            if (horizVel.magnitude < wallMinSpeed)
                return false;

            Vector3 head = origin + Vector3.up * wallMinHeight;
            if (!HasClearanceCapsule(head, Radius * 2f))
                return false;

            Vector3 fwdPlanar = GetPlanarForward();
            Vector3 nPlanar = hit.normal;
            nPlanar.y = 0f;
            if (nPlanar.sqrMagnitude < 1e-6f)
            {
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
            // subimos y disparamos hacia abajo para ubicar la “tapa”
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
            // Simulamos la cápsula del jugador para validar espacio libre
            float r = Radius - clearanceSkin * 0.5f;
            float h = Mathf.Max(r * 2f + 0.01f, heightSegment);
            float half = h * 0.5f - r;

            Vector3 top = center + Vector3.up * half;
            Vector3 bottom = center - Vector3.up * half;

            return !Physics.CheckCapsule(top, bottom, r, environmentMask,
                QueryTriggerInteraction.Ignore);
        }

        #endregion

        #region Gizmos

        void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;

            Gizmos.matrix = Matrix4x4.identity;

            // Dirección de avance
            Vector3 f = Application.isPlaying
                ? GetPlanarForward()
                : (transform.forward - Vector3.Project(transform.forward, Vector3.up)).normalized;
            Vector3 origin = transform.position +
                             Vector3.up * Mathf.Clamp((capsule ? capsule.height : 1.8f) * 0.55f, 0.8f, 1.1f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(origin, origin + f * forwardCheckDistance);

            // Resultado actual
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

            // Laterales wallrun
            float mid = Mathf.Clamp((capsule ? capsule.height : 1.8f) * 0.5f, 0.8f, 1.0f);
            Vector3 sideOrigin = transform.position + Vector3.up * mid;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(sideOrigin, sideOrigin + transform.right * wallCheckDistance);
            Gizmos.DrawLine(sideOrigin, sideOrigin - transform.right * wallCheckDistance);
        }

        #endregion
    }
}