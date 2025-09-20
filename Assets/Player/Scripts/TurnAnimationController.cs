using UnityEngine;
using Player.Scripts.MovementFSM;

/// <summary>
/// Activa animaciones de giro (izq/der) cuando el jugador está quieto
/// y el VisualYawFollower está rotando el cuerpo.
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

    [Header("Ajustes")]
    [Tooltip("Velocidad máxima para considerar que está 'quieto'")]
    public float idleVelocityThreshold = 0.05f;

    [Tooltip("Velocidad mínima de rotación para disparar animaciones")]
    public float turnSpeedThreshold = 5f;

    private float _lastYaw;
    private bool _firstFrame = true;

    void LateUpdate()
    {
        if (!yawFollower || !yawFollower.visualRoot || !animator || !playerRb)
            return;

        // 1. Chequear si el jugador está quieto
        Vector3 vel = playerRb.velocity;
        if (vel.sqrMagnitude > idleVelocityThreshold * idleVelocityThreshold)
        {
            _firstFrame = true;
            return;
        }

        // 2. Calcular diferencia de yaw
        float currentYaw = yawFollower.visualRoot.eulerAngles.y;
        if (_firstFrame)
        {
            _lastYaw = currentYaw;
            _firstFrame = false;
            return;
        }

        float deltaYaw = Mathf.DeltaAngle(_lastYaw, currentYaw);
        _lastYaw = currentYaw;

        // 3. Si el delta supera el umbral, disparamos animación
        if (Mathf.Abs(deltaYaw) >= turnSpeedThreshold)
        {
            if (deltaYaw > 0f)
                animator.CrossFade("TurnRight", 0.5f);
            else
                animator.CrossFade("TurnLeft", 0.5f);
        }
    }
}
