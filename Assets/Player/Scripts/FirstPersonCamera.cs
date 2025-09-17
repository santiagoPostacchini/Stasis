using UnityEngine;

namespace Player.Scripts
{
    [DefaultExecutionOrder(10000)]
    public class FirstPersonCamera : MonoBehaviour
    {
        [Header("Refs")] public Transform yawPivot; // e.g. Player/YawPivot
        public Transform pitchPivot; // e.g. YawPivot/PitchPivot
        public Transform rigTarget; // e.g. camera_target (opcional, para el cuerpo)

        [Header("Look")] public float sensitivity = 2.5f;
        public float smoothTime = 0.06f;
        public float clampDown = -90f;
        public float clampUp = 75f;
        public bool useSmoothing = true;
        public bool invertY;

        [Header("Tilt / Roll")] [Range(0f, 45f)]
        public float maxWallrunTilt = 14f;

        [Range(0f, 45f)] public float vaultTilt = 8f;
        public float rollInSpeed = 10f; // deg/s
        public float rollOutSpeed = 8f; // deg/s
        public bool vaultAsPulse = true;
        [Min(0.05f)] public float vaultHoldTime = 0.20f;

        [Header("Rig Follow (cuerpo)")]
        [Tooltip("0 = el cuerpo no sigue el pitch; 1 = igual a la cámara")]
        [Range(0f, 1f)]
        public float bodyPitchWeight = 0.7f;

        [Tooltip("Límite superior/inferior de cuánto puede inclinarse el cuerpo")]
        public float bodyPitchDown = -45f;

        public float bodyPitchUp = 45f;

        [Tooltip("Suavizado del seguimiento del cuerpo")]
        public float bodyFollowSmooth = 0.08f;

        [Header("Anti 'me paso a 3ª persona'")]
        [Tooltip("A partir de este delta el cuerpo fuerza seguir más a la cámara")]
        public float hardFollowThreshold = 20f;

        [Tooltip("Factor extra cuando se supera el umbral")]
        public float hardFollowBoost = 2.0f;

        [Header("Rig Safety Overrides")]
        [Tooltip(
            "Si está activo y bodyPitchWeight==0, fija el eje X local del rigTarget a la pose inicial cada frame.")]
        public bool lockRigXWhenZeroWeight = true; // NEW

        [Tooltip("Evita tocar Y y Z del rigTarget (deja que otros sistemas los usen).")]
        public bool dontTouchRigYawRoll = true;

        UnityEngine.Camera _cam;
        float _rigRestLocalX;
        Vector3 _rigRestLocalEuler;

        float _yawT, _pitchT;
        float _yawC, _pitchC;
        float _yawVel, _pitchVel;

        float _rollT, _rollC;
        float _vaultTimer;

        float _rigPitchC; // ángulo actual aplicado al rigTarget
        float _rigPitchVel; // para smooth
#if UNITY_EDITOR
        public float blockinputTimer = 1f;
#endif
        

    void Awake()
        {
            _cam = GetComponentInChildren<UnityEngine.Camera>();
            if (!_cam) _cam = GetComponent<UnityEngine.Camera>();

            if (!pitchPivot)
            {
                // Creamos un PitchPivot si falta (y mantenemos la misma pose)
                pitchPivot = new GameObject("PitchPivot").transform;
                pitchPivot.SetParent(yawPivot ? yawPivot : transform.parent, false);
                pitchPivot.position = transform.position;
                pitchPivot.rotation = transform.rotation;
                transform.SetParent(pitchPivot, true);
            }

            if (!yawPivot)
            {
                // Si no hay yawPivot, lo asumimos como el padre del pitchPivot
                yawPivot = pitchPivot.parent ? pitchPivot.parent : transform.parent;
                if (!yawPivot) yawPivot = transform;
            }

            if (rigTarget)
            {
                _rigRestLocalEuler = rigTarget.localEulerAngles;
                _rigRestLocalX = _rigRestLocalEuler.x;
            }

            Cursor.lockState = CursorLockMode.Locked;
            Vector3 yawEuler = yawPivot.localEulerAngles;
            _yawC = _yawT = yawEuler.y;

            Vector3 pitchEuler = pitchPivot.localEulerAngles;
            _pitchC = _pitchT = Normalize(pitchEuler.x);

            _rollC = _rollT = Normalize(transform.localEulerAngles.z);

            _rigPitchC = _pitchC; // iniciar alineado
        }

        void Update()
        {
#if UNITY_EDITOR
            if (blockinputTimer > 0f)
            {
                blockinputTimer -= Time.deltaTime;
                return;
            }
#endif
            float mx = Input.GetAxisRaw("Mouse X") * sensitivity;
            float my = Input.GetAxisRaw("Mouse Y") * sensitivity * (invertY ? 1f : -1f);

            _yawT += mx;
            _pitchT = Mathf.Clamp(_pitchT + my, clampDown, clampUp);

            // pulso de vault
            if (vaultAsPulse && _vaultTimer > 0f)
            {
                _vaultTimer -= Time.deltaTime;
                if (_vaultTimer <= 0f) _rollT = 0f;
            }
        }

        void LateUpdate()
        {
            // Suavizado yaw/pitch
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

            // Aplicar yaw/pitch a los pivotes (NO a la cámara)
            yawPivot.localRotation = Quaternion.Euler(0f, _yawC, 0f);
            pitchPivot.localRotation = Quaternion.Euler(_pitchC, 0f, 0f);

            // Roll solo en la cámara (tilts siguen funcionando)
            float rollSpeed = (Mathf.Abs(_rollT) > Mathf.Abs(_rollC)) ? rollInSpeed : rollOutSpeed;
            _rollC = Mathf.MoveTowardsAngle(_rollC, _rollT, rollSpeed * Time.deltaTime);
            transform.localRotation = Quaternion.Euler(0f, 0f, _rollC);

            if (!rigTarget) return;

            rigTarget.localEulerAngles = new Vector3(0f, _pitchC, 0f);
        }

        public void SetWallrunTilt(float signedSide)
        {
            signedSide = Mathf.Sign(Mathf.Clamp(signedSide, -1f, 1f));
            _rollT = signedSide * maxWallrunTilt;
        }

        public void SetVaultTiltRandom()
        {
            float s = (Random.value < 0.5f) ? -1f : 1f;
            SetVaultTilt(s);
        }

        private void SetVaultTilt(float signedSide)
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

        static float Normalize(float deg)
        {
            deg %= 360f;
            if (deg > 180f) deg -= 360f;
            return deg;
        }
    }
}