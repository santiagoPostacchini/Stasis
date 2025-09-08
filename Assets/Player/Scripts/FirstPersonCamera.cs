using UnityEngine;

namespace Player.Scripts
{
    
    [DefaultExecutionOrder(10000)]
    public class FirstPersonCamera : MonoBehaviour
    {
        [Header("Refs")]
        public Transform yawPivot;
        public Transform pitchTarget;

        [Header("Look")]
        public float sensitivity = 2.5f;
        public float smoothTime = 0.06f;
        public float clampDown = -90f;
        public float clampUp   =  75f;
        public bool useSmoothing = true;
        public bool invertY;

        private float _yawT, _pitchT;
        private float _yawC, _pitchC;
        private float _yawVel, _pitchVel;

        private void Awake()
        {
            if (!pitchTarget) pitchTarget = GetComponentInChildren<UnityEngine.Camera>()?.transform ?? transform;
            if (!yawPivot)    yawPivot    = pitchTarget.parent;

            Cursor.lockState = CursorLockMode.Locked;

            _yawC = _yawT = yawPivot.localEulerAngles.y;
            _pitchC = _pitchT = Normalize(pitchTarget.localEulerAngles.x);
        }

        private void Update()
        {
            float mx = Input.GetAxisRaw("Mouse X") * sensitivity;
            float my = Input.GetAxisRaw("Mouse Y") * sensitivity * (invertY ? 1f : -1f);

            _yawT   += mx;
            _pitchT  = Mathf.Clamp(_pitchT + my, clampDown, clampUp);
        }

        private void LateUpdate()
        {
            if (useSmoothing)
            {
                _yawC   = Mathf.SmoothDampAngle(_yawC,   _yawT,   ref _yawVel,   smoothTime);
                _pitchC = Mathf.SmoothDampAngle(_pitchC, _pitchT, ref _pitchVel, smoothTime);
            }
            else
            {
                _yawC = _yawT;
                _pitchC = _pitchT;
            }
            
            yawPivot.localRotation     = Quaternion.Euler(0f, _yawC, 0f);
            pitchTarget.localRotation  = Quaternion.Euler(_pitchC, 0f, 0f);
        }

        private static float Normalize(float deg) { deg %= 360f; if (deg > 180f) deg -= 360f; return deg; }
        
    }
}
