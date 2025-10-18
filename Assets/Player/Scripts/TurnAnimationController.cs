using Player.Scripts.MovementFSM;
using Player.Scripts.MovementFSM.Player.Scripts.MovementFSM;
using UnityEngine;

namespace Player.Scripts
{
    /// <summary>
    /// Activa animaciones de giro (izq/der) cuando el jugador está quieto
    /// y el VisualYawFollower está rotando el cuerpo, solo si el jugador está grounded.
    /// </summary>
    [DefaultExecutionOrder(12000)]
    public class TurnAnimationController : MonoBehaviour
    {
        [Header("Referencias")]
        [Tooltip("VisualYawFollower que está interpolando la rotación")]
        public VisualYawFollower yawFollower;

        [Tooltip("Animator del modelo (debe tener los triggers TurnLeft / TurnRight)")]
        public Animator animator;

        [Tooltip("Transform o Rigidbody que indica la posición/velocidad del jugador")]
        public Rigidbody playerRb;

        [Tooltip("ParkourScanner para chequear si está grounded (si no se asigna, se busca automáticamente)")]
        public ParkourScanner parkourScanner;

        [Header("Ajustes")]
        [Tooltip("Velocidad máxima para considerar que está 'quieto'")]
        public float idleVelocityThreshold = 0.05f;

        [Tooltip("Velocidad mínima de rotación para disparar animaciones")]
        public float turnSpeedThreshold = 5f;

        private float _lastYaw;
        private bool _firstFrame = true;

        void Awake()
        {
            // Si no está asignado en el inspector, buscar en el mismo GameObject
            if (!parkourScanner)
                parkourScanner = GetComponent<ParkourScanner>();
        }

        void LateUpdate()
        {
            if (!yawFollower || !yawFollower.visualRoot || !animator || !playerRb || !parkourScanner)
                return;

            if (!parkourScanner.IsGrounded())
            {
                _firstFrame = true;
                return;
            }

            Vector3 vel = playerRb.velocity;
            if (vel.sqrMagnitude > idleVelocityThreshold * idleVelocityThreshold)
            {
                _firstFrame = true;
                return;
            }

            float currentYaw = yawFollower.visualRoot.eulerAngles.y;
            if (_firstFrame)
            {
                _lastYaw = currentYaw;
                _firstFrame = false;
                return;
            }

            float deltaYaw = Mathf.DeltaAngle(_lastYaw, currentYaw);
            _lastYaw = currentYaw;

            if (Mathf.Abs(deltaYaw) >= turnSpeedThreshold)
            {
                animator.CrossFade(deltaYaw > 0f ? "TurnRight" : "TurnLeft", 0.5f);
            }
        }
    }
}

