using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BootSequenceAsset", menuName = "UI/Boot Sequence Asset")]
public class BootSequenceAsset : ScriptableObject
{
    [System.Serializable]
    public class BootStep
    {
        [TextArea(1, 4)] public string text = "SYSTEM: ONLINE";
        public float typingSpeed = -1f;   // -1 = usa default
        public float afterDelay = -1f;    // -1 = usa default
        public bool playTypeBeep = true;
        public bool playLineBeep = false;
    }

    public List<BootStep> steps = new List<BootStep>();

    [Header("Defaults")]
    public float defaultTypingSpeed = 0.03f;
    public float defaultAfterDelay = 0.4f;

    [Header("Audio (opcional)")]
    public AudioClip typeBeep;     // sonidito por carácter (suave)
    public AudioClip lineBeep;     // beep al terminar una línea
    public AudioClip finishSfx;    // sfx al terminar toda la secuencia

    [Header("Cierre")]
    public bool autoFadeOut = true;
    public float fadeDelay = 1.0f;
    public float fadeDuration = 0.75f;
}
