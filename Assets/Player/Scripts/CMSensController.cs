using Unity.Cinemachine;
using UnityEngine;

namespace Player.Scripts
{
    public class CmSensController : MonoBehaviour
    {
        [Header("Refs")]
        public CinemachineInputAxisController axisController;

        [Header("Sensitivity")]
        [Min(1f)] public float sensitivity = 3f;

        private void Awake()
        {
            Cursor.lockState = CursorLockMode.Locked;
        }

        private void Start()
        {
            Apply();
        }

        void Reset()
        {
            // Auto-resolver si el componente está en el mismo GO
            if (!axisController) axisController = GetComponent<CinemachineInputAxisController>();
        }

        /// <summary>
        /// Llamalo desde un slider UI (OnValueChanged) o desde opciones.
        /// </summary>
        public void SetSensitivity(float newValue)
        {
            sensitivity = Mathf.Max(0f, newValue);
            Apply();
        }

        /// <summary>
        /// Aplica 'sensitivity' a los ejes controlados por el CinemachineInputAxisController.
        /// </summary>
        [ContextMenu("Apply Now")]
        public void Apply()
        {
            if (!axisController) return;

            // Controllers es la lista de ejes que el componente detectó dinámicamente.
            // Cada uno tiene Gain (Input System) y LegacyGain (Legacy Input).
            var controllers = axisController.Controllers;
            
            for (int i = 0; i < controllers.Count; i++)
            {
                var c = controllers[i];

                c.Input.LegacyGain = c.Input.LegacyGain > 0
                    ? 20 * sensitivity
                    : -20 * sensitivity;
                
                controllers[i] = c;
            }
        }
    }
}