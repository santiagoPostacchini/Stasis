using Player.Scripts.MovementFSM.MVC;
using UnityEngine;

namespace Player.Scripts.MovementFSM
{
    public class ProceduralClimbIK : MonoBehaviour
    {
        [Header("References")] [SerializeField]
        private Model model;

        [SerializeField] private Transform rHandTarget; // Target de la mano Izquierda
        [SerializeField] private Transform lHandTarget; // Target de la mano Derecha

        [Header("Cycle Settings")]
        [Tooltip("Velocidad del ciclo de escalada (más rápido = manos más rápidas)")]
        [SerializeField]
        private float cycleSpeed = 2.5f;

        [Tooltip("Altura vertical de cada 'paso' de la mano")] [SerializeField]
        private float stepHeight = 0.7f;

        [Tooltip("El valor X fijo para separar los brazos (anchura de hombros)")] [SerializeField]
        private float shoulderWidth = 0.55f;

        [Tooltip("Separación horizontal extra para el movimiento cíclico de la mano (0 para no moverse)")]
        [SerializeField]
        private float handCycleWidth = 0.15f;

        [Tooltip("Offset Y de las manos relativo al centro del personaje (altura de hombros)")] [SerializeField]
        private float shoulderHeightOffset = 1.8f;

        [Header("Surface Adaptation")]
        [Tooltip("Distancia para buscar la pared (desde el 'ideal' de la mano)")]
        [SerializeField]
        private float raycastSearchDistance = 0.3f;

        [Tooltip("Offset desde la superficie de la pared (para que la mano no la atraviese)")] [SerializeField]
        private float handOffsetFromWall = 0.08f;

        [Tooltip("Velocidad de interpolación de la posición de la mano")] [SerializeField]
        private float positionLerpSpeed = 12f;

        [Tooltip("Velocidad de adaptación de la rotación de la mano a la superficie")] [SerializeField]
        private float rotationLerpSpeed = 10f;
        
        [Tooltip("Offset vertical para 'asentar' la mano sobre el borde en el mantle (negativo = más abajo)")]
        [SerializeField]
        private float mantleHandYOffset = -0.05f;
        
        [Tooltip("Radio de la 'mano' para el SphereCast (para detectar curvas)")] [SerializeField]
        private float handCastRadius = 0.05f;

        private float _climbCycle;
        private Vector3 _rHandNormal;
        private Vector3 _lHandNormal;

        // Public methods for camera effects
        public float GetClimbCycle() => _climbCycle;
        
        public float GetHandHeightDifference()
        {
            if (!rHandTarget || !lHandTarget) return 0f;
            // Positive = right hand higher, negative = left hand higher
            return rHandTarget.position.y - lHandTarget.position.y;
        }

        private void LateUpdate()
        {
            if (!model || !rHandTarget || !lHandTarget || !model.rb)
            {
                return;
            }

            if (model.isMantlingState)
            {
                HandleMantleIK();
            }
            else if (model.isAtLedge) 
            {
                HandleMantleIK();
            }
            else if (model.isClimbingState)
            {
                HandleClimbIK();
            }
        }

        private void HandleClimbIK()
        {
            float vSpeed = model.rb.velocity.y;
            Vector3 wallNormal = model.climbWallNormal;
            Vector3 wallPoint = model.climbWallPoint;
            LayerMask wallMask = model.wallMask | model.groundMask;

            if (wallNormal.sqrMagnitude < 0.1f) return;
            
            if (_rHandNormal.sqrMagnitude < 0.1f)
                _rHandNormal = wallNormal;
            if (_lHandNormal.sqrMagnitude < 0.1f)
                _lHandNormal = wallNormal;

            _climbCycle += vSpeed * cycleSpeed * Time.deltaTime;

            Vector3 wallRight = Vector3.Cross(Vector3.up, wallNormal).normalized;
            
            Debug.DrawRay(model.rb.position + Vector3.up * 1.5f, wallRight * 2f, Color.red);

            Vector3 playerOnWall = wallPoint + Vector3.ProjectOnPlane(model.rb.position - wallPoint, wallNormal);

            UpdateHandClimb(rHandTarget, -1, _climbCycle, playerOnWall, wallRight, wallNormal, wallMask, ref _rHandNormal);
            UpdateHandClimb(lHandTarget, 1, _climbCycle + Mathf.PI, playerOnWall, wallRight, wallNormal, wallMask, ref _lHandNormal);
        }
        
        private void HandleMantleIK()
        {
            Vector3 wallNormal = model.climbWallNormal;
            Vector3 ledgePoint = model.mantleLedgePoint;

            if (wallNormal.sqrMagnitude < 0.1f) return;

            Vector3 wallRight = Vector3.Cross(Vector3.up, wallNormal).normalized;

            Vector3 verticalOffset = Vector3.up * mantleHandYOffset;

            Vector3 leftPos = ledgePoint + wallRight * (-shoulderWidth / 2f) + verticalOffset;
            
            Vector3 rightPos = ledgePoint + wallRight * (shoulderWidth / 2f) + verticalOffset;

            Quaternion targetRot = Quaternion.LookRotation(Vector3.down, wallNormal);

            rHandTarget.position =
                Vector3.Lerp(rHandTarget.position, leftPos, Time.deltaTime * positionLerpSpeed);
            rHandTarget.rotation =
                Quaternion.Slerp(rHandTarget.rotation, targetRot, Time.deltaTime * rotationLerpSpeed);

            lHandTarget.position =
                Vector3.Lerp(lHandTarget.position, rightPos, Time.deltaTime * positionLerpSpeed);
            lHandTarget.rotation =
                Quaternion.Slerp(lHandTarget.rotation, targetRot, Time.deltaTime * rotationLerpSpeed);
        }

        private void UpdateHandClimb(Transform target, float side, float cycle, Vector3 playerOnWall, Vector3 wallRight,
            Vector3 wallNormal, LayerMask wallMask, ref Vector3 lastHitNormal)
        {
            float yOffset = Mathf.Sin(cycle) * (stepHeight / 2f);
            float xCycleOffset = Mathf.Cos(cycle) * (handCycleWidth / 2f * side);

            float targetY = (model.rb.position.y + shoulderHeightOffset) + yOffset;
            float xShoulderOffset = shoulderWidth / 2f * side;

            Vector3 basePos = playerOnWall + wallRight * (xShoulderOffset + xCycleOffset);
            basePos.y = targetY;

            Vector3 targetPosition;
            Quaternion targetRotation;

            Vector3 rayOrigin = basePos + wallNormal * raycastSearchDistance;
            float castDistance = raycastSearchDistance * 2f;

            if (Physics.SphereCast(rayOrigin, handCastRadius, -wallNormal, out RaycastHit hit, castDistance, wallMask,
                    QueryTriggerInteraction.Ignore))
            {
                targetPosition = hit.point + hit.normal * handOffsetFromWall;
                targetRotation = Quaternion.LookRotation(-hit.normal, Vector3.up);
                
                lastHitNormal = hit.normal;
            }
            else
            {
                targetPosition = basePos + lastHitNormal * handOffsetFromWall;
                targetRotation = Quaternion.LookRotation(-lastHitNormal, Vector3.up);
            }

            target.position = Vector3.Lerp(target.position, targetPosition, Time.deltaTime * positionLerpSpeed);
            target.rotation = Quaternion.Slerp(target.rotation, targetRotation, Time.deltaTime * rotationLerpSpeed);
        }
    }
}