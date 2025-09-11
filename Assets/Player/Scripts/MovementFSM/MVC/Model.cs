using System;
using Player.FullBody_Scripts.MovementFSM;
using Player.Scripts.Interactor;
using Player.Stasis;
using UnityEngine;

namespace Player.Scripts.MovementFSM.MVC
{
    public class Model : MonoBehaviour
    {
        public event Action OnLand = delegate { };
        public event Action<float, float> OnMove = delegate { };
        public event Action OnJump = delegate { };
        public event Action OnShoot = delegate { };
        public event Action<bool> OnStop = delegate { };
        public event Action<bool> OnRun = delegate { };
        public event Action OnCrouchStart = delegate { };
        public event Action OnCrouchEnd = delegate { };
        public event Action OnVaultStart = delegate { };
        public event Action OnVaultEnd = delegate { };
        public event Action OnClimbStart = delegate { };
        public event Action OnClimbEnd = delegate { };
        public event Action OnSlideStart = delegate { };
        public event Action OnSlideEnd = delegate { };
        public event Action<float> OnWallrun = delegate { };
        public event Action OnGetDamage = delegate { };
        public event Action OnDeath = delegate { };

        IController _controller;

        private StasisGun _stasisGun;

        private PlayerInteractor _interactor;

        private FSM _fsm;

        public Rigidbody rb;
        
        private StairStepper _stair;
        
        private ParkourScanner _scanner;

        public ParkourProbe probe;

        [Header("Movement Keys")] 
        public KeyCode runningKey = KeyCode.LeftShift;
        public KeyCode jumpKey = KeyCode.Space;
        public KeyCode crouchKey = KeyCode.LeftControl;

        [Header("Mouse Keys")] public KeyCode mouseLeft = KeyCode.Mouse0;
        public KeyCode mouseRight = KeyCode.Mouse1;

        [Header("Camera Settings")] public Transform cameraHolderTransform;

        [Header("<color=green>Movement Settings</color>")]
        public float walkingSpeed = 4f;
        public float runningSpeed = 8f;
        public float acceleration = 20f;
        public float deceleration = 30f;
        public float jumpHeight = 5f;

        [Header("Jump Assist")] public float coyoteTime = 0.12f;
        public float jumpBufferTime = 0.12f;

        [Header("Air Control")] public bool airEnteredFromGround;
        public float airMaxSpeed = 6f;
        public float airAcceleration = 12f;
        public float minAirTime = 0.08f;
        public float landVelThreshold = -2.5f;
        
        [Header("Landing Event")]
        public float landEventCooldown = 0.15f;
        
        [HideInInspector] public bool  landedPending;
        [HideInInspector] public float lastAirTime;
        [HideInInspector] public float lastFallSpeed;   // v.y justo al salir del aire
        [HideInInspector] public float lastLandingTime = -999f;

        [Header("Wallrun")]
        public LayerMask wallMask;
        public LayerMask wallGroundMask;
        public float wallRunMaxSpeed = 8.0f;
        public float wallRunAccel    = 30.0f;
        public float wallStickForce  = 60.0f;
        public float wallRegrabCooldown = 0.25f;
        public float wallCruiseSpeed      = 4.0f;
        public float wallCruiseAccel      = 18.0f;
        public float wallGlideDownSpeed   = 1.2f;
        public float wallInputThreshold   = 0.12f;
        public float wallStandOff        = 0.18f;
        public float wallStickKp         = 220f;
        public float wallStickKd         = 16f;
        public float wallEnterBlendTime  = 0.18f;
        public float wallIntoDamp        = 8f;
        public float wallAlignLerp       = 12f; 
        [HideInInspector] public float blockWallrunUntil = -999f;

        [Tooltip("Fuerza hacia adelante a lo largo de la pared.")]
        public float wallRunForce = 25f;
        [Tooltip("Vel. vertical aplicada al mantener Shift (subir).")]
        public float wallClimbSpeed = 3.5f;
        [Tooltip("Vel. vertical aplicada al mantener Ctrl (bajar).")]
        public float wallDescendSpeed = 3.5f;

        [Tooltip("Tiempo máximo en pared (s).")]
        public float maxWallRunTime = 1.5f;
        [Tooltip("Tiempo de cooldown de salida forzada (s).")]
        public float exitWallTime = 0.3f;

        [Tooltip("Distancia para detectar pared a los lados.")]
        public float wallCheckDistance = 0.9f;
        [Tooltip("Altura mínima desde el suelo para poder iniciar/seguir wallrun.")]
        public float minJumpHeight = 0.6f;

