using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class DirectionImpulseOnCanMove_Collision : MonoBehaviour
{
    public bool playerInContact = false;
    [Header("Direccion (from -> to)")]
    public Transform from;
    public Transform to;

    [Header("FollowTargetController")]
    [Tooltip("Si no se asigna, se busca en el padre.")]
    public FollowTargetController followController;

    [Header("Jugador a impulsar")]
    public Rigidbody playerRb;
    [Tooltip("Si playerRb es null, se intentara encontrar por tag.")]
    public string playerTag = "Player";

    [Header("Parametros del impulso")]
    public float forceMagnitude = 10f;
    public ForceMode forceMode = ForceMode.Impulse;
    public bool onlyHorizontalDirection = true;

    [Header("Retrigger")]
    [Tooltip("Si es false, dispara una unica vez (no vuelve a disparar en futuros false->true).")]
    public bool retriggerOnStop = true;

    [Header("Deteccion 'arriba' por colision")]
    [Tooltip("Requerir que el jugador este arriba para disparar.")]
    public bool requirePlayerOnTop = true;
    [Tooltip("Umbral del dot(normal, up). 0.5 ~ 60°, 0.7 ~ 45°.")]
    [Range(0f, 1f)] public float minUpDot = 0.5f;
    [Tooltip("Si true usa Vector3.up; si false usa transform.up.")]
    public bool useWorldUp = true;

    [Header("Eventos (opcional)")]
    public UnityEvent onImpulseFired;
    //
    // Estado interno
    private bool prevCanMove = false;
   
    private bool playerOnTop = false;
    private int contactCount = 0; // para manejar multiples contactos/superficies

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = false; // necesitamos colisiones, no triggers
    }

    private void Awake()
    {
        if (!followController)
            followController = GetComponentInParent<FollowTargetController>();

        if (!playerRb)
        {
            var go = GameObject.FindGameObjectWithTag(playerTag);
            if (go) playerRb = go.GetComponent<Rigidbody>();
        }
    }

    private void FixedUpdate()
    {
        if (followController == null || from == null || to == null || playerRb == null)
            return;

        bool canMoveNow = followController.canMove;

        // Flanco ascendente false -> true
        if (!prevCanMove && canMoveNow)
        {
            bool allowed = playerInContact && (!requirePlayerOnTop || playerOnTop);
            if (allowed)
            {
                FireImpulse();

                if (!retriggerOnStop)
                {
                    // Queda latcheado: no re-dispara mas
                    prevCanMove = true;
                    return;
                }
            }
        }

        prevCanMove = canMoveNow;
    }

    private void FireImpulse()
    {
        Vector3 dir = to.position - from.position;
        if (onlyHorizontalDirection) dir.y = 0f;

        if (dir.sqrMagnitude < 1e-6f) return;

        dir.Normalize();
        playerRb.AddForce(dir * forceMagnitude, forceMode);
        onImpulseFired?.Invoke();
    }

    // -------- Colisiones con el Player --------

    private bool IsPlayerCollision(Collision c)
    {
        if (playerRb && c.rigidbody == playerRb) return true;
        if (!playerRb && c.collider.CompareTag(playerTag)) return true;
        return false;
    }

    private bool AnyTopContact(Collision c)
    {
        Vector3 up = useWorldUp ? Vector3.up : transform.up;

        // Si este collider es la "plataforma", la normal apunta desde la plataforma hacia el otro collider.
        // Para un contacto arriba, la normal tiende a alinear con up (dot alto).
        for (int i = 0; i < c.contactCount; i++)
        {
            ContactPoint cp = c.GetContact(i);
            float d = Vector3.Dot(cp.normal.normalized, up);
            if (d >= minUpDot) return true;
        }
        return false;
    }

    private void OnCollisionEnter(Collision c)
    {
        if (!IsPlayerCollision(c)) return;

        contactCount++;
        playerInContact = true;

        if (requirePlayerOnTop)
            playerOnTop = playerOnTop || AnyTopContact(c);
    }

    private void OnCollisionStay(Collision c)
    {
        if (!IsPlayerCollision(c)) return;
        playerInContact = true;
        // Re-evaluar "arriba" por si cambian los contactos
        if (requirePlayerOnTop)
            playerOnTop = AnyTopContact(c);
    }

    private void OnCollisionExit(Collision c)
    {
        if (!IsPlayerCollision(c)) return;

        contactCount = Mathf.Max(0, contactCount - 1);
        if (contactCount == 0)
        {
            playerInContact = false;
            playerOnTop = false;
        }
    }
}
