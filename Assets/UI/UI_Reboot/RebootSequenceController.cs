using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using TMPro;

[DisallowMultipleComponent]
[AddComponentMenu("UI/Reboot Sequence Controller")]
public class RebootSequenceController : MonoBehaviour
{
    [Header("Asset (arrastrá o usa ResourcesPath)")]
    [SerializeField] private BootSequenceAsset asset;
    [Tooltip("Opcional: si 'asset' está vacío, intenta cargar desde Resources con esta ruta (sin .asset). Ej: UI/Boot/BootSequence_Default")]
    [SerializeField] private string resourcesPath;

    [Header("UI (autobind si quedan vacíos)")]
    [SerializeField] private TextMeshProUGUI consoleText;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Control")]
    [SerializeField] private KeyCode skipKey = KeyCode.Space;   // mantener para saltar tipeo/delays
    [SerializeField] private bool startOnAwake = true;

    [Header("Audio (autobind si queda vacío)")]
    [SerializeField] private AudioSource audioSource; // opcional

    [Header("Events")]
    public UnityEvent onSequenceStart;
    public UnityEvent onSequenceEnd;

    private bool _skipping;

    // ---------- LIFECYCLE ----------
    private void Reset()
    {
        // Intento de autocompletar refs al agregar el componente
        TryAutoBind(true);
    }

    private void Awake()
    {
        if (asset == null && !string.IsNullOrWhiteSpace(resourcesPath))
        {
            asset = UnityEngine.Resources.Load<BootSequenceAsset>(resourcesPath);
            if (asset == null)
                Debug.LogWarning($"[RebootSequenceController] No se encontró BootSequenceAsset en Resources: '{resourcesPath}'.");
        }

        TryAutoBind(false);

        if (canvasGroup != null) canvasGroup.alpha = 1f;
        if (consoleText != null) consoleText.text = string.Empty;
    }


    private void Start()
    {
        if (!ValidateReady()) return;
        if (startOnAwake) StartSequence();
    }

    // ---------- PUBLIC ----------
    public void SetAsset(BootSequenceAsset newAsset) => asset = newAsset;

    public void StartSequence()
    {
        if (!ValidateReady()) return;
        StopAllCoroutines();
        StartCoroutine(RunSequence());
    }

    // ---------- CORE ----------
    private IEnumerator RunSequence()
    {
        onSequenceStart?.Invoke();

        consoleText.text = string.Empty;

        foreach (var step in asset.steps)
        {
            float typing = step.typingSpeed > 0 ? step.typingSpeed : asset.defaultTypingSpeed;
            float delay  = step.afterDelay   > 0 ? step.afterDelay   : asset.defaultAfterDelay;

            yield return StartCoroutine(TypeLine(step.text, typing, step.playTypeBeep));

            if (step.playLineBeep) Play(asset.lineBeep);

            yield return StartCoroutine(DelayOrSkip(delay));
        }

        Play(asset.finishSfx);

        if (asset.autoFadeOut && canvasGroup != null)
        {
            yield return StartCoroutine(DelayOrSkip(asset.fadeDelay));
            yield return StartCoroutine(FadeCanvas(canvasGroup, 1f, 0f, asset.fadeDuration));
        }

        onSequenceEnd?.Invoke();
    }

    private IEnumerator TypeLine(string line, float typingSpeed, bool beepPerChar)
    {
        for (int i = 0; i < line.Length; i++)
        {
            if (_skipping)
            {
                consoleText.text += line.Substring(i);
                break;
            }

            consoleText.text += line[i];

            if (beepPerChar && (i % 2 == 0)) Play(asset.typeBeep);

            float elapsed = 0f;
            while (elapsed < typingSpeed && !_skipping)
            {
                if (Input.GetKey(skipKey)) _skipping = true;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        consoleText.text += "\n";
        _skipping = false;
    }

    private IEnumerator DelayOrSkip(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            if (Input.GetKey(skipKey)) break;
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        _skipping = false;
    }

    private IEnumerator FadeCanvas(CanvasGroup cg, float from, float to, float duration)
    {
        float t = 0f;
        cg.alpha = from;
        while (t < duration)
        {
            if (Input.GetKey(skipKey)) { cg.alpha = to; break; }
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        cg.alpha = to;
    }

    private void Play(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }

    // ---------- HELPERS ----------
    private void TryAutoBind(bool inReset)
    {
        if (consoleText == null)
        {
            consoleText = GetComponentInChildren<TextMeshProUGUI>(true);
            if (!inReset && consoleText == null)
                Debug.LogWarning("[RebootSequenceController] No se encontró TextMeshProUGUI hijo. Asignalo en el Inspector.");
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = GetComponentInChildren<CanvasGroup>(true);

            if (!inReset && canvasGroup == null)
                Debug.LogWarning("[RebootSequenceController] No se encontró CanvasGroup. Asignalo en el Inspector (Panel_Backdrop).");
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = GetComponentInChildren<AudioSource>(true);
            // (sin warning: es opcional)
        }
    }

    private bool ValidateReady()
    {
        if (asset == null)
        {
            Debug.LogError("[RebootSequenceController] 'asset' no asignado y no se pudo cargar por Resources. Asigná un BootSequenceAsset.");
            return false;
        }
        if (consoleText == null)
        {
            Debug.LogError("[RebootSequenceController] 'consoleText' no asignado. Asigná un TextMeshProUGUI en el Canvas.");
            return false;
        }
        return true;
    }
}
