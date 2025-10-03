using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

namespace ObjectPool
{
    public class AudioSourceFactory : MonoBehaviour
    {
        public static AudioSourceFactory Instance;

        [Header("Prefab")]
        public AudioSource prefab;

        [Header("Pooling")]
        public int stonks = 15;     // cantidad inicial
        public bool dynamic = true; // permite crecer si se agota

        private ObjectPool<AudioSource> _pool;

        private void Awake()
        {
            Instance = this;

            if (!prefab)
            {
                Debug.LogError("[AudioSourceFactory] Asigná el prefab de AudioSource.", this);
                enabled = false;
                return;
            }

            _pool = new ObjectPool<AudioSource>(
                factoryMethod: CreateInstance,
                callback: TurnOnOff,
                initialStonks: stonks,
                dynamic: dynamic
            );
        }

        // ---------- Core pooling ----------
        private AudioSource CreateInstance()
        {
            var inst = Instantiate(prefab, transform);
            return inst;
        }

        private static void TurnOnOff(AudioSource src, bool on)
        {
            if (!src) return;

            src.gameObject.SetActive(on);

            if (!on)
            {
                // reset seguro al devolver al pool
                src.Stop();
                src.clip = null;
                src.loop = false;
                src.pitch = 1f;
                src.volume = 1f;
            }
        }

        /// <summary>Obtiene una instancia del pool.</summary>
        public AudioSource GetSource()
        {
            return _pool.GetObject();
        }

        /// <summary>Devuelve una instancia al pool.</summary>
        public void ReturnSource(AudioSource src)
        {
            if (!src) return;
            _pool.ReturnObject(src);
        }

        // ---------- Helpers opcionales (auto-return si no es loop) ----------
        /// <summary>
        /// Reproduce un clip en una posición y lo devuelve al terminar si no es loop.
        /// Para loops, devolvelo manualmente con ReturnSource().
        /// </summary>
        public AudioSource PlayClipAt(
            Vector3 position,
            AudioClip clip,
            float volume = 1f,
            float pitch = 1f,
            bool loop = false,
            float spatialBlend = 1f,
            AudioMixerGroup mixerOverride = null,
            bool autoReturnNonLoop = true)
        {
            if (!clip) return null;

            var src = GetSource();
            src.transform.position = position;
            SetupAndPlay(src, clip, volume, pitch, loop, spatialBlend, mixerOverride);

            if (!loop && autoReturnNonLoop)
                StartCoroutine(Co_AutoReturn(src, clip.length, pitch));

            return src;
        }

        /// <summary>
        /// Reproduce un clip siguiendo un transform y lo devuelve al terminar si no es loop.
        /// </summary>
        public AudioSource PlayClipFollowing(
            Transform follow,
            AudioClip clip,
            float volume = 1f,
            float pitch = 1f,
            bool loop = false,
            float spatialBlend = 1f,
            AudioMixerGroup mixerOverride = null,
            bool autoReturnNonLoop = true)
        {
            if (!clip || !follow) return null;

            var src = GetSource();
            src.transform.position = follow.position;
            SetupAndPlay(src, clip, volume, pitch, loop, spatialBlend, mixerOverride);

            StartCoroutine(Co_FollowWhilePlaying(src, follow, loop));

            if (!loop && autoReturnNonLoop)
                StartCoroutine(Co_AutoReturn(src, clip.length, pitch));

            return src;
        }

        /// <summary>
        /// OneShot en posición (también auto-devuelve tras la duración aproximada).
        /// </summary>
        public AudioSource PlayOneShotAt(
            Vector3 position,
            AudioClip clip,
            float volume = 1f,
            float pitch = 1f,
            float spatialBlend = 1f,
            AudioMixerGroup mixerOverride = null,
            bool autoReturn = true)
        {
            if (!clip) return null;

            var src = GetSource();
            src.transform.position = position;

            // configurar mixer/blend/pitch para el oneshot
            if (mixerOverride) src.outputAudioMixerGroup = mixerOverride;

            src.spatialBlend = Mathf.Clamp01(spatialBlend);
            src.pitch = pitch;
            src.PlayOneShot(clip, Mathf.Clamp01(volume));

            if (autoReturn)
                StartCoroutine(Co_AutoReturn(src, clip.length, pitch));

            return src;
        }

        /// <summary>Detiene el source y lo devuelve al pool.</summary>
        public void StopAndReturn(AudioSource src)
        {
            if (!src) return;
            src.Stop();
            ReturnSource(src);
        }

        // ---------- Internals ----------
        private void SetupAndPlay(
            AudioSource src,
            AudioClip clip,
            float volume,
            float pitch,
            bool loop,
            float spatialBlend,
            AudioMixerGroup mixerOverride)
        {
            if (mixerOverride) src.outputAudioMixerGroup = mixerOverride;
            src.clip = clip;
            src.volume = Mathf.Clamp01(volume);
            src.pitch = pitch;
            src.loop = loop;
            src.spatialBlend = Mathf.Clamp01(spatialBlend);
            src.Play();
        }

        private IEnumerator Co_AutoReturn(AudioSource src, float clipLength, float pitch)
        {
            // duración aproximada considerando pitch
            float t = 0f;
            float dur = clipLength / Mathf.Max(0.01f, Mathf.Abs(pitch));
            while (t < dur && src && src.isActiveAndEnabled)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            ReturnSource(src);
        }

        private IEnumerator Co_FollowWhilePlaying(AudioSource src, Transform follow, bool loop)
        {
            // sigue el transform mientras el clip se reproduce
            while (src && follow && src.isPlaying)
            {
                src.transform.position = follow.position;
                yield return null;
            }

            // si era loop, no auto-devolvemos (espera StopAndReturn del caller)
            if (!loop && src)
                ReturnSource(src);
        }
    }
}