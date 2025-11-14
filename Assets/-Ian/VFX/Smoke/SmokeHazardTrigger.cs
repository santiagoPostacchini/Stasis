using Managers.Game;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class SmokeHazardTrigger : MonoBehaviour
{
    [Header("Filtro de objetivo")]
    [Tooltip("Si está activo, solo mata al colisionar con este tag (ej: Player).")]
    public bool requireTag = true;
    public string targetTag = "Player";

    [Tooltip("Opcional: filtra por capas (dejar en Nothing para ignorar).")]
    public LayerMask playerLayers;

    [Header("Comportamiento")]
    [Tooltip("Mata al entrar en el volumen.")]
    public bool killOnEnter = true;
    [Tooltip("Mata mientras permanece dentro (útil para hurtboxes continuas).")]
    public bool killOnStay = false;
    [Tooltip("Evita múltiples muertes seguidas (seg).")]
    [Min(0f)] public float repeatCooldown = 0.25f;

    private float _nextAllowedTime;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true; // Asegura Trigger
    }

    private void OnTriggerEnter(Collider other)
    {
        if (killOnEnter) TryKill(other);
    }

    private void OnTriggerStay(Collider other)
    {
        if (killOnStay) TryKill(other);
    }

    private void TryKill(Collider other)
    {
        if (Time.time < _nextAllowedTime) return;

        if (requireTag && !other.CompareTag(targetTag)) return;

        if (playerLayers.value != 0) // si se configuró alguna capa
        {
            if ((playerLayers.value & (1 << other.gameObject.layer)) == 0) return;
        }

        // Requisito de física: al menos uno debe tener Rigidbody (o CharacterController)
        // Normalmente el Player lo tiene. Si no, considera agregar un Rigidbody kinemático al Player.
        if (GameManager.Instance != null)
        {
            GameManager.Instance.PlayerDeath();
            _nextAllowedTime = Time.time + repeatCooldown;
        }
        else
        {
            Debug.LogWarning($"[{name}] No hay GameManager.Instance en escena.");
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.25f);
        var col = GetComponent<Collider>();
        if (col is BoxCollider b)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(b.center, b.size);
        }
        else
        {
            Gizmos.DrawWireSphere(transform.position, 0.25f);
        }
    }
#endif
}
