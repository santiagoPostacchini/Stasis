// AudioEventListenerEditor.cs
#if UNITY_EDITOR
using System;
using System.Linq;
using Audio.Scripts;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    [CustomEditor(typeof(AudioEventListener))]
    public class AudioEventListenerEditor : UnityEditor.Editor
    {
        private SerializedProperty _targetScript;
        private SerializedProperty _defaultMixerGroup;
        private SerializedProperty _initialPool;
        private SerializedProperty _sourceTemplate;
        private SerializedProperty _events;

        private void OnEnable()
        {
            _targetScript       = serializedObject.FindProperty("targetScript");
            _defaultMixerGroup  = serializedObject.FindProperty("defaultMixerGroup");
            _initialPool        = serializedObject.FindProperty("initialSourcePool");
            _sourceTemplate     = serializedObject.FindProperty("sourceTemplate");
            _events             = serializedObject.FindProperty("events");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_targetScript);
            EditorGUILayout.PropertyField(_defaultMixerGroup);
            EditorGUILayout.PropertyField(_sourceTemplate);
            EditorGUILayout.PropertyField(_initialPool);

            var listener = (AudioEventListener)target;

            EditorGUILayout.Space(8);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Eventos detectados", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Refrescar", GUILayout.Width(100)))
                {
                    Undo.RecordObject(listener, "Refresh Audio Events");
                    listener.TrySubscribeAll();
                    EditorUtility.SetDirty(listener);
                    GUI.FocusControl(null);
                }
            }

            if (_events is { isArray: true })
            {
                for (int i = 0; i < _events.arraySize; i++)
                {
                    var ev = _events.GetArrayElementAtIndex(i);
                    DrawEventConfig(ev, listener);
                    EditorGUILayout.Space(6);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawEventConfig(SerializedProperty ev, AudioEventListener listener)
        {
            var eventName           = ev.FindPropertyRelative("eventName");
            var enabled             = ev.FindPropertyRelative("enabled");
            var isStopEvent         = ev.FindPropertyRelative("isStopEvent");
            var randomOne           = ev.FindPropertyRelative("randomOne");
            var clips               = ev.FindPropertyRelative("clips");

            var usePitchRandom      = ev.FindPropertyRelative("usePitchRandom");
            var pitchMin            = ev.FindPropertyRelative("pitchMin");
            var pitchMax            = ev.FindPropertyRelative("pitchMax");

            var stopMode            = ev.FindPropertyRelative("stopMode");
            var stopTargetEventName = ev.FindPropertyRelative("stopTargetEventName");
            var fadeOutOnStop       = ev.FindPropertyRelative("fadeOutOnStop");
            var fadeOutTime         = ev.FindPropertyRelative("fadeOutTime");

            var box = new GUIStyle("HelpBox");
            EditorGUILayout.BeginVertical(box);

            using (new EditorGUILayout.HorizontalScope())
            {
                enabled.boolValue = EditorGUILayout.ToggleLeft($" {eventName.stringValue}", enabled.boolValue, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                isStopEvent.boolValue = GUILayout.Toggle(isStopEvent.boolValue, "Stop Event", "Button", GUILayout.Width(100));
            }

            EditorGUI.indentLevel++;

            // ---------- PITCH ----------
            EditorGUILayout.LabelField("Pitch", EditorStyles.boldLabel);
            usePitchRandom.boolValue = EditorGUILayout.ToggleLeft("Usar Pitch Aleatorio", usePitchRandom.boolValue);

            using (new EditorGUI.DisabledScope(!usePitchRandom.boolValue))
            {
                DrawMinMaxRow(pitchMin, pitchMax, labelA: "Min", labelB: "Max", fieldWidth: 80);
                if (pitchMin.floatValue > pitchMax.floatValue)
                    pitchMax.floatValue = pitchMin.floatValue;
            }

            if (!isStopEvent.boolValue)
            {
                // Random One (no repeat)
                randomOne.boolValue = EditorGUILayout.ToggleLeft("Usar Random Sound (sin repetir el último)", randomOne.boolValue);

                EditorGUILayout.LabelField("Clips", EditorStyles.boldLabel);
                DrawClipList(clips);
            }
            else
            {
                EditorGUILayout.PropertyField(stopMode);

                if ((AudioEventListener.StopMode)stopMode.enumValueIndex == AudioEventListener.StopMode.ByEvent)
                {
                    var options = listener.EventConfigs.Select(e => e.eventName).ToArray();
                    int idx = Array.IndexOf(options, stopTargetEventName.stringValue);
                    int newIdx = EditorGUILayout.Popup("Stop Target Event", Mathf.Max(0, idx), options);
                    if (newIdx >= 0 && newIdx < options.Length)
                        stopTargetEventName.stringValue = options[newIdx];
                }
                else if ((AudioEventListener.StopMode)stopMode.enumValueIndex == AudioEventListener.StopMode.ByClips)
                {
                    EditorGUILayout.HelpBox("Agregá aquí los clips a detener. Si la lista está vacía no se detendrá nada.", MessageType.Info);
                    EditorGUILayout.LabelField("Clips a detener", EditorStyles.boldLabel);
                    DrawClipList(clips, showLoop:false);
                }

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(fadeOutOnStop, new GUIContent("Fade Out"));
                if (fadeOutOnStop.boolValue)
                    EditorGUILayout.PropertyField(fadeOutTime, new GUIContent("Tiempo"));
                EditorGUILayout.EndHorizontal();
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }

        private void DrawMinMaxRow(SerializedProperty a, SerializedProperty b, string labelA, string labelB, float fieldWidth)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(4);
                GUILayout.Label(labelA, GUILayout.Width(28));
                a.floatValue = EditorGUILayout.FloatField(a.floatValue, GUILayout.Width(fieldWidth));
                GUILayout.Space(12);
                GUILayout.Label(labelB, GUILayout.Width(30));
                b.floatValue = EditorGUILayout.FloatField(b.floatValue, GUILayout.Width(fieldWidth));
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawClipList(SerializedProperty listProp, bool showLoop = true)
        {
            int removeAt = -1;

            for (int i = 0; i < listProp.arraySize; i++)
            {
                var el     = listProp.GetArrayElementAtIndex(i);
                var clip   = el.FindPropertyRelative("clip");
                var volume = el.FindPropertyRelative("volume");
                var delay  = el.FindPropertyRelative("delay");
                var loop   = el.FindPropertyRelative("loop");

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(clip);
                if (GUILayout.Button("X", GUILayout.Width(22))) removeAt = i;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.Slider(volume, 0f, 1f);
                EditorGUILayout.PropertyField(delay);
                EditorGUILayout.EndHorizontal();

                if (showLoop) EditorGUILayout.PropertyField(loop);

                EditorGUILayout.EndVertical();
            }

            if (removeAt >= 0)
                listProp.DeleteArrayElementAtIndex(removeAt);

            if (GUILayout.Button("+ Agregar clip"))
                listProp.InsertArrayElementAtIndex(Mathf.Max(0, listProp.arraySize));
        }
    }
}
#endif