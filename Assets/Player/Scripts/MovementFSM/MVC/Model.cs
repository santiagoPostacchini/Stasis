using System;
using Audio.Scripts;
using DG.Tweening;
using Player.FullBody_Scripts.MovementFSM;
using Player.Scripts.Interactor;
using Player.Scripts.MovementFSM.Player.Scripts.MovementFSM;
using Player.Stasis;
using Unity.Cinemachine;
using UnityEngine;

namespace Player.Scripts.MovementFSM.MVC
{
    public class Model : MonoBehaviour, ISoundPlayer
    {
        public event Action<bool, float> OnGroundedChanged = delegate { };
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
        public event Action<int> OnWallrunStart = delegate { };
        public event Action OnWallrunEnd = delegate { };
        public event Action OnGetDamage = delegate { };
        public event Action OnDeath = delegate { };
        public event Action<int> OnTurnYaw = delegate { };
        public event Action OnInteractFocusEnter = delegate { };
        public event Action OnInteractFocusExit = delegate { };
        public event Action OnInteract = delegate { };
        
        IController _controller;
        private StasisGun _stasisGun;
        private PlayerInteractor _interactor;
        private StairStepper _stair;
        internal ParkourScanner Scanner;
        private FSM _fsm;

        [Header("References")] public Rigidbody rb;
        public Transform cameraHolderTransform;
        public CinemachineBrain cinemachineBrain;
        public ParkourProbe probe;

        [Header("Movement Keys")] public KeyCode runningKey = KeyCode.LeftShift;
        public KeyCode jumpKey = KeyCode.Space;
        public KeyCode crouchKey = KeyCode.LeftControl;

        [Header("Mouse Keys")] public KeyCode mouseLeft = KeyCode.Mouse0;
        public KeyCode mouseRight = KeyCode.Mouse1;

        [Header("Layers")] public LayerMask groundMask;
        public LayerMask wallMask;
        
        [Header("Parkour Tags (usar en colliders)")]
        public string tagVault   = "Vault";
        public string tagClimb   = "Climb";
        public string tagWallrun = "Wallrun";

        [Header("<color=green>Movement Settings</color>")]
        public float walkingSpeed = 4f;

        public float runningSpeed = 8f;
        public float acceleration = 20f;
        public float deceleration = 30f;
        public float jumpHeight = 5f;

        [HideInInspector] public bool isRunningRuntime;

        [Header("Jump Assist")] public float coyoteTime = 0.12f;
        public float jumpBufferTime = 0.12f;

        [Header("Ground Stick / Snap")]
        [Tooltip("Si true, al estar grounded se fuerza vel.Y=0 (previene creep y caída por bordes).")]
        public bool zeroYVelocityWhenGrounded = true;

        [Range(0f, 80f), Tooltip("Desde qué pendiente permitimos deslizar y NO forzamos vel.Y=0.")]
        public float slideFromSlopeDeg = 30f;

        [Tooltip("Límite por FixedUpdate para el snap vertical (evita teleports).")]
        public float snapMaxStepPerFixed = 0.20f;

        [Header("Air Control")] public bool airEnteredFromGround;
        public float airMaxSpeed = 6f;
        public float airAcceleration = 12f;
        public float minAirTime = 0.08f;
        public float landVelThreshold = -2.5f;

        [Header("Landing Event")] public float landEventCooldown = 0.15f;
        [HideInInspector] public bool landedPending;
        [HideInInspector] public float lastAirTime;
        [HideInInspector] public float lastFallSpeed;
        [HideInInspector] public float lastLandingTime = -999f;

        [Header("Vault")] public float vaultRegrabCooldown = 0.25f;
        [HideInInspector] public float blockVaultUntil = -999f;

        [Header("Climb")] public float climbRegrabCooldown = 0.25f;
        [HideInInspector] public float blockClimbUntil = -999f;

