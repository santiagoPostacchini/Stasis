using UnityEngine;

namespace Player.Scripts.MovementFSM
{
    [DefaultExecutionOrder(11000)]
    public class VisualYawFollower : MonoBehaviour
    {
        [Header("Enable/Disable")]
        public bool followEnabled = true;
        public bool snapWhenEnabled;

        [Header("Refs")]
        [Tooltip("Tu CamHolder / yawPivot (donde se aplica el yaw de la cámara)")]
        public Transform yawPivot;
        [Tooltip("Raíz visual (ej. 'Graphics' con el SkinnedMesh). Si está vacío, usa este transform.")]
        public Transform visualRoot;

        [Header("Tuning")]
        [Range(0f, 1f)] public float turnLerp = 0.2f;

        private bool _wasFollowing;

        void Reset() { visualRoot = transform; }

        void LateUpdate()
        {
            if (!yawPivot) return;

            if (!followEnabled)
            {
                _wasFollowing = false;
                return;
            }

            if (!visualRoot) visualRoot = transform;
            
            Vector3 f = yawPivot.forward; f.y = 0f;
            if (f.sqrMagnitude < 1e-6f) return;

            Quaternion target = Quaternion.LookRotation(f.normalized, Vector3.up);
            
            if (!_wasFollowing && snapWhenEnabled)
            {
                visualRoot.rotation = target;
                _wasFollowing = true;
                return;
            }
            
            float t = 1f - Mathf.Pow(1f - turnLerp, Time.deltaTime * 60f);
            visualRoot.rotation = Quaternion.Slerp(visualRoot.rotation, target, t);

            _wasFollowing = true;
        }
    }
}