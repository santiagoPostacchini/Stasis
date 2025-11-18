using DG.Tweening;
using Player.Scripts.MovementFSM.MVC;
using UnityEngine;
using UnityEngine.Animations.Rigging;

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
        [SerializeField] private MultiParentConstraint spineConstraint;
        
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
        
        [Header("Climb IK")]
        [SerializeField] private ProceduralClimbIK climbIKHandler;
        [SerializeField] private TwoBoneIKConstraint leftHandIkRig;
        [SerializeField] private TwoBoneIKConstraint rightHandIkRig;
        [SerializeField] private float ikFadeDuration = 0.2f;
        
        [SerializeField] private float animLerpSpeed = 8f;
        private bool _isRun, _isStopping, _canInteract;

        private bool _worldGrounded;
        private bool _animGrounded = true;

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
        public void GroundedChangedEvent(bool grounded, float airTime, float fallSpeed)
        {
            _worldGrounded = grounded;
            if (grounded)
            {
                float totalAir = Time.time - _leftGroundTime;
                float fallDist = _leftGroundY - (rb ? rb.position.y : 0f);
                float impactSpeed = rb ? Mathf.Abs(rb.velocity.y) : 0f;
                
                if (playerCamEffects)
                {
                    playerCamEffects.TriggerLandTilt(impactSpeed, totalAir);
                }

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
                    if (!_canInteract)
                    {
                        if (_isInspecting) return;
                        animator.CrossFade("Player_Arm_Movement", 0.05f);
                    }
                }

                _airElapsed = 0f;
            }
            else
            {
                _leftGroundTime = Time.time;
                _leftGroundY = rb ? rb.position.y : 0f;

                _airElapsed = Mathf.Max(_airElapsed, airTime);
            }
        }

        public void OnJumpEvent()
        {
            _forceAirUntilTime = Time.time + 0.10f;
            _animGrounded = false;
            animator.SetBool(IsGrounded, false);

            animator.CrossFade("Player_Leg_Jump", 0f);
            if (_isInspecting) return;
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
        }

        public void OnVaultEndEvent()      
        { 
            if (playerCamEffects) playerCamEffects.VaultEnd();
        }
        
        public void OnClimbStartEvent(Vector3 forward)
        {
            Transform visualRoot = visualYawFollower.transform;
            if (visualRoot)
            {
                if (forward.sqrMagnitude < 0.001f) forward = -transform.forward;
                    
                Quaternion targetRotation = Quaternion.LookRotation(forward, Vector3.up);
                visualRoot.rotation = targetRotation;
            }
            
            if (visualYawFollower) visualYawFollower.enabled = false;
            if (spineConstraint) spineConstraint.weight = 0f;
            
            if (climbIKHandler) climbIKHandler.enabled = true;
            if (leftHandIkRig) leftHandIkRig.DOKill();
            if (rightHandIkRig) rightHandIkRig.DOKill();

            if (leftHandIkRig) DOTween.To(() => leftHandIkRig.weight, x => leftHandIkRig.weight = x, 1f, ikFadeDuration);
            if (rightHandIkRig) DOTween.To(() => rightHandIkRig.weight, x => rightHandIkRig.weight = x, 1f, ikFadeDuration);
            
            animator.CrossFade("Player_Leg_Climb", 0.05f);
            animator.CrossFade("Player_Arm_Climb", 0.05f);
            
            // Pass climbIK reference to camera effects for hand-based tilt
            if (playerCamEffects && climbIKHandler)
            {
                playerCamEffects.climbIK = climbIKHandler;
            }
            
            if (playerCamEffects) playerCamEffects.ClimbStart();
        }

        public void OnClimbEndEvent()
        {
            // Get model reference to check if we're jumping
            Model model = GetComponentInParent<Model>();
            bool isJumpingFromClimb = model && model.didClimbJump;
            
            if (visualYawFollower) visualYawFollower.enabled = true;
            if (spineConstraint) spineConstraint.weight = 0.75f;
            
            if (leftHandIkRig) leftHandIkRig.DOKill();
            if (rightHandIkRig) rightHandIkRig.DOKill();
            
            // If jumping from climb, immediately disable IK to prevent animation conflicts
            if (isJumpingFromClimb)
            {
                if (leftHandIkRig) leftHandIkRig.weight = 0f;
                if (rightHandIkRig) rightHandIkRig.weight = 0f;
                if (climbIKHandler) climbIKHandler.enabled = false;
            }
            else
            {
                // Normal fade out
                if (leftHandIkRig) DOTween.To(() => leftHandIkRig.weight, x => leftHandIkRig.weight = x, 0f, ikFadeDuration);
                if (rightHandIkRig) DOTween.To(() => rightHandIkRig.weight, x => rightHandIkRig.weight = x, 0f, ikFadeDuration)
                    .OnComplete(() => {
                        if (climbIKHandler) climbIKHandler.enabled = false;
                    });
            }
            
            if (playerCamEffects) playerCamEffects.ClimbEnd();
        }
        
        public void OnSlideStartEvent()    { }
        
        public void OnSlideEndEvent()      { }

        public void OnWallrunStartEvent(int dir)
        {
            animator.CrossFade(dir < 0 ? "Player_Leg_Wallrun_Left" : "Player_Leg_Wallrun_Right", 0.1f);
            animator.CrossFade(dir < 0 ? "Player_Arm_Wallrun_Left" : "Player_Arm_Wallrun_Right", 0.1f);
            if (playerCamEffects) playerCamEffects.WallrunStart(dir);
        }

        public void OnWallrunEndEvent()    { if (playerCamEffects) playerCamEffects.WallrunEnd(); }
        
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

        private bool _isInspecting = false;

        /// <summary>
        /// Reproduce Arms_Inspect en la layer de manos, sin interrupciones.
        /// </summary>
        public void InspectHands()
        {
            if (_isInspecting) return; // evitar spam
            _isInspecting = true;

            int armsLayer = 1; // <- cambia este índice si tu Arms_Layer es otro

            // Hacer CrossFade SOLO en la capa de brazos
            animator.CrossFade("Arms_Inspect", 0.1f, armsLayer);

            // Obtener duración real de la animación
            AnimatorStateInfo st =
                animator.GetCurrentAnimatorStateInfo(armsLayer);

            float clipLength = st.length;

            // Bloquear todas las animaciones de brazos durante la duración real
            DOVirtual.DelayedCall(clipLength, () =>
            {
                _isInspecting = false;
            });
        }
    }
}
