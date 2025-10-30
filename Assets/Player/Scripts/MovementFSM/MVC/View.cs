using Player.Scripts.IK;
using UnityEngine;
using static Player.Scripts.IK.HandIkFsm;

namespace Player.Scripts.MovementFSM.MVC
{
    public class View : MonoBehaviour
    {
        private static readonly int XAxisHash   = Animator.StringToHash("xAxis");
        private static readonly int ZAxisHash   = Animator.StringToHash("zAxis");
        private static readonly int IsStopping  = Animator.StringToHash("IsStopping");
        private static readonly int CanInteract = Animator.StringToHash("CanInteract");
        private static readonly int IsGrounded  = Animator.StringToHash("IsGrounded");
        private static readonly int IsDeepFall  = Animator.StringToHash("IsDeepFall");
        private static readonly int TurnLeftHash  = Animator.StringToHash("TurnLeft");
        private static readonly int TurnRightHash = Animator.StringToHash("TurnRight");

        [SerializeField] private Animator animator;
        [SerializeField] private FirstPersonCameraEffects playerCamEffects;
        [SerializeField] private VisualYawFollower visualYawFollower;

        [Header("Ik")]
        [SerializeField] private HandIkFsm handsIkFsm;
        [SerializeField] private FeetIkfsm feetIkFsm;
        
        [Header("Refs Filtro")]
        [SerializeField] private Rigidbody rb;
        [SerializeField] private StairStepper stepper;

        [Header("<color=yellow>Animator Settings</color>")]
        private float _animX, _animZ, _xAxis, _zAxis;

        [Header("Air Anim")]
        [Tooltip("Tiempo en el aire antes de usar la anim de caída al vacío.")]
        [SerializeField] private float deepFallDelay = 0.45f;
        [Tooltip("Tiempo mínimo en aire para permitir anim 'Air' si no hay salto claro.")]
        [SerializeField] private float airAnimMinTime = 0.3f;
        [Tooltip("Velocidad vertical mínima (|vy|) para permitir 'Air' inmediata.")]
        [SerializeField] private float airAnimMinFallSpeed = 1.0f;
        [Tooltip("Tiempo mínimo de aire para permitir 'Land' visible.")]
        [SerializeField] private float landAnimMinAirTime = 0.12f;
        [Tooltip("Caída mínima (metros) para permitir 'Land' visible.")]
        [SerializeField] private float landAnimMinFallDistance = 0.15f;
        [Tooltip("Velocidad vertical mínima al impactar para permitir 'Land' visible.")]
        [SerializeField] private float landAnimMinImpactSpeed = 1.0f;

        [SerializeField] private float animLerpSpeed = 8f;
        private bool _isRun, _isStopping, _canInteract;

        private bool _worldGrounded;
        private bool _animGrounded = true;

        // Timers/mediciones para el filtro
        private float _leftGroundTime;
        private float _leftGroundY;
        private float _forceAirUntilTime;

        private float _airElapsed;

        private void Awake()
        {
            if (!animator) animator = GetComponentInChildren<Animator>();
            if (!rb) rb = GetComponentInParent<Rigidbody>();
            if (!stepper) stepper = GetComponentInParent<StairStepper>();
        }

        private void Update()
        {
            // ---- movimiento (igual que antes) ----
            float targetMax = _isRun ? 1f : 0.5f;
            float targetAnimX = Mathf.Clamp(_xAxis, -1f, 1f) * targetMax;
            float targetAnimZ = Mathf.Clamp(_zAxis, -1f, 1f) * targetMax;

            _animX = Mathf.Lerp(_animX, targetAnimX, Time.deltaTime * animLerpSpeed);
            _animZ = Mathf.Lerp(_animZ, targetAnimZ, Time.deltaTime * animLerpSpeed);

            _animX = Mathf.Clamp(_animX, -1f, 1f);
            _animZ = Mathf.Clamp(_animZ, -1f, 1f);

            animator.SetFloat(XAxisHash, _animX);
            animator.SetFloat(ZAxisHash, _animZ);
            animator.SetBool(IsStopping, _isStopping);

            if (!_worldGrounded)
            {
                bool stepping = stepper && stepper.IsStepping;
                float sinceLeft = Time.time - _leftGroundTime;
                float vyAbs = rb ? Mathf.Abs(rb.velocity.y) : 0f;
                bool forceAir = Time.time < _forceAirUntilTime;

                bool qualifiesForAir =
                    !stepping && (forceAir || sinceLeft >= airAnimMinTime || vyAbs >= airAnimMinFallSpeed);

                if (qualifiesForAir && _animGrounded)
                {
                    _animGrounded = false;
                    animator.SetBool(IsGrounded, false);
                    _airElapsed = 0f;
                }

                if (!_animGrounded)
                {
                    _airElapsed += Time.deltaTime;
                    bool deep = _airElapsed >= deepFallDelay;
                    animator.SetBool(IsDeepFall, deep);
                }
            }
        }
        public void OnMove(float x, float z)
        {
            _xAxis = x;
            _zAxis = z;
        }

