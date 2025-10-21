using UnityEngine;
using Unity.Cinemachine;

namespace Player.Scripts
{
    [DefaultExecutionOrder(12000)]
    public class FirstPersonCameraEffects : MonoBehaviour
    {
        [Header("Refs")]
        public Transform effectsPivot;
        public UnityEngine.Camera cam;

        [Header("FOV")]
        public bool  enableFovKick = true;
        [Tooltip("Si >0, fuerza ese FOV como base (ignora el de la vcam). Si 0, se toma el de la vcam activa cuando aparece.")]
        public float baseFovOverride = 75f;
        public float wallrunFovAdd = 4f;
        public float vaultFovAdd   = 3f;
        public float runFovAdd     = 6f;
        public float fovInSpeed    = 8f;
        public float fovOutSpeed   = 6f;

        [Header("Tilt / Roll")]
        [Range(0f, 45f)] public float wallrunTilt = 14f;
        [Range(0f, 45f)] public float vaultTilt  = 8f;
        public float rollInSpeed  = 10f;
        public float rollOutSpeed = 8f;
        public bool  vaultAsPulse = true;
        [Min(0.05f)] public float vaultHoldTime = 0.20f;

        [Header("Shake / Bob")]
        public bool enableShake = true;
        public float shakeFrequency = 18f;
        public float shakeDamping   = 6f;
        public bool enableHeadbob;
        public float bobSpeed = 8f;
        public float bobAmount = 0.03f;
        [Range(0f,1f)] public float idleBobWeight = 0.2f;

        // --- Nuevo: usar Dutch de Cinemachine para tilt ---
        [Header("Cinemachine")]
        [Tooltip("Si está activo, el tilt se aplica como Lens.Dutch (roll real en eje Z de la vcam).")]
        public bool useCinemachineDutch = true;

        // ----- internos -----
        float _rollTarget, _rollCurrent, _vaultTimer;
        float _fovCurrent;
        float _extraFovRuntime; // suma (run + wallrun + vault)
        float _shakeAmpRuntime; Vector2 _shakePhase;
        Vector3 _bobLocalOrigin;

        CinemachineBrain _brain;
        CinemachineCamera _activeCamCached; // vcam activa cacheada
        float _baseFovLive;                 // FOV base cacheado de la vcam activa
        bool  _haveBaseFov;

        void Awake()
        {
            if (!cam) cam = GetComponentInChildren<UnityEngine.Camera>();
            _brain = cam ? cam.GetComponent<CinemachineBrain>() : null;

            if (!effectsPivot)
            {
                var go = new GameObject("EffectsPivot");
                effectsPivot = go.transform;
                effectsPivot.SetParent(cam ? cam.transform : transform, false);
                effectsPivot.localPosition = Vector3.zero;
                effectsPivot.localRotation = Quaternion.identity;
            }
            _bobLocalOrigin = effectsPivot.localPosition;

            // Inicial: si hay override lo usamos; si no, esperaremos a que la vcam esté live
            _haveBaseFov = baseFovOverride > 0f;
            _baseFovLive = _haveBaseFov ? baseFovOverride : (cam ? cam.fieldOfView : 60f);
            _fovCurrent  = _baseFovLive;
            ApplyFovImmediate(_fovCurrent);
        }

        void OnEnable()
        {
            _rollTarget = _rollCurrent = NormalizeAngle(effectsPivot.localEulerAngles.z);
            _extraFovRuntime = 0f;
            _shakeAmpRuntime = 0f;
            RefreshActiveCamAndMaybeCacheBase();
            _fovCurrent = _baseFovLive;
            ApplyFovImmediate(_fovCurrent);

            // Asegurar Dutch en 0 al habilitar
            if (useCinemachineDutch) ApplyDutch(0f);
        }

        void Update()
        {
            RefreshActiveCamAndMaybeCacheBase();

            if (vaultAsPulse && _vaultTimer > 0f)
            {
                _vaultTimer -= Time.deltaTime;
                if (_vaultTimer <= 0f)
                    _rollTarget = Mathf.Approximately(Mathf.Abs(_rollTarget), vaultTilt) ? 0f : _rollTarget;
            }

            if (enableShake && _shakeAmpRuntime > 0f)
                _shakeAmpRuntime = Mathf.Max(0f, _shakeAmpRuntime - shakeDamping * Time.deltaTime);
        }

