using UnityEngine;
using System.Collections;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(BoxCollider))]
public class JumpPadVolumeKick : MonoBehaviour
{
    [Header("Refs")]
    public Volume padVolume;         // Volume local del pad (Profile con overrides)
    public string playerTag = "Player";

    [Header("Weights")]
    [Range(0,1)] public float peakWeight = 1f;
    [Range(0,1)] public float holdWeight = 0.6f;
    [Range(0,1)] public float outsideWeight = 0f;

    [Header("Timings")]
    public float riseTime = 0.12f;   // subida al pico
    public float fallTime = 0.22f;   // caída a sostenido
    public float exitTime = 0.18f;   // salir del área

    // Opcional: animar 1–2 parámetros para enfatizar
    [Header("Optional Param Pulses")]
    public bool pulseChromaticAberration = true;
    public float caPeak = 0.18f;
    public float caHold = 0.05f;

    public bool pulseLensDistortion = true;
    public float ldPeak = -0.22f;
    public float ldHold = -0.08f;

    ChromaticAberration ca;
    LensDistortion ld;

    Coroutine anim;

    void Reset() {
        var col = GetComponent<BoxCollider>();
        col.isTrigger = true;
        padVolume = GetComponent<Volume>();
    }

    void Awake() {
        if (padVolume != null && padVolume.profile != null) {
            padVolume.profile.TryGet(out ca);
            padVolume.profile.TryGet(out ld);
        }
    }

    void OnTriggerEnter(Collider other) {
        if (!other.CompareTag(playerTag)) return;
        if (anim != null) StopCoroutine(anim);
        anim = StartCoroutine(KickRoutine());
    }

    void OnTriggerExit(Collider other) {
        if (!other.CompareTag(playerTag)) return;
        if (anim != null) StopCoroutine(anim);
        anim = StartCoroutine(LerpWeight(padVolume.weight, outsideWeight, exitTime));
        // Opcional: relajar params al salir
        if (ca) ca.intensity.value = 0f;
        if (ld) ld.intensity.value = 0f;
    }

    IEnumerator KickRoutine() {
        // 1) Subida rápida al pico
        yield return LerpWeight(padVolume.weight, peakWeight, riseTime);
        // Pulsos de parámetros en el pico
        if (pulseChromaticAberration && ca) ca.intensity.value = caPeak;
        if (pulseLensDistortion && ld)  ld.intensity.value = ldPeak;
        // 2) Caída a sostenido
        yield return LerpWeight(peakWeight, holdWeight, fallTime);
        if (pulseChromaticAberration && ca) ca.intensity.value = caHold;
        if (pulseLensDistortion && ld)  ld.intensity.value = ldHold;
        anim = null;
    }

    IEnumerator LerpWeight(float a, float b, float t) {
        float time = 0f;
        while (time < t) {
            time += Time.deltaTime;
            float k = t > 0f ? time / t : 1f;
            if (padVolume) padVolume.weight = Mathf.Lerp(a, b, k);
            yield return null;
        }
        if (padVolume) padVolume.weight = b;
    }
}