        public void OnRun(bool run)
        {
            bool wasRun = _isRun;
            _isRun = run;
            if (playerCamEffects)
            {
                if (run && !wasRun) playerCamEffects.OnRunStart();
                else if(!run && wasRun) playerCamEffects.OnRunEnd();
            }
        }

        public void OnStop(bool stp) => _isStopping = stp;
        public void GroundedChangedEvent(bool grounded, float airTime)
        {
            _worldGrounded = grounded;
            if (grounded)
            {
                // Vamos a decidir si mostramos anim de land o no
                float totalAir = Time.time - _leftGroundTime;
                float fallDist = _leftGroundY - (rb ? rb.position.y : 0f);
                float impactSpeed = rb ? Mathf.Abs(rb.velocity.y) : 0f;

                bool showLand = !_animGrounded && (
                                    totalAir >= landAnimMinAirTime ||
                                    fallDist >= landAnimMinFallDistance ||
                                    impactSpeed >= landAnimMinImpactSpeed
                                );

                _animGrounded = true;
                animator.SetBool(IsGrounded, true);
                animator.SetBool(IsDeepFall, false);

                if (!showLand)
                {
                    animator.CrossFade("Player_Leg_Movement", 0.05f);
                    animator.CrossFade("Player_Arm_Movement", 0.05f);
                }

                _airElapsed = 0f;
            }
            else
            {
                // No forzamos 'air' del Animator aún; sólo registramos el momento y altura de salida
                _leftGroundTime = Time.time;
                _leftGroundY = rb ? rb.position.y : 0f;

                // Mantengo compatibilidad con tu contador si lo usás externamente
                _airElapsed = Mathf.Max(_airElapsed, airTime);
            }
            feetIkFsm.TryGround();
        }

        public void OnJumpEvent()
        {
            // Esto es un salto real => queremos Air inmediato en Animator
            _forceAirUntilTime = Time.time + 0.10f;
            _animGrounded = false;
            animator.SetBool(IsGrounded, false);

            animator.CrossFade("Player_Leg_Jump", 0f);
            animator.CrossFade("Player_Arm_Jump", 0f);
        }

        public void OnShootEvent()         { }
        public void OnCrouchStartEvent()   { }
        public void OnCrouchEndEvent()     { }

        public void OnVaultStartEvent()
        {
            animator.CrossFade("Player_Leg_Vault", 0.1f);
            animator.CrossFade("Player_Arm_Vault", 0.1f);
            animator.applyRootMotion = false;
            if (playerCamEffects) playerCamEffects.VaultStart();
            handsIkFsm.ForceState(HandState.Vault);
        }

        public void OnVaultEndEvent()      
        { 
            if (playerCamEffects) playerCamEffects.VaultEnd();
            handsIkFsm.ForceState(HandState.Idle);
        }
        public void OnClimbStartEvent()
        {
            if (visualYawFollower) visualYawFollower.followEnabled = false;
        }
        public void OnClimbEndEvent()      { if (visualYawFollower) visualYawFollower.followEnabled = true; }
        public void OnSlideStartEvent()    { }
        public void OnSlideEndEvent()      { }

        public void OnWallrunStartEvent(int dir)
        {
            animator.CrossFade(dir > 0 ? "Player_Leg_Wallrun_Left" : "Player_Leg_Wallrun_Right", 0f);
            animator.CrossFade(dir > 0 ? "Player_Arm_Wallrun_Left" : "Player_Arm_Wallrun_Right", 0f);
            if (playerCamEffects) playerCamEffects.WallrunStart(dir);
            handsIkFsm.ForceState(HandState.Wallrun);
        }

        public void OnWallrunEndEvent()    { if (playerCamEffects) playerCamEffects.WallrunEnd(); handsIkFsm.ForceState(HandState.Idle); }
        public void OnDamageEvent()        { }
        public void OnDeathEvent()         { }
        public void OnGrabEvent()          { }
        public void OnTurnYaw(int dir)     { animator.CrossFade(dir < 0 ? TurnLeftHash : TurnRightHash, 0.1f); }
        public void OnDropEvent()          { }
        public void OnThrowEvent()         { }

        public void OnCanInteractEnterEvent()
        {
            _canInteract = true;
            animator.SetBool(CanInteract, _canInteract);
            animator.CrossFade("Player_Arm_CanPickup", 0.1f);
        }

        public void OnCanInteractExitEvent()
        {
            _canInteract = false;
            animator.SetBool(CanInteract, _canInteract);
        }

        public void OnInteractEvent()
        {
            _canInteract = false;
            animator.SetBool(CanInteract, _canInteract);
            animator.CrossFade("Player_Arm_Interact", 0.1f);
        }
    }
}
