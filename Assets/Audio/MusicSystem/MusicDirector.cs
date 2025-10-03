using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Audio;

namespace Audio.MusicSystem
{
    /// <summary>
    /// Orquesta cues por capas con PlayScheduled, cuantización, crossfades, stingers e intensidad.
    /// Esqueleto extensible. Integra con cualquier AudioMixer.
    /// </summary>
    [DisallowMultipleComponent]
    public class MusicDirector : MonoBehaviour
    {
        private static MusicDirector Instance { get; set; }

        [Header("Graph")]
        public MusicGraph graph;

        [Header("Runtime Routing")]
        public Transform audioRoot;                 // contenedor para sources
        public int voicesPerLayer = 1;             // usualmente 1 (por stem)
        public float globalVolume = 1f;

        [Header("Debug")]
        public bool logTransitions;

        // --- Runtime state ---
        private string _currentNodeId;
        private MusicCue _currentCue;
        private double _cueStartDspTime;           // dónde arrancó el cue en la grilla
        private readonly Dictionary<string, AudioSource> _activeLayerSources = new(); // layerId -> src
        private readonly Dictionary<string, float> _parameters = new();               // nombre->valor
        private readonly HashSet<string> _triggers = new();                           // one-shot triggers

        // Para crossfade
        private readonly List<AudioSource> _fadingOut = new();

        // ---- Unity ----
        private void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            if (!audioRoot) audioRoot = transform;
        }

        private void Start()
        {
            if (!graph) { Debug.LogWarning("[MusicDirector] No graph asignado."); return; }
            var start = string.IsNullOrEmpty(graph.startNodeId) ? graph.nodes.FirstOrDefault()?.id : graph.startNodeId;
            if (string.IsNullOrEmpty(start))
            {
                Debug.LogWarning("[MusicDirector] Graph sin nodo de inicio.");
                return;
            }
            GoToNode(start, MusicGraph.Quantization.Immediate, 0f, playStinger: false, stingerId: null);
        }

        private void Update()
        {
            if (_triggers.Count > 0)
            {
                foreach (var trg in _triggers.ToArray())
                    TryTransitionByTrigger(trg);
                _triggers.Clear();
            }

            // 2) procesar transiciones por parámetros
            TryTransitionByParameters();
        }

        // ---------- API pública ----------
        public void SetParameter(string paramName, float value) => _parameters[paramName] = value;
        public float GetParameter(string paramName, float fallback = 0f) => _parameters.GetValueOrDefault(paramName, fallback);
        public void Trigger(string triggerName) => _triggers.Add(triggerName);

        /// <summary>Activa/desactiva capas por id dentro del cue actual con fade local.</summary>
        public void SetLayerEnabled(string layerId, bool enabled, float fadeSeconds = 0.5f)
        {
            if (!_currentCue) return;

            var layer = _currentCue.layers.FirstOrDefault(l => l.id == layerId);
            if (layer == null) return;

            if (enabled) EnsureLayerPlaying(layer, fadeSeconds);
            else FadeOutAndStopLayer(layerId, fadeSeconds);
        }

        /// <summary>Ajusta volumen por capa (no destruye el mix por snapshots).</summary>
        public void SetLayerVolume(string layerId, float volume, float fadeSeconds = 0.2f)
        {
            if (_activeLayerSources.TryGetValue(layerId, out var src) && src)
                StartCoroutine(FadeVolume(src, Mathf.Clamp01(volume) * globalVolume, fadeSeconds));
        }

        /// <summary>Cambia a otro nodo/cue con cuantización y crossfade.</summary>
        public void GoToNode(string nodeId, MusicGraph.Quantization q = MusicGraph.Quantization.Bar,
                             float crossfadeSeconds = 1f, bool playStinger = false, string stingerId = null)
        {
            var node = graph.nodes.FirstOrDefault(n => n.id == nodeId);
            if (node == null || !node.cue)
            {
                Debug.LogWarning($"[MusicDirector] Node '{nodeId}' inválido.");
                return;
            }

            var nextCue = node.cue;
            var when = GetNextQuantizedDspTime(_currentCue ? _currentCue : nextCue, q);

            if (logTransitions) Debug.Log($"[MusicDirector] -> {nodeId} at {q} (t={when:0.000})");

            // programar reproducción del nuevo cue
            StartCoroutine(PlayCueScheduled(nextCue, when));

            // hacer crossfade de las capas actuales
            if (_currentCue)
                CrossfadeOutCurrent(crossfadeSeconds, when);

            // stinger optional
            if (playStinger && !string.IsNullOrEmpty(stingerId))
                FireStinger(nextCue, stingerId, when);

            _currentNodeId = nodeId;
            _currentCue = nextCue;
            _cueStartDspTime = when;
        }

