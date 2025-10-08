using UnityEngine;

public class CamHolderRotationReset : MonoBehaviour
{
    [Header("Rotación inicial (en grados)")]
    public Vector3 initialRotation = new Vector3(15f, 0f, 0f);

    [Header("Tiempo antes de iniciar el Lerp (segundos)")]
    public float waitTime = 2f;

    [Header("Duración del Lerp (segundos)")]
    public float lerpDuration = 1.5f;

    [Header("Curva de transición (0 a 1)")]
    public AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Referencia al AnimatorManager")]
    public AnimatorManager animatorManager;

    private Quaternion startRotation;
    private Quaternion targetRotation = Quaternion.identity;
    private bool isLerping = false;
    private float lerpTimer = 0f;

    private bool hasTriggeredLerp = false;

    void Start()
    {
        // Aplicar rotación inicial
        transform.localRotation = Quaternion.Euler(initialRotation);
        startRotation = transform.localRotation;
    }

    void Update()
    {
        if (animatorManager == null)
            return;

        // Esperar a que el AnimatorManager active el Animator
        if (animatorManager.animatorActivated && !hasTriggeredLerp)
        {
            hasTriggeredLerp = true;
            Invoke(nameof(BeginLerp), waitTime);
        }

        if (!isLerping)
            return;

        lerpTimer += Time.deltaTime;
        float t = Mathf.Clamp01(lerpTimer / lerpDuration);

        float curvedT = transitionCurve.Evaluate(t);
        transform.localRotation = Quaternion.Lerp(startRotation, targetRotation, curvedT);

        if (t >= 1f)
            isLerping = false;
    }

    void BeginLerp()
    {
        // Volver a la rotación inicial antes de iniciar
        transform.localRotation = Quaternion.Euler(initialRotation);
        startRotation = transform.localRotation;

        isLerping = true;
        lerpTimer = 0f;
    }
}



