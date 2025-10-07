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

    private Quaternion startRotation;
    private Quaternion targetRotation = Quaternion.identity;
    private bool isLerping = false;
    private float lerpTimer = 0f;

    void Start()
    {
        // Aplicar rotación inicial
        transform.localRotation = Quaternion.Euler(initialRotation);
        startRotation = transform.localRotation;

        // Esperar antes de iniciar el Lerp
        Invoke(nameof(BeginLerp), waitTime);
    }

    void BeginLerp()
    {
        isLerping = true;
        lerpTimer = 0f;
    }

    void Update()
    {
        if (!isLerping)
            return;

        lerpTimer += Time.deltaTime;
        float t = Mathf.Clamp01(lerpTimer / lerpDuration);

        // Aplicar curva a la progresión
        float curvedT = transitionCurve.Evaluate(t);

        // Interpolar con la curva
        transform.localRotation = Quaternion.Lerp(startRotation, targetRotation, curvedT);

        if (t >= 1f)
            isLerping = false;
    }
}