        // ---------- Internals: transitions ----------
        private void TryTransitionByTrigger(string triggerName)
        {
            foreach (var t in graph.transitions)
            {
                if (t.fromNodeId != _currentNodeId) continue;
                if (!string.IsNullOrEmpty(t.triggerName) && t.triggerName == triggerName)
                {
                    GoToNode(t.toNodeId, t.quantization, t.crossfadeSeconds, t.fireStingerOnEnter, t.stingerId);
                    return;
                }
            }
        }

        private void TryTransitionByParameters()
        {
            foreach (var t in graph.transitions)
            {
                if (t.fromNodeId != _currentNodeId) continue;
                if (!string.IsNullOrEmpty(t.triggerName)) continue; // estas se manejan en Trigger()

                if (!string.IsNullOrEmpty(t.paramName) && _parameters.TryGetValue(t.paramName, out var val))
                {
                    if (t.Matches(val))
                    {
                        GoToNode(t.toNodeId, t.quantization, t.crossfadeSeconds, t.fireStingerOnEnter, t.stingerId);
                        return;
                    }
                }
            }
        }

        // ---------- Internals: scheduling ----------
        private IEnumerator PlayCueScheduled(MusicCue cue, double whenDsp)
        {
            // crear/reusar sources por capa
            foreach (var layer in cue.layers)
            {
                if (!layer.clip) continue;

                // si la capa está marcada como no opcional o enabled por default => arranca
                if (!layer.isOptional || layer.enabledByDefault)
                    EnsureLayerScheduled(cue, layer, whenDsp);
            }

            yield break;
        }

        private void EnsureLayerScheduled(MusicCue cue, MusicCue.Layer layer, double whenDsp)
        {
            var src = GetOrCreateSource(layer.id, cue.outputMixer);
            src.clip = layer.clip;
            src.volume = Mathf.Clamp01(layer.defaultVolume) * globalVolume;
            src.loop = layer.loop;
            AlignSourceSettingsForCue(src, cue);
            src.PlayScheduled(whenDsp);

            // Para fiabilidad en loops perfectos, ambos: usar clips loopables y arrancar juntos.
            _activeLayerSources[layer.id] = src;
        }

        private void EnsureLayerPlaying(MusicCue.Layer layer, float fadeSeconds)
        {
            if (!_currentCue) return;
            if (!layer.clip) return;

            var when = GetNextQuantizedDspTime(_currentCue, MusicGraph.Quantization.Bar);
            var src = GetOrCreateSource(layer.id, _currentCue.outputMixer);
            AlignSourceSettingsForCue(src, _currentCue);
            src.clip = layer.clip;
            src.loop = layer.loop;
            src.volume = 0f; // fade-in
            src.PlayScheduled(when);
            StartCoroutine(FadeVolume(src, layer.defaultVolume * globalVolume, fadeSeconds, when));
            _activeLayerSources[layer.id] = src;
        }

        private void CrossfadeOutCurrent(float fadeSeconds, double anchorWhen)
        {
            foreach (var kv in _activeLayerSources.ToArray())
            {
                var src = kv.Value;
                if (!src) { _activeLayerSources.Remove(kv.Key); continue; }

                _fadingOut.Add(src);
                StartCoroutine(FadeOutAndStop(src, fadeSeconds, anchorWhen));
                _activeLayerSources.Remove(kv.Key);
            }
        }

        private void FadeOutAndStopLayer(string layerId, float fadeSeconds)
        {
            if (_activeLayerSources.TryGetValue(layerId, out var src) && src)
            {
                _fadingOut.Add(src);
                StartCoroutine(FadeOutAndStop(src, fadeSeconds, /*anchor*/AudioSettings.dspTime));
                _activeLayerSources.Remove(layerId);
            }
        }

