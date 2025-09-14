using UnityEngine;

namespace Player.Scripts
{
    [DefaultExecutionOrder(10000)]
    public class FirstPersonCamera : MonoBehaviour
    {
        [Header("Refs")] public Transform yawPivot;
        public Transform pitchTarget;

        [Header("Look")] public float sensitivity = 2.5f;
        public float smoothTime = 0.06f;
        public float clampDown = -90f;
        public float clampUp = 75f;
        public bool useSmoothing = true;
        public bool invertY;
        
        [Header("Tilt / Roll")] [Range(0f, 45f)]
        public float maxWallrunTilt = 14f;

        [Range(0f, 45f)] public float vaultTilt = 8f;
        public float rollInSpeed = 10f; // grados/seg
        public float rollOutSpeed = 8f; // grados/seg
        public bool vaultAsPulse = true;
        [Min(0.05f)] public float vaultHoldTime = 0.20f;

        private float _yawT, _pitchT;
        private float _yawC, _pitchC;
        private float _yawVel, _pitchVel;

        private float _rollT, _rollC, _rollVel;
        private float _vaultTimer;

        private void Awake()
        {
            if (!pitchTarget) pitchTarget = GetComponentInChildren<UnityEngine.Camera>()?.transform ?? transform;
            if (!yawPivot) yawPivot = pitchTarget.parent;

            Cursor.lockState = CursorLockMode.Locked;

            _yawC = _yawT = yawPivot.localEulerAngles.y;
            _pitchC = _pitchT = Normalize(pitchTarget.localEulerAngles.x);
            _rollC = _rollT = Normalize(pitchTarget.localEulerAngles.z);
        }

        private void Update()
        {
            float mx = Input.GetAxisRaw("Mouse X") * sensitivity;
            float my = Input.GetAxisRaw("Mouse Y") * sensitivity * (invertY ? 1f : -1f);

            _yawT += mx;
            _pitchT = Mathf.Clamp(_pitchT + my, clampDown, clampUp);

            // pulso de vault (si está activo, cuenta atrás para volver a 0)
            if (vaultAsPulse && _vaultTimer > 0f)
            {
                _vaultTimer -= Time.deltaTime;
                if (_vaultTimer <= 0f) _rollT = 0f;
            }
        }

        private void LateUpdate()
        {
            // suavizado yaw/pitch original
            if (useSmoothing)
            {
                _yawC = Mathf.SmoothDampAngle(_yawC, _yawT, ref _yawVel, smoothTime);
                _pitchC = Mathf.SmoothDampAngle(_pitchC, _pitchT, ref _pitchVel, smoothTime);
            }
            else
            {
                _yawC = _yawT;
                _pitchC = _pitchT;
            }

            // suavizado roll con velocidades de entrada/salida
            float inOut = (Mathf.Abs(_rollT) > Mathf.Abs(_rollC)) ? rollInSpeed : rollOutSpeed;
            _rollC = Mathf.MoveTowardsAngle(_rollC, _rollT, inOut * Time.deltaTime);

            // aplicar rotaciones
            yawPivot.localRotation = Quaternion.Euler(0f, _yawC, 0f);
            pitchTarget.localRotation = Quaternion.Euler(_pitchC, 0f, _rollC);
        }

        private static float Normalize(float deg)
        {
            deg %= 360f;
            if (deg > 180f) deg -= 360f;
            return deg;
        }
        
        public void SetWallrunTilt(float signedSide)
        {
            signedSide = Mathf.Sign(Mathf.Clamp(signedSide, -1f, 1f));
            _rollT = signedSide * maxWallrunTilt;
        }
        
        public void SetVaultTiltRandom()
        {
            float signed = (Random.value < 0.5f) ? -1f : +1f;
            SetVaultTilt(signed);
        }
        
        public void SetVaultTilt(float signedSide)
        {
            signedSide = Mathf.Sign(Mathf.Clamp(signedSide, -1f, 1f));
            _rollT = signedSide * vaultTilt;
            if (vaultAsPulse) _vaultTimer = vaultHoldTime;
        }
        
        public void ClearTilt()
        {
            _rollT = 0f;
            _vaultTimer = 0f;
        }
    }
}