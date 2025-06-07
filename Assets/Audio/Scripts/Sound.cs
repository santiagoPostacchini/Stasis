using UnityEngine;
using UnityEngine.Audio;

namespace Audio.Scripts
{
    [CreateAssetMenu(menuName = "Assets/Audio/Audio Objects")]
    public class Sound : ScriptableObject
    {
        [Tooltip("Nombre del sonido")]
        public string soundName;

        [Tooltip("Clip de audio")]
        public AudioClip clip;

        [Tooltip("Grupo del AudioMixer")]
        public AudioMixerGroup mixerGroup;

        [Tooltip("Se reproduce autom�ticamente al iniciar la escena?")]
        public bool playOnAwake;

        [Tooltip("Se repite en bucle?")]
        public bool loop;

        [Range(0f, 1f), Tooltip("Volumen inicial")]
        public float volume = 1f;
    }
}
