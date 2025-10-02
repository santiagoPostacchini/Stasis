// AudioEventListener.cs
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Audio;

namespace Audio.Scripts
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public class AudioEventListener : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("Componente que implementa ISoundPlayer y expone eventos/campos delegado sin parámetros.")]
        [SerializeField] private MonoBehaviour targetScript; // Debe implementar ISoundPlayer

        [Header("Mixer / Defaults")]
        [Tooltip("Mixer Group por defecto para este objeto.")]
        [SerializeField] private AudioMixerGroup defaultMixerGroup;

        [Tooltip("Número inicial de fuentes en el pool (para reproducir múltiples sonidos a la vez y poder detenerlos).")]
        [Min(1)] [SerializeField] private int initialSourcePool = 3;

        [Tooltip("Copiar settings 3D/2D de este AudioSource plantilla a las fuentes del pool.")]
        [SerializeField] private AudioSource sourceTemplate;

        [Header("Inspector")]
        [SerializeField] private List<EventConfig> events = new();

        [Header("Debug")]
        [SerializeField] private bool debugScan;

        private AudioSource _template;
        private readonly List<AudioSource> _pool = new();
        private readonly List<PlayingInstance> _playing = new();

        private readonly Dictionary<string, EventInfo> _eventInfoByName = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Delegate>  _handlersByEventName = new(StringComparer.Ordinal);

        private readonly Dictionary<string, FieldInfo> _fieldInfoByName   = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Delegate>  _fieldHandlersByName = new(StringComparer.Ordinal);

        private readonly List<string> _allMemberNames = new();

        private const BindingFlags ScanFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private void Reset()
        {
            sourceTemplate = GetComponent<AudioSource>();
        }

        private void Awake()
        {
            if (!sourceTemplate)
                sourceTemplate = GetComponent<AudioSource>();

            if (defaultMixerGroup)
                sourceTemplate.outputAudioMixerGroup = defaultMixerGroup;

            _template = sourceTemplate;

            for (int i = 0; i < Mathf.Max(1, initialSourcePool); i++)
                _pool.Add(CreatePooledSource());
        }

        private void OnEnable()  => TrySubscribeAll();
        private void OnDisable()
        {
            UnsubscribeAll();
            StopAllPlayingInstances();
        }

        // ---------- Suscripción / Escaneo ----------
        public void TrySubscribeAll()
        {
            UnsubscribeAll();

            if (!targetScript) return;

            if (targetScript is not ISoundPlayer)
            {
                Debug.LogWarning($"[{name}] El target asignado no implementa ISoundPlayer.");
                return;
            }

            _eventInfoByName.Clear();
            _handlersByEventName.Clear();
            _fieldInfoByName.Clear();
            _fieldHandlersByName.Clear();
            _allMemberNames.Clear();

            ScanDelegateMembers(targetScript.GetType());
            SyncEventConfigListWithReflectedMembers();

            if (!Application.isPlaying)
            {
                if (debugScan)
                    Debug.Log($"[{name}] (Scan only) Encontrados: {string.Join(", ", _allMemberNames)}");
                return;
            }

            foreach (var ei in _eventInfoByName.Values)
            {
                string evName = ei.Name;
                var del = CreateZeroParamDelegateFor(ei.EventHandlerType, () => OnReflectedEvent(evName));
                if (del != null)
                {
                    ei.AddEventHandler(targetScript, del);
                    _handlersByEventName[evName] = del;
                    if (debugScan) Debug.Log($"[{name}] Subscribed EVENT: {evName}");
                }
            }

            foreach (var fi in _fieldInfoByName.Values)
            {
                string evName = fi.Name;
                var handlerType = fi.FieldType;
                var current = (Delegate)fi.GetValue(targetScript);
                var addDel = CreateZeroParamDelegateFor(handlerType, () => OnReflectedEvent(evName));
                if (addDel != null)
                {
                    var combined = Delegate.Combine(current, addDel);
                    fi.SetValue(targetScript, combined);
                    _fieldHandlersByName[evName] = addDel;
                    if (debugScan) Debug.Log($"[{name}] Subscribed FIELD: {evName}");
                }
            }

            if (debugScan)
                Debug.Log($"[{name}] Suscripto a: {string.Join(", ", _allMemberNames)}");
        }

        private void UnsubscribeAll()
        {
            if (!targetScript) return;

            foreach (var kv in _handlersByEventName)
            {
                try
                {
                    if (_eventInfoByName.TryGetValue(kv.Key, out var ei))
                        ei.RemoveEventHandler(targetScript, kv.Value);
                }
                catch
                {
                    // ignored
                }
            }
            _handlersByEventName.Clear();
            _eventInfoByName.Clear();

            foreach (var kv in _fieldHandlersByName)
            {
                try
                {
                    if (_fieldInfoByName.TryGetValue(kv.Key, out var fi))
                    {
                        var current = (Delegate)fi.GetValue(targetScript);
                        var removed = Delegate.Remove(current, kv.Value);
                        fi.SetValue(targetScript, removed);
                    }
                }
                catch
                {
                    // ignored
                }
            }
            _fieldHandlersByName.Clear();
            _fieldInfoByName.Clear();

            _allMemberNames.Clear();
        }

        private void ScanDelegateMembers(Type type)
        {
            foreach (var ei in type.GetEvents(ScanFlags))
            {
                var handlerType = ei.EventHandlerType;
                if (IsZeroParamDelegateType(handlerType))
                {
                    _eventInfoByName[ei.Name] = ei;
                    _allMemberNames.Add(ei.Name);
                    if (debugScan) Debug.Log($"[{name}] Event OK: {ei.Name} ({handlerType.Name})");
                }
                else if (debugScan)
                {
                    Debug.Log($"[{name}] Event IGNORE (params != 0): {ei.Name} ({handlerType.Name})");
                }
            }

            foreach (var fi in type.GetFields(ScanFlags))
            {
                var fType = fi.FieldType;
                if (!typeof(Delegate).IsAssignableFrom(fType)) continue;
                if (!IsZeroParamDelegateType(fType)) continue;
                if (fi.Name.Contains("k__BackingField")) continue;

                _fieldInfoByName[fi.Name] = fi;
                _allMemberNames.Add(fi.Name);
                if (debugScan) Debug.Log($"[{name}] Field-Delegate OK: {fi.Name} ({fType.Name})");
            }
        }

        private static bool IsZeroParamDelegateType(Type delegateType)
        {
            if (delegateType == null) return false;
            if (!typeof(Delegate).IsAssignableFrom(delegateType)) return false;
            var invoke = delegateType.GetMethod("Invoke");
            if (invoke == null) return false;
            return invoke.GetParameters().Length == 0;
        }

        private static Delegate CreateZeroParamDelegateFor(Type delegateType, Action handler)
        {
            if (!IsZeroParamDelegateType(delegateType)) return null;
            return Delegate.CreateDelegate(delegateType, handler.Target, handler.Method);
        }

        private void SyncEventConfigListWithReflectedMembers()
        {
            foreach (var eventName in _allMemberNames)
            {
                if (events.All(e => e.eventName != eventName))
                    events.Add(new EventConfig { eventName = eventName });
            }
            events.RemoveAll(e => !_allMemberNames.Contains(e.eventName));
        }

        // ---------- Handler central ----------
        private void OnReflectedEvent(string eventName)
        {
            var cfg = events.FirstOrDefault(e => e.enabled && e.eventName == eventName);
            if (cfg == null) return;

            if (cfg.isStopEvent)
            {
                HandleStop(cfg);
                return;
            }

            if (cfg.clips == null || cfg.clips.Count == 0) return;

            if (cfg.randomOne && cfg.clips.Count > 1)
            {
                int idx = NextRandomIndexNoRepeat(cfg);
                var chosen = cfg.clips[idx];
                cfg.LastRandomIndex = idx; // guardar último
                StartCoroutine(PlayClipRoutine(chosen, cfg));
            }
            else
            {
                // sin random: reproducir todos (respetando delays)
                foreach (var clipCfg in cfg.clips)
                    StartCoroutine(PlayClipRoutine(clipCfg, cfg));
            }
        }

        // Elige un índice aleatorio distinto del último usado para ese evento.
        private int NextRandomIndexNoRepeat(EventConfig cfg)
        {
            int count = cfg.clips.Count;
            if (count <= 1) return 0;

            int last = cfg.LastRandomIndex;
            int idx;

            // Si nunca eligió, o solo hay 2, un intento basta:
            if (last < 0 || count == 2)
            {
                idx = UnityEngine.Random.Range(0, count);
                if (idx == last) idx = (idx + 1) % count;
                return idx;
            }

            // Para N > 2, intentamos hasta que salga distinto (máx 8 intentos)
            int attempts = 0;
            do
            {
                idx = UnityEngine.Random.Range(0, count);
                attempts++;
            } while (idx == last && attempts < 8);

            if (idx == last) idx = (idx + 1) % count; // fallback
            return idx;
        }

        // ---------- Stop logic ----------
        private void HandleStop(EventConfig cfg)
        {
            bool stoppedAny = false;

            if (cfg.stopMode == StopMode.ByClips)
            {
                if (cfg.clips is { Count: > 0 })
                {
                    foreach (var pc in _playing.ToArray())
                    {
                        if (pc.Source && cfg.clips.Any(c => c.clip == pc.Source.clip))
                        {
                            StopInstance(pc, cfg.fadeOutOnStop, cfg.fadeOutTime);
                            stoppedAny = true;
                        }
                    }
                }
            }
            else if (cfg.stopMode == StopMode.ByEvent)
            {
                if (!string.IsNullOrEmpty(cfg.stopTargetEventName))
                {
                    foreach (var pc in _playing.ToArray())
                    {
                        if (pc.Tag == cfg.stopTargetEventName)
                        {
                            StopInstance(pc, cfg.fadeOutOnStop, cfg.fadeOutTime);
                            stoppedAny = true;
                        }
                    }
                }
            }
            else if (cfg.stopMode == StopMode.All)
            {
                foreach (var pc in _playing.ToArray())
                {
                    StopInstance(pc, cfg.fadeOutOnStop, cfg.fadeOutTime);
                    stoppedAny = true;
                }
            }

            if (debugScan && !stoppedAny)
                Debug.Log($"[{name}] Stop '{cfg.eventName}' no encontró instancias a detener.");
        }

        private void StopInstance(PlayingInstance inst, bool fade, float fadeTime)
        {
            if (inst == null || !inst.Source) return;

            if (fade && inst.Source.isPlaying && fadeTime > 0f)
            {
                StartCoroutine(FadeOutAndRelease(inst, fadeTime));
            }
            else
            {
                inst.Source.Stop();
                ReleaseInstance(inst);
            }
        }

        private IEnumerator FadeOutAndRelease(PlayingInstance inst, float time)
        {
            if (!inst.Source) yield break;

            float startVol = inst.Source.volume;
            float t = 0f;
            while (t < time && inst.Source)
            {
                t += Time.unscaledDeltaTime;
                float k = 1f - Mathf.Clamp01(t / time);
                inst.Source.volume = startVol * k;
                yield return null;
            }
            if (inst.Source) inst.Source.Stop();
            ReleaseInstance(inst);
        }

        private void StopAllPlayingInstances()
        {
            foreach (var inst in _playing.ToArray())
            {
                if (inst.Source && inst.Source.isPlaying)
                    inst.Source.Stop();
                ReleaseInstance(inst);
            }
        }

        // ---------- Play logic ----------
        private IEnumerator PlayClipRoutine(ClipConfig clipCfg, EventConfig evCfg)
        {
            if (clipCfg == null || !clipCfg.clip) yield break;

            if (clipCfg.delay > 0f)
                yield return new WaitForSeconds(clipCfg.delay);

            var src = GetFreeSource();
            ApplyTemplate(src);

            // Siempre usa el mixer por defecto del objeto.
            src.outputAudioMixerGroup = defaultMixerGroup;

            src.clip = clipCfg.clip;
            src.loop = clipCfg.loop;

            // Pitch
            src.pitch = evCfg.usePitchRandom ? UnityEngine.Random.Range(evCfg.pitchMin, evCfg.pitchMax) : 1f;

            src.volume = Mathf.Clamp01(clipCfg.volume);

            src.Play();

            var inst = new PlayingInstance { Source = src, Tag = evCfg.eventName };
            _playing.Add(inst);

            if (!src.loop)
            {
                yield return new WaitWhile(() => src && src.isPlaying);
                ReleaseInstance(inst);
            }
        }

        private AudioSource GetFreeSource()
        {
            foreach (var s in _pool)
            {
                if (!s.isPlaying && _playing.All(p => p.Source != s))
                    return s;
            }
            var extra = CreatePooledSource();
            _pool.Add(extra);
            return extra;
        }

        private AudioSource CreatePooledSource()
        {
            var go = new GameObject("AudioSource_Pooled");
            go.transform.SetParent(transform, worldPositionStays: false);
            return go.AddComponent<AudioSource>();
        }

        private void ApplyTemplate(AudioSource dst)
        {
            if (!_template) return;

            dst.spatialBlend = _template.spatialBlend;
            dst.minDistance = _template.minDistance;
            dst.maxDistance = _template.maxDistance;
            dst.rolloffMode = _template.rolloffMode;
            dst.dopplerLevel = _template.dopplerLevel;
            dst.spread = _template.spread;
            dst.priority = _template.priority;
            dst.bypassEffects = _template.bypassEffects;
            dst.bypassListenerEffects = _template.bypassListenerEffects;
            dst.bypassReverbZones = _template.bypassReverbZones;
            dst.reverbZoneMix = _template.reverbZoneMix;
            dst.spatialize = _template.spatialize;
            dst.panStereo = _template.panStereo;
            dst.outputAudioMixerGroup = _template.outputAudioMixerGroup;
        }

        private void ReleaseInstance(PlayingInstance inst)
        {
            if (inst == null) return;
            _playing.Remove(inst);
            if (!inst.Source) return;

            inst.Source.clip = null;
            inst.Source.loop = false;
            inst.Source.volume = 1f;
            inst.Source.pitch = 1f;
        }

        public MonoBehaviour TargetScript
        {
            get => targetScript;
            set
            {
                if (targetScript == value) return;
                targetScript = value;
                TrySubscribeAll();
            }
        }

        public IReadOnlyList<EventConfig> EventConfigs => events;

        // ----------------- Data structs -----------------
        [Serializable]
        public class EventConfig
        {
            [HideInInspector] public string guid = Guid.NewGuid().ToString();

            [Tooltip("Nombre del evento/campo delegado detectado por reflexión en el Target.")]
            public string eventName;

            [Tooltip("Habilitar/Deshabilitar este evento.")]
            public bool enabled = true;

            [Header("Play")]
            [Tooltip("Si está activo, el evento reproduce un sonido (o varios). Si es StopEvent se ignora la reproducción.")]
            public bool isStopEvent;

            [Tooltip("Si 'Random One' está activo, elige 1 clip al azar de la lista (sin repetir el último).")]
            public bool randomOne;

            [Tooltip("Lista de clips que se reproducen para este evento.")]
            public List<ClipConfig> clips = new();

            [Header("Pitch")]
            [Tooltip("Si está activo, aplica pitch aleatorio entre Min y Max.")]
            public bool usePitchRandom = true;

            [Min(0.1f)] public float pitchMin = 1f;
            [Min(0.1f)] public float pitchMax = 1f;

            [Header("Stop")]
            [Tooltip("Modo de parada cuando este evento se use como 'Stop Event'.")]
            public StopMode stopMode = StopMode.ByClips;

            [Tooltip("Si StopMode = ByEvent, indica el nombre del evento a apagar (detiene todo lo que haya lanzado ese evento).")]
            public string stopTargetEventName;

            [Tooltip("Aplicar fade out al detener.")]
            public bool fadeOutOnStop = true;

            [Tooltip("Tiempo de fade out (segundos).")]
            [Min(0f)] public float fadeOutTime = 0.1f;

            // Runtime: recordar último índice elegido para evitar repetición
            [NonSerialized] public int LastRandomIndex = -1;
        }

        public enum StopMode { ByClips, ByEvent, All }

        [Serializable]
        public class ClipConfig
        {
            public AudioClip clip;
            [Range(0f, 1f)] public float volume = 1f;
            [Tooltip("Delay (segundos) antes de reproducir este clip.")]
            [Min(0f)] public float delay;
            [Tooltip("Reproducir en loop hasta que un Stop lo detenga.")]
            public bool loop;
        }

        private class PlayingInstance
        {
            public AudioSource Source;
            public string Tag;
        }
    }
}