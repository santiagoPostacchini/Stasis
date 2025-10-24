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
    [Tooltip("Destruir este GameObject al recoger.")]
    public bool destroyOnPickup = true;
    [Tooltip("Ocultar renderers al recoger (si no destruimos).")]
    public bool hideOnPickup = false;

    [Header("Feedback (opc)")]
    [Tooltip("SFX al recoger.")]
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

    [ContextMenu("Debug/Pick (simulate)")]
    private void DebugPick() => DoPick(null);

    private void OnTriggerEnter(Collider other)
    {
        if (_taken) return;
        if (!other.CompareTag(playerTag)) return;
        DoPick(other.transform);
    }

    private void DoPick(Transform picker)
    {
        var system = FindFirstObjectByType<LoreSystem>();
        if (!system)
        {
            Debug.LogError("LorePickup: No existe LoreSystem en escena.");
            return;
        }

        if (system.Unlock(entryId))
        {
            _taken = true;
            if (sfxOnPickup) sfxOnPickup.Play();
            if (vfxPrefab) Instantiate(vfxPrefab, transform.position, Quaternion.identity);

            onPicked?.Invoke();

            if (hideOnPickup)
                SetRenderersEnabled(false);

            if (destroyOnPickup)
                Destroy(gameObject);
        }
    }

    private void SetRenderersEnabled(bool enabled)
    {
        foreach (var r in GetComponentsInChildren<Renderer>(true))
            r.enabled = enabled;
    }
}
