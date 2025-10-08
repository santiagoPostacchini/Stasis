using UnityEngine;
using System.Collections;

public class CanvasManager : MonoBehaviour
{
    [Header("Referencias")]
    public CanvasGroup canvasGroup;          // CanvasGroup del canvas
    public RagdollHanger ragdollHanger;      // Referencia al script del hanger

    [Header("Fade Inicial")]
    public float startFadeDelay = 2f;        // Segundos antes de empezar el fade inicial
    public float startFadeDuration = 5f;     // Duración del fade inicial

    [Header("Fade Después de FadeBlack")]
    public float fadeBlackDelay = 2f;        // Segundos antes de empezar el fade después de fadeBlack
    public float fadeBlackDuration = 5f;     // Duración del fade después de fadeBlack

    private bool hasStartedFade = false;
    private bool hasReappeared = false;
    private bool hasFadedAfterBlack = false;

    void Start()
    {
        if (canvasGroup == null)
        {
            Debug.LogWarning("Falta asignar el CanvasGroup en CanvasManager");
            return;
        }

        canvasGroup.alpha = 1f;

        StartCoroutine(FadeOutAfterDelay());
    }

    void Update()
    {
        if (ragdollHanger == null || canvasGroup == null)
            return;

        // Cuando se activa fadeBlack
        if (ragdollHanger.fadeBlack && !hasReappeared)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            hasReappeared = true;

            // Inicia fade después del delay
            StartCoroutine(FadeOutAfterFadeBlack());
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
}



