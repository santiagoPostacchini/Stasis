using UnityEngine;
using System.Collections;
using UnityEngine.Rendering;

[RequireComponent(typeof(BoxCollider))]
public class VolumePulseOnEnter : MonoBehaviour
{
    [Header("Refs")]
    public Volume targetVolume;          // Asigná el Volume local del pad
    public string playerTag = "Player";  // O referencia directa al Player

    [Header("Pulse")]
    [Tooltip("Peso normal mientras estás dentro (siempre que no esté pulsing).")]
    [Range(0f, 1f)] public float steadyWeightInside = 1f;
    [Tooltip("Peso pico apenas entrás (se lerpea).")]
    [Range(0f, 1f)] public float enterPeakWeight = 1f;
    [Tooltip("Tiempo para subir al pico.")]
    public float riseTime = 0.15f;
    [Tooltip("Tiempo para bajar del pico al steady.")]
    public float fallTime = 0.25f;

    [Header("On Exit")]
    [Tooltip("Peso al salir (normalmente 0).")]
    [Range(0f, 1f)] public float weightOutside = 0f;
    [Tooltip("Tiempo al salir para volver a fuera.")]
    public float exitTime = 0.2f;

    Coroutine anim;

    void Reset()
    {
        var col = GetComponent<BoxCollider>();
        col.isTrigger = true;
        targetVolume = GetComponent<Volume>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!MatchesPlayer(other)) return;
        if (anim != null) StopCoroutine(anim);
        anim = StartCoroutine(PulseEnter());
    }

    void OnTriggerStay(Collider other)
    {
        // Asegura que, si ya pasó el pulso, quede en el steady dentro.
        if (!MatchesPlayer(other)) return;
        if (anim == null && targetVolume != null)
            targetVolume.weight = Mathf.MoveTowards(targetVolume.weight, steadyWeightInside, Time.deltaTime * 4f);
    }

    void OnTriggerExit(Collider other)
    {
        if (!MatchesPlayer(other)) return;
        if (anim != null) StopCoroutine(anim);
        anim = StartCoroutine(LerpWeight(targetVolume.weight, weightOutside, exitTime));
    }

    bool MatchesPlayer(Collider c) => string.IsNullOrEmpty(playerTag) || c.CompareTag(playerTag);

    IEnumerator PulseEnter()
    {
        if (targetVolume == null) yield break;
        // Subir rápido al pico
        yield return LerpWeight(targetVolume.weight, enterPeakWeight, riseTime);
        // Bajar suave al steady dentro
        yield return LerpWeight(targetVolume.weight, steadyWeightInside, fallTime);
        anim = null;
    }

    IEnumerator LerpWeight(float a, float b, float t)
    {
        if (targetVolume == null) yield break;
        float time = 0f;
        while (time < t)
        {
            time += Time.deltaTime;
            float k = t > 0f ? time / t : 1f;
            targetVolume.weight = Mathf.Lerp(a, b, k);
            yield return null;
        }
        targetVolume.weight = b;
    }
}