        [Tooltip("Fuerza hacia arriba al saltar desde la pared (impulso).")]
        public float wallJumpUpForce = 7.5f;
        [Tooltip("Fuerza lateral alejándose de la pared (impulso).")]
        public float wallJumpSideForce = 6.5f;

        [Tooltip("Si true, mantenemos gravedad activada pero la contrarrestamos.")]
        public bool wallUseGravity = true;
        [Tooltip("Contrafuerza de gravedad durante wallrun.")]
        public float gravityCounterForce = 14f;

        [Tooltip("Empuje hacia la pared para ‘pegar’ al jugador.")]
        public float pushToWallForce = 100f;
        [Tooltip("Ángulo máx. entre la dir. de avance y el eje de la pared.")]
        [Range(0f, 80f)] public float wallToForwardMaxAngle = 55f;
        
        // Compartidos entre estados (timestamps)
        [HideInInspector] public float lastJumpPressedTime = -999f;
        [HideInInspector] public float lastLeftGroundTime = -999f;

        [HideInInspector] public float xAxis, zAxis, rawX, rawZ;
        [HideInInspector] public bool runningKeyPressed;
        [HideInInspector] public float stopThreshold = 0.05f;
        [HideInInspector] public float moveThreshold = 0.20f;
        [HideInInspector] public float stopCooldown = 0.2f;

        [HideInInspector] public bool wasMovingByInput;
        [HideInInspector] public float stopTimer;

        public bool canMove = true;
        public bool canRun = true;

        public bool HasJumpBuffered() => (Time.time - lastJumpPressedTime) <= jumpBufferTime;
        public void BufferJumpNow() => lastJumpPressedTime = Time.time;

        public bool HasJumpBufferedAfterLeftGround()
        {
            return lastJumpPressedTime >= (lastLeftGroundTime + 0.0001f);
        }

        public void ClearJumpBuffer()
        {
            lastJumpPressedTime = -999f;
        }
        
        [HideInInspector] public bool jumpDownThisFrame;
        
        public void RegisterJumpDownThisFrame()
        {
            jumpDownThisFrame = true;
        }

        private void Start()
        {
            _controller = new Controller(this, GetComponent<View>());
            _stasisGun = GetComponentInChildren<StasisGun>();
            _interactor = GetComponentInChildren<PlayerInteractor>();
            rb = GetComponent<Rigidbody>();
            _stair = GetComponent<StairStepper>();
            _scanner = GetComponent<ParkourScanner>();
        
            if (_scanner)
            {
                _scanner.OnProbeUpdated += p => probe = p;
                probe = _scanner.Probe;
            }

            _fsm = new FSM();
            _fsm.CreateState(FSM.States.Grounded, new S_Grounded(_fsm, this, cameraHolderTransform));
            _fsm.CreateState(FSM.States.Climb, new S_Climb(_fsm, this));
            _fsm.CreateState(FSM.States.Slide, new S_Slide(_fsm, this));
            _fsm.CreateState(FSM.States.Vault, new S_Vault(_fsm, this));
            _fsm.CreateState(FSM.States.Air, new S_Air(_fsm, this, cameraHolderTransform));
            _fsm.CreateState(FSM.States.Wallrun, new S_Wallrun(_fsm, this));
            _fsm.ChangeState(FSM.States.Grounded);
        }

        private void Update()
        {
            _controller.OnUpdate();
            _fsm.ArtificialUpdate();
        }

        private void FixedUpdate()
        {
            _fsm.ArtificialFixedUpdate();
            _stair.ManualFixedStep();
        }

        private void LateUpdate()
        {
            jumpDownThisFrame = false;
        }

        public void UpdateAxisInput(float x, float z, float rx, float rz)
        {
            xAxis = x;
            zAxis = z;
            rawX = rx;
            rawZ = rz;
            OnMove?.Invoke(x, z);
        }

        public void UpdateRunKey(bool pressed)
        {
            runningKeyPressed = pressed;
        }

        public void JumpInput()
        {
            OnJump?.Invoke();
        }

        public void ShootInput()
        {
            OnShoot?.Invoke();
        }

        public void UpdateStopping(bool stp)
        {
            OnStop?.Invoke(stp);
        }

        public void UpdateIsRunning(bool run)
        {
            OnRun?.Invoke(run);
        }

        public void LandedEvent()
        {
            OnLand?.Invoke();
        }

        public void WallrunEvent(float dir)
        {
            OnWallrun?.Invoke(dir);
        }
        
        internal bool IsGroundedNow()
        {
            return _scanner && _scanner.IsGrounded();
        }
    }
}