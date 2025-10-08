using UnityEngine;
using System.Collections;

public class CanvasManager : MonoBehaviour
{
    [Header("Referencias")]
    public CanvasGroup canvasGroup;          // CanvasGroup del canvas principal
    public RagdollHanger ragdollHanger;      // Referencia al script del hanger

    [Header("Fade Inicial")]
    public float startFadeDelay = 2f;
    public float startFadeDuration = 5f;

    [Header("Fade Después de FadeBlack")]
    public float fadeBlackDelay = 2f;
    public float fadeBlackDuration = 5f;

    [Header("Canvas Extra al Release")]
    public Canvas extraCanvas;               // Canvas que se activa al Release
    public float extraCanvasActiveTime = 3f; // Tiempo antes de desactivarlo

    private bool hasStartedFade = false;
    private bool hasReappeared = false;
    private bool hasFadedAfterBlack = false;
    private bool hasActivatedExtraCanvas = false;

    void Start()
    {
        if (canvasGroup == null)
        {
            Debug.LogWarning("Falta asignar el CanvasGroup en CanvasManager");
            return;
        }

        canvasGroup.alpha = 1f;

        if (extraCanvas != null)
            extraCanvas.gameObject.SetActive(false); // Empieza desactivado

        StartCoroutine(FadeOutAfterDelay());
    }

    void Update()
    {
        if (ragdollHanger == null || canvasGroup == null)
            return;

        // Cuando se activa fadeBlack → cortar canvas extra
        if (ragdollHanger.fadeBlack && extraCanvas != null && extraCanvas.gameObject.activeSelf)
        {
            extraCanvas.gameObject.SetActive(false);
        }

        // Cuando se activa fadeBlack en canvas principal
        if (ragdollHanger.fadeBlack && !hasReappeared)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            hasReappeared = true;

            StartCoroutine(FadeOutAfterFadeBlack());
        }

        // Cuando se libera el ragdoll
        if (ragdollHanger.hasReleased && !hasActivatedExtraCanvas && extraCanvas != null)
        {
            hasActivatedExtraCanvas = true;
            StartCoroutine(ActivateExtraCanvas());
        }
    }

    private IEnumerator FadeOutAfterDelay()
    {
        yield return new WaitForSeconds(startFadeDelay);

        hasStartedFade = true;
        float elapsed = 0f;
        float startAlpha = canvasGroup.alpha;

        while (elapsed < startFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / startFadeDuration;
            canvasGroup.alpha = Mathf.SmoothStep(startAlpha, 0f, t);
            yield return null;
        }

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    private IEnumerator FadeOutAfterFadeBlack()
    {
        yield return new WaitForSeconds(fadeBlackDelay);

        float elapsed = 0f;
        float startAlpha = canvasGroup.alpha;

        while (elapsed < fadeBlackDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeBlackDuration;
            canvasGroup.alpha = Mathf.SmoothStep(startAlpha, 0f, t);
            yield return null;
        }

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        hasFadedAfterBlack = true;
    }

    private IEnumerator ActivateExtraCanvas()
    {
        extraCanvas.gameObject.SetActive(true);

        yield return new WaitForSeconds(extraCanvasActiveTime);

        if (extraCanvas != null)
            extraCanvas.gameObject.SetActive(false);
    }
}