        [Header("Wallrun")] public float wallRunMaxSpeed = 8.0f;
        public float wallRunAccel = 30.0f;
        public float wallCruiseSpeed = 4.0f;
        public float wallCruiseAccel = 18.0f;
        public float wallGlideDownSpeed = 1.2f;
        public float wallInputThreshold = 0.12f;
        public float wallStandOff = 0.18f;
        public float wallStickKp = 220f;
        public float wallStickKd = 16f;
        public float wallEnterBlendTime = 0.18f;
        public float wallIntoDamp = 8f;
        public float wallAlignLerp = 12f;
        public Collider lastWallCollider;
        public Vector3  lastWallNormal;
        public float    lastWallDetachTime;
        public float wallReorientDuration = 0.2f;
        
        [Tooltip("Tiempo máx. para sostener el estado cuando se pierde el hit entre dos paredes (s)")]
        public float wallSeamHold = 0.12f;

        [Tooltip("Velocidad de suavizado de la normal de pared (lerp exp)")]
        public float wallNormalSmooth = 14f;

        [HideInInspector] public float blockWallrunUntil = -999f;

        [Header("Jump/Ground Timing Guards")] public float groundedIgnoreAfterJump = 0.08f;
        [HideInInspector] public float groundedIgnoreUntil = -999f;

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

        [Tooltip("Fuerza hacia arriba al saltar desde la pared (impulso).")]
        public float wallJumpUpForce = 7.5f;

        [Tooltip("Fuerza lateral alejándose de la pared (impulso).")]
        public float wallJumpSideForce = 6.5f;

        [Tooltip("Si true, mantenemos gravedad activada pero la contrarrestamos.")]
        public bool wallUseGravity = true;

        [Tooltip("Contrafuerza de gravedad durante wallrun.")]
        public float gravityCounterForce = 14f;

        [Header("Runtime / Shared")] [HideInInspector]
        public float lastJumpPressedTime = -999f;
        
        [Tooltip("Tiempo sin poder reengancharse a paredes después de un walljump")]
        public float wallPostJumpNoRegrab = 0.20f; // 0.18–0.25 va bien

        [HideInInspector] public float wallJustJumpedUntil = -999f;
        [HideInInspector] public float wallSeamDisableUntil = -999f;

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

        [HideInInspector] public bool jumpDownThisFrame;

        public bool HasJumpBuffered() => (Time.time - lastJumpPressedTime) <= jumpBufferTime;

        public void BufferJumpNow() => lastJumpPressedTime = Time.time;

        public bool HasJumpBufferedAfterLeftGround()
            => lastJumpPressedTime >= (lastLeftGroundTime + 0.0001f);

        public void ClearJumpBuffer() => lastJumpPressedTime = -999f;

        public void RegisterJumpDownThisFrame() => jumpDownThisFrame = true;

