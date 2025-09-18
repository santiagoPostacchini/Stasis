// Assets/Scripts/IKSuite/Editor/IKObjectPresetEditor.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace IKSuite
{
    [CustomEditor(typeof(IKObjectPreset))]
    public class IKObjectPresetEditor : Editor
    {
        SerializedProperty systemType;
        SerializedProperty boneCount;

        // Distance / Inverse
        SerializedProperty distance_remapLerp;
        SerializedProperty distance_moveDuration;
        SerializedProperty distance_outMin;
        SerializedProperty distance_outMax;

        SerializedProperty inverse_overrideOut;
        SerializedProperty inverse_outMin;
        SerializedProperty inverse_outMax;

        // Movement
        SerializedProperty movement_speed;
        SerializedProperty movement_distanceThreshold;

        // Rotation
        SerializedProperty rotation_remapLerp;
        SerializedProperty rotation_arcHeight;
        SerializedProperty rotation_moveDelay;
        SerializedProperty rotation_travelTime;
        SerializedProperty rotation_stopDuration;

        // Stairs
        SerializedProperty stairs_useRotationValues;
        SerializedProperty stairs_remapLerp;
        SerializedProperty stairs_arcHeight;
        SerializedProperty stairs_moveDelay;
        SerializedProperty stairs_travelTime;
        SerializedProperty stairs_stopDuration;

        void OnEnable()
        {
            systemType = serializedObject.FindProperty("systemType");
            boneCount = serializedObject.FindProperty("boneCount");

            distance_remapLerp = serializedObject.FindProperty("distance_remapLerp");
            distance_moveDuration = serializedObject.FindProperty("distance_moveDuration");
            distance_outMin = serializedObject.FindProperty("distance_outMin");
            distance_outMax = serializedObject.FindProperty("distance_outMax");

            inverse_overrideOut = serializedObject.FindProperty("inverse_overrideOut");
            inverse_outMin = serializedObject.FindProperty("inverse_outMin");
            inverse_outMax = serializedObject.FindProperty("inverse_outMax");

            movement_speed = serializedObject.FindProperty("movement_speed");
            movement_distanceThreshold = serializedObject.FindProperty("movement_distanceThreshold");

            rotation_remapLerp = serializedObject.FindProperty("rotation_remapLerp");
            rotation_arcHeight = serializedObject.FindProperty("rotation_arcHeight");
            rotation_moveDelay = serializedObject.FindProperty("rotation_moveDelay");
            rotation_travelTime = serializedObject.FindProperty("rotation_travelTime");
            rotation_stopDuration = serializedObject.FindProperty("rotation_stopDuration");

            stairs_useRotationValues = serializedObject.FindProperty("stairs_useRotationValues");
            stairs_remapLerp = serializedObject.FindProperty("stairs_remapLerp");
            stairs_arcHeight = serializedObject.FindProperty("stairs_arcHeight");
            stairs_moveDelay = serializedObject.FindProperty("stairs_moveDelay");
            stairs_travelTime = serializedObject.FindProperty("stairs_travelTime");
            stairs_stopDuration = serializedObject.FindProperty("stairs_stopDuration");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(systemType);
            EditorGUILayout.PropertyField(boneCount);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            var mode = (IKSystemType)systemType.enumValueIndex;

            switch (mode)
            {
                case IKSystemType.IK_Distance:
                    DrawDistanceBlock(isInverse: false);
                    break;

                case IKSystemType.IK_DistanceInverse:
                    DrawDistanceBlock(isInverse: true);
                    break;

                case IKSystemType.IK_PlatformMovement:
                    DrawMovementBlock();
                    break;

                case IKSystemType.IK_PlatformRotation:
                    DrawRotationBlock(title: "Platform Rotation");
                    break;

                case IKSystemType.IK_PlatformRotation_Stairs:
                    DrawStairsBlock();
                    break;
            }

            serializedObject.ApplyModifiedProperties();
        }

        void DrawDistanceBlock(bool isInverse)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(isInverse ? "Distance Inverse – FollowTargetController" : "Distance – FollowTargetController", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(distance_remapLerp, new GUIContent("Remap Lerp"));
                EditorGUILayout.PropertyField(distance_moveDuration, new GUIContent("Move Duration"));

                if (!isInverse)
                {
                    EditorGUILayout.PropertyField(distance_outMin, new GUIContent("Out Min"));
                    EditorGUILayout.PropertyField(distance_outMax, new GUIContent("Out Max"));
                }
                else
                {
                    EditorGUILayout.PropertyField(inverse_overrideOut, new GUIContent("Override OutMin/OutMax"));
                    if (inverse_overrideOut.boolValue)
                    {
                        EditorGUILayout.PropertyField(inverse_outMin, new GUIContent("Inverse Out Min"));
                        EditorGUILayout.PropertyField(inverse_outMax, new GUIContent("Inverse Out Max"));
                    }
                    else
                    {
                        // Mostrar como referencia (readonly) los del modo Distance
                        using (new EditorGUI.DisabledScope(true))
                        {
                            EditorGUILayout.PropertyField(distance_outMin, new GUIContent("Out Min (Distance)"));
                            EditorGUILayout.PropertyField(distance_outMax, new GUIContent("Out Max (Distance)"));
                        }
                    }
                }
            }
        }

        void DrawMovementBlock()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Platform Movement – PathFollower1 (PlatformM)", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(movement_speed, new GUIContent("Speed"));
                EditorGUILayout.PropertyField(movement_distanceThreshold, new GUIContent("Distance Threshold"));
            }
        }

        void DrawRotationBlock(string title)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(title + " – FollowMultipleTargetController (Platform)", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(rotation_remapLerp, new GUIContent("Remap Lerp"));
                EditorGUILayout.PropertyField(rotation_arcHeight, new GUIContent("Arc Height"));
                EditorGUILayout.PropertyField(rotation_moveDelay, new GUIContent("Move Delay"));
                EditorGUILayout.PropertyField(rotation_travelTime, new GUIContent("Travel Time"));
                EditorGUILayout.PropertyField(rotation_stopDuration, new GUIContent("Stop Duration"));
            }
        }

        void DrawStairsBlock()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Platform Rotation – Stairs – FollowMultipleTargetController (Platform)", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(stairs_useRotationValues, new GUIContent("Usar valores de Rotation"));

                if (!stairs_useRotationValues.boolValue)
                {
                    EditorGUILayout.PropertyField(stairs_remapLerp, new GUIContent("Remap Lerp"));
                    EditorGUILayout.PropertyField(stairs_arcHeight, new GUIContent("Arc Height"));
                    EditorGUILayout.PropertyField(stairs_moveDelay, new GUIContent("Move Delay"));
                    EditorGUILayout.PropertyField(stairs_travelTime, new GUIContent("Travel Time"));
                    EditorGUILayout.PropertyField(stairs_stopDuration, new GUIContent("Stop Duration"));
                }
                else
                {
                    // Mostrar como referencia (readonly) los de Rotation
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.PropertyField(rotation_remapLerp, new GUIContent("Remap Lerp (Rotation)"));
                        EditorGUILayout.PropertyField(rotation_arcHeight, new GUIContent("Arc Height (Rotation)"));
                        EditorGUILayout.PropertyField(rotation_moveDelay, new GUIContent("Move Delay (Rotation)"));
                        EditorGUILayout.PropertyField(rotation_travelTime, new GUIContent("Travel Time (Rotation)"));
                        EditorGUILayout.PropertyField(rotation_stopDuration, new GUIContent("Stop Duration (Rotation)"));
                    }
                }
            }
        }
    }
}
#endif