        // ---------- Internals: sources ----------
        private AudioSource GetOrCreateSource(string layerId, AudioMixerGroup mixer)
        {
            if (_activeLayerSources.TryGetValue(layerId, out var existing) && existing)
                return existing;

            var go = new GameObject($"MusicLayer_{layerId}");
            go.transform.SetParent(audioRoot, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.outputAudioMixerGroup = mixer;
            src.spatialBlend = 0f; // música = 2D
            return src;
        }

        private void AlignSourceSettingsForCue(AudioSource src, MusicCue cue)
        {
            src.pitch = 1f;
            src.panStereo = 0f;
            // Si quisieras sample-accurate loop con offsets, podrías configurar timeSamples aquí.
            // TODO: integrar “loop region” si haces bucles parciales dentro del clip.
        }

        private double GetNextQuantizedDspTime(MusicCue cue, MusicGraph.Quantization q)
        {
            var dspNow = AudioSettings.dspTime;
            if (q == MusicGraph.Quantization.Immediate || !cue) return dspNow + 0.02; // pequeño margen

            double grid = q switch
            {
                MusicGraph.Quantization.Beat     => 60.0 / cue.bpm,
                MusicGraph.Quantization.Bar      => (60.0 / cue.bpm) * cue.beatsPerBar,
                MusicGraph.Quantization.TwoBars  => (60.0 / cue.bpm) * cue.beatsPerBar * 2,
                MusicGraph.Quantization.FourBars => (60.0 / cue.bpm) * cue.beatsPerBar * 4,
                MusicGraph.Quantization.Loop     => cue.LoopDuration,
                _ => (60.0 / cue.bpm) * cue.beatsPerBar
            };

            // siguiente múltiplo de 'grid' relativo al inicio del cue actual (o ahora si no hay)
            var anchor = _currentCue ? _cueStartDspTime : dspNow;
            var t = dspNow - anchor;
            var n = Math.Ceiling(t / grid);
            return anchor + Math.Max(1, n) * grid; // al menos el próximo grid
        }

        // ---------- Internals: stingers ----------
        private void FireStinger(MusicCue targetCue, string stingerId, double whenDsp)
        {
            var st = targetCue.stingers.FirstOrDefault(s => s.id == stingerId);
            if (!st?.clip) return;

            // Stinger en source temporal 2D
            var go = new GameObject($"Stinger_{stingerId}");
            go.transform.SetParent(audioRoot, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.outputAudioMixerGroup = targetCue.outputMixer;
            src.clip = st.clip;
            src.volume = st.volume * globalVolume;
            src.loop = false;
            src.PlayScheduled(whenDsp);
            Destroy(go, (float)(whenDsp - AudioSettings.dspTime) + st.clip.length + 0.5f);
        }

        // ---------- Coroutines: fades ----------
        private IEnumerator FadeOutAndStop(AudioSource src, float seconds, double anchorWhen)
        {
            if (!src) yield break;
            float start = src.volume;
            float t = 0f;
            while (t < seconds && src)
            {
                t += Time.unscaledDeltaTime;
                src.volume = Mathf.Lerp(start, 0f, t / seconds);
                yield return null;
            }
            if (src)
            {
                src.Stop();
                Destroy(src.gameObject);
            }
            _fadingOut.Remove(src);
        }

        private IEnumerator FadeVolume(AudioSource src, float target, float seconds, double playAnchor = -1)
        {
            if (!src) yield break;

            // si está programado en el futuro, esperamos a que empiece
            if (playAnchor > 0)
            {
                var wait = (float)Math.Max(0, playAnchor - AudioSettings.dspTime);
                if (wait > 0) yield return new WaitForSecondsRealtime(wait);
            }

            float start = src.volume;
            float t = 0f;
            while (t < seconds && src)
            {
                t += Time.unscaledDeltaTime;
                src.volume = Mathf.Lerp(start, target, t / seconds);
                yield return null;
            }
            if (src) src.volume = target;
        }
    }
}