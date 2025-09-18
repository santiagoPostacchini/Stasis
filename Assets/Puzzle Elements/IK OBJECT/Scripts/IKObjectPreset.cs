// Assets/Scripts/IKSuite/IKObjectPreset.cs
using UnityEngine;

namespace IKSuite
{
    public enum IKSystemType
    {
        IK_Distance,
        IK_DistanceInverse,
        IK_PlatformMovement,
        IK_PlatformRotation,
        IK_PlatformRotation_Stairs
    }

    [CreateAssetMenu(menuName = "IK/IK Object Preset", fileName = "IKObjectPreset")]
    public class IKObjectPreset : ScriptableObject
    {
        [Header("Sistema activo")]
        public IKSystemType systemType = IKSystemType.IK_Distance;

        [Header("Cantidad de Bones")]
        [Min(1)] public int boneCount = 3;

        // ----------------  DISTANCE / INVERSE  ----------------
        [Header("Distance / Inverse - FollowTargetController")]
        public AnimationCurve distance_remapLerp = AnimationCurve.Linear(0, 0, 1, 1);
        [Tooltip("Opcional: si tu FollowTargetController expone 'moveDuration'")]
        public float distance_moveDuration = 0.25f;
        [Tooltip("Si tu FTC usa outMin/outMax para invertir")]
        public float distance_outMin = 1f;
        public float distance_outMax = 0f;

        [Header("Distance Inverse overrides (dejar en true para invertir outMin/outMax)")]
        public bool inverse_overrideOut = true;
        public float inverse_outMin = 0f;
        public float inverse_outMax = 1f;

        // ----------------  PLATFORM MOVEMENT  ----------------
        [Header("Platform Movement - PathFollower1 (en PlatformM)")]
        public float movement_speed = 1.5f;
        public float movement_distanceThreshold = 0.1f;

        // ----------------  PLATFORM ROTATION  ----------------
        [Header("Platform Rotation - FollowMultipleTargetController (en Platform)")]
        public AnimationCurve rotation_remapLerp = AnimationCurve.Linear(0, 0, 1, 1);
        public float rotation_arcHeight = 1.0f;
        public float rotation_moveDelay = 0.1f;
        public float rotation_travelTime = 0.7f;
        public float rotation_stopDuration = 0.15f;

        // ----------------  STAIRS  ----------------
        [Header("Stairs usa mismos valores que Rotation")]
        public bool stairs_useRotationValues = true;
        public AnimationCurve stairs_remapLerp = AnimationCurve.Linear(0, 0, 1, 1);
        public float stairs_arcHeight = 1.0f;
        public float stairs_moveDelay = 0.1f;
        public float stairs_travelTime = 0.7f;
        public float stairs_stopDuration = 0.15f;
    }
}
