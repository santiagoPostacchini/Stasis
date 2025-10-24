using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class LorePickup : MonoBehaviour
{
    [Header("Asignación")]
    [Tooltip("ID del LoreEntry a desbloquear. Debe existir en la LoreDatabase del LoreSystem.")]
    public string entryId;

    [Header("Condiciones")]
    [Tooltip("Tag que debe tener el jugador para recoger.")]
    public string playerTag = "Player";

    [Header("Desaparición")]
    [Tooltip("Destruir este GameObject al recoger.")]
    public bool destroyOnPickup = true;
    [Tooltip("Ocultar renderers al recoger (si no destruimos).")]
    public bool hideOnPickup = false;
    [Tooltip("Siempre destruir aunque el entry ya estuviera desbloqueado (útil si usás respawn).")]
    public bool alwaysDestroyOnTrigger = false;
    [Tooltip("Demora antes de destruir (para que termine el SFX).")]
    [Min(0f)] public float destroyDelay = 0f;

    [Header("Feedback (opc)")]
    [Tooltip("SFX al recoger (Play() en este AudioSource).")]
    public AudioSource sfxOnPickup;
    [Tooltip("VFX al recoger (se instancia en la posición).")]
    public GameObject vfxPrefab;

    [Header("Eventos")]
    public UnityEvent onPicked;

    private bool _taken;

    void Reset()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    void Awake()
    {
        // Diagnóstico de configuración común
        var col = GetComponent<Collider>();
        if (!col) Debug.LogError("LorePickup: Falta Collider en el MISMO GO del script.", this);

        // Aviso si el collider está en un hijo (común cuando el atributo RequireComponent se “duplica”)
        if (col && !col.isTrigger)
        {
            Debug.LogWarning("LorePickup: El Collider no está marcado como Trigger. Activándolo en tiempo de ejecución.", this);
            col.isTrigger = true;
        }
    }

    [ContextMenu("Debug/Pick (simulate)")]
    private void DebugPick() => DoPick(null);

    private void OnTriggerEnter(Collider other)
    {
        if (_taken) return;

        if (!other.CompareTag(playerTag))
        {
            // Esto ayuda a detectar un Tag mal configurado
            // (si molesta el spam, convertir a Log una sola vez).
            return;
        }

        DoPick(other.transform);
    }

    private void DoPick(Transform picker)
    {
        // Encontrar sistema
        var system = FindFirstObjectByType<LoreSystem>();
        if (!system)
        {
            Debug.LogError("LorePickup: No existe LoreSystem en escena. No puedo desbloquear.", this);
            return;
        }

        bool unlockedNow = system.Unlock(entryId); // false si id inválido o ya desbloqueado

        if (!unlockedNow)
        {
            if (string.IsNullOrWhiteSpace(entryId))
                Debug.LogWarning("LorePickup: entryId vacío.", this);
            else if (!system.IsUnlocked(entryId))
                Debug.LogWarning($"LorePickup: entryId '{entryId}' NO existe en la Database.", this);
            else
                Debug.Log($"LorePickup: entryId '{entryId}' ya estaba desbloqueado.", this);

            if (!alwaysDestroyOnTrigger && !hideOnPickup)
            {
                // Nada más que hacer, no destruimos (comportamiento por defecto)
                return;
            }
        }

        // Marcamos tomado y reproducimos feedback
        _taken = true;
        if (sfxOnPickup) sfxOnPickup.Play();
        if (vfxPrefab) Instantiate(vfxPrefab, transform.position, Quaternion.identity);

        onPicked?.Invoke();

        if (hideOnPickup) SetRenderersEnabled(false);

        if (destroyOnPickup || alwaysDestroyOnTrigger)
        {
            if (destroyDelay > 0f) Destroy(gameObject, destroyDelay);
            else Destroy(gameObject);
        }
        else
        {
            // Al menos deshabilitar el collider si ya se tomó
            var col = GetComponent<Collider>();
            if (col) col.enabled = false;
        }
    }

    private void SetRenderersEnabled(bool enabled)
    {
        foreach (var r in GetComponentsInChildren<Renderer>(true))
            r.enabled = enabled;
    }
}