        void LateUpdate()
        {
            // --- ROLL (animación del tilt) ---
            float rollSpeed = (Mathf.Abs(_rollTarget) > Mathf.Abs(_rollCurrent)) ? rollInSpeed : rollOutSpeed;
            _rollCurrent = Mathf.MoveTowardsAngle(_rollCurrent, _rollTarget, rollSpeed * Time.deltaTime);

            if (useCinemachineDutch && HasActiveVcam())
            {
                // Tilt real en eje Z de la cámara (Dutch)
                ApplyDutch(_rollCurrent);
                // Dejá el pivot sin rotación: lo usamos sólo para bob/shake de posición
                effectsPivot.localRotation = Quaternion.identity;
            }
            else
            {
                // Fallback: rotación local del pivot en Z (como tenías)
                var e = effectsPivot.localEulerAngles;
                effectsPivot.localRotation = Quaternion.Euler(e.x, e.y, _rollCurrent);
            }

            // --- FOV (base cacheado + extras) ---
            if (enableFovKick)
            {
                float want = _baseFovLive + _extraFovRuntime;
                float speed = (want > _fovCurrent) ? fovInSpeed : fovOutSpeed;
                _fovCurrent = Mathf.MoveTowards(_fovCurrent, want, speed * Time.deltaTime);
                ApplyFovImmediate(_fovCurrent);
            }

            // --- Bob / Shake (sólo posición) ---
            Vector3 pos = _bobLocalOrigin;
            if (enableHeadbob && bobAmount > 0f)
            {
                float w = Mathf.Lerp(idleBobWeight, 1f, 0f);
                float t = Time.time * bobSpeed;
                pos += Vector3.up * (Mathf.Sin(t) * bobAmount * w);
            }
            if (enableShake && _shakeAmpRuntime > 0f)
            {
                _shakePhase += new Vector2(shakeFrequency, shakeFrequency * 1.21f) * Time.deltaTime;
                float sx = Mathf.Sin(_shakePhase.x) * (_shakeAmpRuntime * 0.0025f);
                float sy = Mathf.Cos(_shakePhase.y) * (_shakeAmpRuntime * 0.0025f);
                pos += new Vector3(sx, sy, 0f);
            }
            effectsPivot.localPosition = pos;
        }

        // ==== API externa ====
        // Nota: si tu scanner pasa wallSide como -1 izquierda / +1 derecha, usá este mapeo:
        public void WallrunStart(int wallSide)
        {
            wallSide = Mathf.Clamp(wallSide, -1, +1);
            // Convención: pared a la derecha ⇒ roll negativo (inclina a la derecha)
            _rollTarget = (wallSide > 0 ? -1f : +1f) * wallrunTilt;
            AddFov(+Mathf.Abs(wallrunFovAdd));
        }
        public void WallrunEnd()
        {
            _rollTarget = 0f;
            AddFov(-Mathf.Abs(wallrunFovAdd));
        }

        public void VaultStart(int sideSign = 0)
        {
            sideSign = Mathf.Clamp(sideSign, -1, 1);
            _rollTarget = (sideSign==0? 1: sideSign) * vaultTilt;
            if (vaultAsPulse) _vaultTimer = vaultHoldTime;
            AddFov(+Mathf.Abs(vaultFovAdd));
            AddImpulseShake(1.4f);
        }
        public void VaultEnd()
        {
            if (!vaultAsPulse) _rollTarget = 0f;
            if (useCinemachineDutch) ApplyDutch(0f);
            AddFov(-Mathf.Abs(vaultFovAdd));
        }

        public void OnRunStart() => AddFov(+Mathf.Abs(runFovAdd));
        public void OnRunEnd()   => AddFov(-Mathf.Abs(runFovAdd));

        private void AddImpulseShake(float deg)
        {
            if (!enableShake) return;
            _shakeAmpRuntime = Mathf.Max(_shakeAmpRuntime, Mathf.Abs(deg));
        }

        public void ClearAll()
        {
            _rollTarget = 0f; _vaultTimer = 0f; _extraFovRuntime = 0f; _shakeAmpRuntime = 0f;
            effectsPivot.localPosition = _bobLocalOrigin;
            effectsPivot.localRotation = Quaternion.identity;
            RefreshActiveCamAndMaybeCacheBase(force:true);
            _fovCurrent = _baseFovLive;
            ApplyFovImmediate(_fovCurrent);
            if (useCinemachineDutch) ApplyDutch(0f);
        }

        // ===== helpers =====
        void AddFov(float delta) { if (!enableFovKick) return; _extraFovRuntime += delta; }

        void RefreshActiveCamAndMaybeCacheBase(bool force=false)
        {
            if (!_brain) return;
            var active = _brain.ActiveVirtualCamera as CinemachineCamera; // CM3
            if (active != _activeCamCached || force)
            {
                _activeCamCached = active;
                if (!_haveBaseFov)
                {
                    if (_activeCamCached) _baseFovLive = _activeCamCached.Lens.FieldOfView;
                    else if (cam)         _baseFovLive = cam.fieldOfView;
                }
            }
        }

        bool HasActiveVcam()
        {
            if (!_brain) return false;
            return (_brain.ActiveVirtualCamera as CinemachineCamera) != null;
        }

        void ApplyDutch(float deg)
        {
            if (!_brain) return;
            var vcam = _brain.ActiveVirtualCamera as CinemachineCamera;
            if (!vcam) return;
            var lens = vcam.Lens;   // CM3 LensSettings
            lens.Dutch = deg;       // roll sobre Z (tilt real)
            vcam.Lens = lens;
        }

        void ApplyFovImmediate(float v)
        {
            if (_brain)
            {
                var vcam = _brain.ActiveVirtualCamera as CinemachineCamera;
                if (vcam)
                {
                    var lens = vcam.Lens;
                    lens.FieldOfView = v;
                    vcam.Lens = lens;
                    return;
                }
            }
            if (cam) cam.fieldOfView = v; // fallback cuando aún no hay vcam live
        }

        static float NormalizeAngle(float deg) { deg %= 360f; if (deg > 180f) deg -= 360f; return deg; }
    }
}