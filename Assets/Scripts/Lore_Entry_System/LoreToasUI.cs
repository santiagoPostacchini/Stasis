using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public class LoreToastUI : MonoBehaviour
{
    [Header("Refs (UI)")]
    [Tooltip("CanvasGroup del cartel/hint.")]
    public CanvasGroup group;
    [Tooltip("Texto (TMP) del hint. Ej: 'Press I to read new entry'")]
    public TMP_Text message;
    [Tooltip("Duración (seg) visible antes de ocultar (0 = no autohide).")]
    public float autoHideSeconds = 0f;

    [Header("Texto por defecto")]
    [SerializeField] private string defaultText = "Press I to read new entry";

    private float _tHide = -1f;

    void Awake()
    {
        if (!group) group = GetComponentInChildren<CanvasGroup>(true);
        HideImmediate();
        var system = FindFirstObjectByType<LoreSystem>();
        if (system) system.OnEntryUnlocked += OnEntryUnlocked;
    }

    void OnDestroy()
    {
        var system = FindFirstObjectByType<LoreSystem>();
        if (system) system.OnEntryUnlocked -= OnEntryUnlocked;
    }

    private void OnEntryUnlocked(LoreEntry e)
    {
        if (message) message.text = defaultText;
        Show();
        if (autoHideSeconds > 0f) _tHide = Time.unscaledTime + autoHideSeconds;
    }

    void Update()
    {
        if (_tHide > 0f && Time.unscaledTime >= _tHide)
        {
            _tHide = -1f;
            Hide();
        }
    }

    [ContextMenu("Debug/Show")]
    public void Show() { SetAlpha(1f); }

    [ContextMenu("Debug/Hide")]
    public void Hide() { SetAlpha(0f); }

    private void HideImmediate() => SetAlpha(0f);

    private void SetAlpha(float a)
    {
        if (!group) return;
        group.alpha = a;
        group.interactable = a > 0.99f;
        group.blocksRaycasts = a > 0.99f;
    }
}