        private void Start()
        {
            _controller = new Controller(this, GetComponent<View>());
            _stasisGun = GetComponentInChildren<StasisGun>();
            _interactor = GetComponentInChildren<PlayerInteractor>();
            rb = GetComponent<Rigidbody>();
            _stair = GetComponent<StairStepper>();
            Scanner = GetComponent<ParkourScanner>();
            
            if (!cinemachineBrain)
            {
                cinemachineBrain = GetComponentInChildren<CinemachineBrain>();

                if (!cinemachineBrain && UnityEngine.Camera.main)
                {
                    cinemachineBrain = UnityEngine.Camera.main.GetComponent<CinemachineBrain>();
                }
            }
            
            _interactor.OnInteractPerformed += HandleInteractPerformed;
            _interactor.OnInteractableFocusEnter += HandleFocusEnter;
            _interactor.OnInteractableFocusExit += HandleFocusExit;

            if (Scanner)
            {
                Scanner.OnProbeUpdated += p => probe = p;
                probe = Scanner.Probe;

                Scanner.OnGroundedChanged += HandleGroundedChanged;
                
                Scanner.groundMask      = groundMask;
                Scanner.environmentMask = groundMask | wallMask;
                Scanner.tagVault        = tagVault;
                Scanner.tagClimb        = tagClimb;
                Scanner.tagWallrun      = tagWallrun;
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
            lastFallSpeed = rb ? rb.velocity.y : 0f;
            _fsm.ArtificialFixedUpdate();
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

        public void UpdateRunKey(bool pressed) => runningKeyPressed = pressed;

        // ReSharper disable Unity.PerformanceAnalysis
        public void JumpInput() => OnJump?.Invoke();

        // ReSharper disable Unity.PerformanceAnalysis
        public void ShootInput() => OnShoot?.Invoke();

        public void UpdateStopping(bool stp) => OnStop?.Invoke(stp);
        public void UpdateIsRunning(bool run) => OnRun?.Invoke(run);

        // ReSharper disable Unity.PerformanceAnalysis
        private void GroundChangedEvent(bool grounded, float airTime)
            => OnGroundedChanged?.Invoke(grounded, airTime);

        // ReSharper disable Unity.PerformanceAnalysis
        public void WallrunStartEvent(int dir) => OnWallrunStart?.Invoke(dir);
        public void WallrunEndEvent() => OnWallrunEnd?.Invoke();
        
        public void ClimbStartEvent() => OnClimbStart?.Invoke();
        
        public void ClimbEndEvent() => OnClimbEnd?.Invoke();

        // ReSharper disable Unity.PerformanceAnalysis
        public void VaultStartEvent() => OnVaultStart?.Invoke();

        // ReSharper disable Unity.PerformanceAnalysis
        public void VaultEndEvent() => OnVaultEnd?.Invoke();

        internal bool IsGroundedNow() => Scanner && Scanner.IsGrounded();

        // ReSharper disable Unity.PerformanceAnalysis
        private void HandleFocusEnter() => OnInteractFocusEnter?.Invoke();

        // ReSharper disable Unity.PerformanceAnalysis
        private void HandleFocusExit() => OnInteractFocusExit?.Invoke();

        // ReSharper disable Unity.PerformanceAnalysis
        private void HandleInteractPerformed() => OnInteract?.Invoke();

        private void HandleGroundedChanged(bool grounded, RaycastHit hit)
        {
            if (!grounded)
            {
                // acaba de dejar el suelo
                GroundChangedEvent(false, 0f);
                lastLeftGroundTime = Time.time;
                airEnteredFromGround = true;
                landedPending = false;
                return;
            }

            if (Time.time < groundedIgnoreUntil)
            {
                // si decidís “ignorar” este grounded, reportá como no-grounded con el tiempo transcurrido
                GroundChangedEvent(false, Time.time - lastLeftGroundTime);
                return;
            }

            if (HasJumpBuffered())
            {
                GroundChangedEvent(false, Time.time - lastLeftGroundTime);
                landedPending = false;
                lastLandingTime = Time.time;
                return;
            }

            // landed
            float airTime = Time.time - lastLeftGroundTime;
            GroundChangedEvent(true, airTime);

            // (tu lógica existente de landing debajo se mantiene igual)
            float at = airTime;
            bool minAirOk  = at >= minAirTime;
            bool impactOk  = lastFallSpeed <= landVelThreshold;
            bool cooldownOk = (Time.time - lastLandingTime) > landEventCooldown;

            if (cooldownOk && (minAirOk && impactOk))
            {
                lastLandingTime = Time.time;
                landedPending = false;
            }
            else
            {
                landedPending = true;
            }
        }

        public bool IsIdleForTurn()
        {
            if (isRunningRuntime) return false;

            bool noInput = (Mathf.Abs(xAxis) < 0.05f && Mathf.Abs(zAxis) < 0.05f);

            var hv = rb ? new Vector3(rb.velocity.x, 0f, rb.velocity.z) : Vector3.zero;
            bool lowSpeed = hv.magnitude < 0.15f;
            return noInput && lowSpeed;
        }

        public void RequestTurnYaw(int dir)
        {
            dir = (dir >= 0) ? +1 : -1;
            OnTurnYaw(dir);
        }
    }
}