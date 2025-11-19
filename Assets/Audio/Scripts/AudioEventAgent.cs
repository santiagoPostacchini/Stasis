using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Audio;

namespace Audio.Scripts
{
    [DisallowMultipleComponent]
    public class AudioEventAgent : MonoBehaviour
    {
        [Header("Targets")]
        [Tooltip("Componentes a escanear para detectar eventos/campos delegado sin parámetros.")]
        [SerializeField] private List<MonoBehaviour> targetScripts = new();

        [Header("Mixer / Defaults")]
        [SerializeField] private AudioMixerGroup defaultMixerGroup;

        [Tooltip("Opcional: template de AudioSource para aplicar a las instancias del pool.")]
        [SerializeField] private AudioSource sourceTemplate;

        [Header("Emitters")]
        [Tooltip("Override general para la posición de emisión. Si está vacío, se usa este transform.")]
        [SerializeField] private Transform globalEmitterOverride;

        [Header("Inspector")]
        [SerializeField] private List<EventConfig> events = new();

        [Header("Debug")]
        [SerializeField] private bool debugScan;

        // API para el Hub
        public IList<MonoBehaviour> TargetScripts => targetScripts;
        public AudioMixerGroup DefaultMixerGroup => defaultMixerGroup;
        public AudioSource SourceTemplate => sourceTemplate;
        public Transform GlobalEmitterOverride => globalEmitterOverride;
        public IReadOnlyList<EventConfig> EventConfigs => events;

        private void OnEnable()
        {
            AudioEventHub.Instance?.RegisterAgent(this);
#if UNITY_EDITOR
            if (!Application.isPlaying) // mantener el inspector sincronizado en edit mode
                SyncEventConfigListWithReflectedMembers(EditorDetectedKeys());
#endif
        }

        private void OnDisable()
        {
            AudioEventHub.Instance?.UnregisterAgent(this);
        }

        // ---------- Buscador de config por key (lo usa el Hub) ----------
        public EventConfig FindConfigByKey(string eventKey)
        {
            return events.FirstOrDefault(e => e.eventKey == eventKey);
        }

        // ---------- Sync de inspector (no suscribe) ----------
        public void SyncEventConfigListWithReflectedMembers(IEnumerable<string> detectedKeys)
        {
            var detectedKeyList = detectedKeys.Distinct().ToList();

            var existingConfigs = new Dictionary<string, EventConfig>();
            foreach (var evt in events)
            {
                if (!string.IsNullOrEmpty(evt.eventKey) && !existingConfigs.ContainsKey(evt.eventKey))
                {
                    existingConfigs.Add(evt.eventKey, evt);
                }
            }
            
            var newEventList = new List<EventConfig>();
            foreach (var key in detectedKeyList)
            {
                // Generar los nombres amigables desde la nueva key (Ej: "PlayerSounds.OnJump")
                var parts = key.Split(new[] { "::" }, StringSplitOptions.None);
                var member = parts.Length > 1 ? parts[1] : key;
                string display = member;
                if (parts.Length > 1)
                {
                    var typeName = parts[0].Split('.').LastOrDefault() ?? parts[0];
                    display = $"{typeName}.{member}";
                }

                if (existingConfigs.TryGetValue(key, out var existingConfig))
                {
                    existingConfig.displayName = display;
                    existingConfig.eventName = member;
                    newEventList.Add(existingConfig);
                }
                else
                {
                    newEventList.Add(new EventConfig { eventKey = key, eventName = member, displayName = display });
                }
            }
            events.Clear();
            events.AddRange(newEventList);
        }


#if UNITY_EDITOR
        private IEnumerable<string> EditorDetectedKeys()
        {
            foreach (var (script, member) in AudioEventHub.EditorScanTargets(targetScripts))
            {
                yield return AudioEventHub.MakeKeyForEditor(script, member);
            }
        }
#endif

        [Serializable]
        public class EventConfig
        {
            [HideInInspector] public string guid = Guid.NewGuid().ToString();

            // Keys únicas
            [HideInInspector] public string eventKey;     // agentID::scriptID::member
            [HideInInspector] public string displayName;  // Script.Member (UI)
            public string eventName;                      // nombre simple (solo miembro)

            [Tooltip("Habilitar/Deshabilitar este evento.")]
            public bool enabled = true;

            [Header("Play")]
            public bool isStopEvent;
            public bool randomOne;
            public List<ClipConfig> clips = new();

            [Header("Emitter Override (opcional)")]
            public Transform emitterOverride;

            [Header("Pitch")]
            public bool usePitchRandom = true;
            [Min(0.1f)] public float pitchMin = 1f;
            [Min(0.1f)] public float pitchMax = 1f;

            [Header("Stop")]
            public StopMode stopMode = StopMode.ByClips;
            [Tooltip("Si StopMode = ByEvent, apunta a la 'eventKey' del evento a detener.")]
            public string   stopTargetEventKey;
            public bool     fadeOutOnStop = true;
            [Min(0f)] public float fadeOutTime = 0.1f;

            [Header("Voice Limiter")]
            [Tooltip("Máximo de instancias simultáneas de este evento (0 = sin límite).")]
            public int maxSimultaneous = 1;

            [Tooltip("Si otro trigger del mismo evento llega dentro de esta ventana (segundos), se ignora.")]
            [Min(0f)] public float coalesceWindow = 0.05f;

            [Tooltip("Bloquear duplicados en el mismo frame (útil con blends de animaciones).")]
            public bool blockSameFrameDuplicates = true;

            // Runtime
            [NonSerialized] public int LastRandomIndex = -1;
        }

        public enum StopMode { ByClips, ByEvent, All }

        [Serializable]
        public class ClipConfig
        {
            public AudioClip clip;
            [Range(0f, 1f)] public float volume = 1f;
            [Min(0f)] public float delay;
            public bool loop;
        }
    }
}