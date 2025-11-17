using Player.Scripts.MovementFSM.MVC;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
[RequireComponent(typeof(Volume))]
public class ColdHazardVolumeLink : MonoBehaviour
{
    [Header("Player Model (hazard source)")]
    [Tooltip("Model del jugador que expone hazardSpeedMultiplier. Asignar por inspector.")]
    public Model playerModel;

    [Header("Curva de intensidad")]
    [Tooltip("X: peligro (0 = normal, 1 = al borde de la muerte). Y: intensidad visual (0–1).")]
    public AnimationCurve dangerToEffect = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Volume Weight")]
    [Tooltip("Weight máximo que se aplica al Volume.")]
    [Range(0f, 1f)] public float maxWeight = 1f;

    [Header("Vignette (frío)")]
    public bool driveVignette = true;
    [Range(0f, 1f)] public float minVignette = 0f;
    [Range(0f, 1f)] public float maxVignette = 0.45f;
    public Color coldVignetteColor = new Color(0.6f, 0.8f, 1.0f, 1f);

    [Header("Color / White Balance")]
    public bool driveWhiteBalance = true;
    [Tooltip("Temperatura mínima (más azul) al máximo peligro.")]
    public float minTemperature = -40f;
    [Tooltip("Temperatura neutra cuando no hay peligro.")]
    public float maxTemperature = 0f;

    public bool driveColorAdjustments = true;
    [Tooltip("Saturación extra negativa al máximo peligro (desaturado/helado).")]
    public float minSaturation = -20f;
    public float maxSaturation = 0f;

    [Header("Chromatic Aberration (sutil)")]
    public bool driveChromaticAberration = true;
    [Range(0f, 1f)] public float minChromatic = 0f;
    [Range(0f, 1f)] public float maxChromatic = 0.25f;

    private Volume _volume;
    private VolumeProfile _profile;

    void Awake()
    {
        _volume = GetComponent<Volume>();
        _profile = _volume.profile;

        if (!playerModel)
        {
            Debug.LogWarning($"[{name}] ColdHazardVolumeLink no tiene asignado playerModel. " +
                             "Asigna el Model del jugador por inspector.");
        }
    }

    void LateUpdate()
    {
        if (!playerModel || _profile == null)
            return;

        float m = Mathf.Clamp01(playerModel.hazardSpeedMultiplier);
        float danger = 1f - m; // 0 = normal, 1 = a punto de morir

        float k = Mathf.Clamp01(dangerToEffect.Evaluate(danger));

        // Volume weight
        _volume.weight = maxWeight * k;

        // Vignette frío
        if (driveVignette && _profile.TryGet(out Vignette vignette))
        {
            vignette.intensity.value = Mathf.Lerp(minVignette, maxVignette, k);
            vignette.color.value = coldVignetteColor;
        }

        // White Balance (temperatura)
        if (driveWhiteBalance && _profile.TryGet(out WhiteBalance whiteBalance))
        {
            float temp = Mathf.Lerp(maxTemperature, minTemperature, k);
            whiteBalance.temperature.value = temp;
        }

        // Color Adjustments (saturación)
        if (driveColorAdjustments && _profile.TryGet(out ColorAdjustments colorAdj))
        {
            float sat = Mathf.Lerp(maxSaturation, minSaturation, k);
            colorAdj.saturation.value = sat;
        }

        // Chromatic aberration suave
        if (driveChromaticAberration && _profile.TryGet(out ChromaticAberration ca))
        {
            ca.intensity.value = Mathf.Lerp(minChromatic, maxChromatic, k);
        }
    }
}
