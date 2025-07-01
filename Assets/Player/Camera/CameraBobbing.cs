using Managers.Events;
using Player.Scripts.MVC;
using UnityEngine;

namespace Player.Camera
{
    public class CameraBobbing : MonoBehaviour
    {
        private bool _isEnabled = true;

        [SerializeField, Range(0, 0.1f)] private float amplitude;
        [SerializeField, Range(0, 30)] private float frecuency;

        [SerializeField] private new Transform camera;

        private readonly float _toggleSpeed = 1f;

        [SerializeField] private Rigidbody rb; // Ahora usa Rigidbody
        [SerializeField] private Model model; // Referencia al jugador
        private Vector3 _startPos;

        private bool _footstepTriggeredThisCycle;
        private float _previousBobbingValue;

        private void Awake()
        {
            _startPos = camera.localPosition;
        }

        private void Update()
        {
            if (!_isEnabled) return;

            CheckMotion();
            ResetPosition();
        }

        private Vector3 FootstepMotion()
        {
            Vector3 pos = Vector3.zero;

            float bobbingValue = Mathf.Sin(Time.time * frecuency);

            pos.y = bobbingValue * amplitude;
            pos.x = Mathf.Sin(Time.time * frecuency / 2) * amplitude / 2;

            CheckFootstepEvent(bobbingValue);

            _previousBobbingValue = bobbingValue;

            return pos;
        }

        private void CheckMotion()
        {
            // Usa la velocidad del Rigidbody en el plano XZ
            float speed = new Vector3(rb.velocity.x, 0, rb.velocity.z).magnitude;

            if (speed < _toggleSpeed)
            {
                _footstepTriggeredThisCycle = false;
                return;
            }

            PlayMotion(FootstepMotion());
        }

        private void ResetPosition()
        {
            if (camera.localPosition == _startPos) return;
            camera.localPosition = Vector3.Lerp(camera.localPosition, _startPos, 1 * Time.deltaTime);
        }

        private void PlayMotion(Vector3 motion)
        {
            camera.localPosition += motion;
        }

        private void CheckFootstepEvent(float currentBobbingValue)
        {
            if (_previousBobbingValue < 0 && currentBobbingValue >= 0)
            {
                if (!_footstepTriggeredThisCycle && model && model.state != Model.MovementState.Air)
                {
                    EventManager.TriggerEvent("OnFootstep", model.gameObject);
                    _footstepTriggeredThisCycle = true;
                }
            }

            if (currentBobbingValue < 0)
                _footstepTriggeredThisCycle = false;
        }
    }
}