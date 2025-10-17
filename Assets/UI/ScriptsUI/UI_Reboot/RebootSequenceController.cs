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

    // --- Fade final de TEXTO (siempre se ejecuta al terminar la secuencia) ---
    [Header("Fade final de texto")]
    [SerializeField] private bool fadeTextOnEnd = true;
    [SerializeField] private float endFadeDelay = 4f;
    [SerializeField] private float endFadeDuration = 1.25f;
    [SerializeField] private bool clearTextOnEnd = true;
    [SerializeField] private bool deactivateTextGOOnEnd = false;

    private bool _skipping;

    // ---------- LIFECYCLE ----------
    private void Reset()
    {
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

        if (consoleText != null)
        {
            consoleText.text = string.Empty;
            // Asegura visibilidad y estado coherente del renderer
            consoleText.alpha = 1f;
            consoleText.canvasRenderer.SetAlpha(1f);
        }
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
        consoleText.alpha = 1f;
        consoleText.canvasRenderer.SetAlpha(1f);

        foreach (var step in asset.steps)
        {
            float typing = step.typingSpeed > 0 ? step.typingSpeed : asset.defaultTypingSpeed;
            float delay = step.afterDelay > 0 ? step.afterDelay : asset.defaultAfterDelay;

            yield return StartCoroutine(TypeLine(step.text, typing, step.playTypeBeep));

            if (step.playLineBeep) Play(asset.lineBeep);

            yield return StartCoroutine(DelayOrSkip(delay));
        }

        Play(asset.finishSfx);

        // 1) SIEMPRE: Fade del TEXTO tras esperar endFadeDelay (por defecto 4s)
        if (fadeTextOnEnd && consoleText != null)
        {
            yield return StartCoroutine(DelayOrSkip(endFadeDelay));
            yield return StartCoroutine(FadeTMPText(consoleText, 1f, 0f, endFadeDuration));

            if (clearTextOnEnd) consoleText.text = string.Empty;
            if (deactivateTextGOOnEnd) consoleText.gameObject.SetActive(false);
        }

        // 2) OPCIONAL: luego, si el asset lo pide, fade del Canvas completo
        if (asset.autoFadeOut && canvasGroup != null)
        {
            yield return StartCoroutine(DelayOrSkip(asset.fadeDelay));
            yield return StartCoroutine(FadeCanvas(canvasGroup, 1f, 0f, asset.fadeDuration));
        }

        onSequenceEnd?.Invoke();
        // Al final de RunSequence()
        if (canvasGroup != null)
            canvasGroup.gameObject.SetActive(false);
        else
            gameObject.SetActive(false);
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

    // --- Fade robusto de TMP usando CanvasRenderer (ignora timeScale y respeta 'skip') ---
    private IEnumerator FadeTMPText(TextMeshProUGUI tmp, float from, float to, float duration)
    {
        if (tmp == null) yield break;

        // Normaliza estado inicial
        tmp.alpha = 1f; // mantiene colores/material
        tmp.canvasRenderer.SetAlpha(from);

        if (duration <= 0f)
        {
            tmp.canvasRenderer.SetAlpha(to);
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            if (Input.GetKey(skipKey)) { tmp.canvasRenderer.SetAlpha(to); break; }
            t += Time.unscaledDeltaTime;
            float a = Mathf.Lerp(from, to, t / duration);
            tmp.canvasRenderer.SetAlpha(a);
            yield return null;
        }

        tmp.canvasRenderer.SetAlpha(to);
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
