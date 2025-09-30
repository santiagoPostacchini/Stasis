using UnityEngine;

namespace Player.Scripts.MovementFSM.MVC
{
    public class View : MonoBehaviour
    {
        private static readonly int XAxisHash = Animator.StringToHash("xAxis");
        private static readonly int ZAxisHash = Animator.StringToHash("zAxis");
        private static readonly int IsStopping = Animator.StringToHash("IsStopping");
        private static readonly int CanInteract = Animator.StringToHash("CanInteract");
        private static readonly int IsGrounded = Animator.StringToHash("IsGrounded");
        private static readonly int TurnLeftHash = Animator.StringToHash("TurnLeft");
        private static readonly int TurnRightHash = Animator.StringToHash("TurnRight");

        [SerializeField] private Animator animator;
        [SerializeField] private FirstPersonCameraEffects playerCamEffects;

        [Header("<color=yellow>Animator Settings</color>")]
        private float _animX, _animZ, _xAxis, _zAxis;

        private float _targetAnimX, _targetAnimZ;
        [SerializeField] private float animLerpSpeed = 8f;
        private bool _isRun, _isStopping, _canInteract;

        private void Awake()
        {
            animator = GetComponentInChildren<Animator>();
        }

        private void Update()
        {
            float targetMax = _isRun ? 1f : 0.5f;
            _targetAnimX = Mathf.Clamp(_xAxis, -1f, 1f) * targetMax;
            _targetAnimZ = Mathf.Clamp(_zAxis, -1f, 1f) * targetMax;

            _animX = Mathf.Lerp(_animX, _targetAnimX, Time.deltaTime * animLerpSpeed);
            _animZ = Mathf.Lerp(_animZ, _targetAnimZ, Time.deltaTime * animLerpSpeed);

            _animX = Mathf.Clamp(_animX, -1f, 1f);
            _animZ = Mathf.Clamp(_animZ, -1f, 1f);

            animator.SetFloat(XAxisHash, _animX);
            animator.SetFloat(ZAxisHash, _animZ);
            animator.SetBool(IsStopping, _isStopping);
        }

        public void OnMove(float x, float z)
        {
            _xAxis = x;
            _zAxis = z;
        }

        public void OnRun(bool run)
        {
            _isRun = run;
            if (playerCamEffects)
            {
                if (run && !_isRun) playerCamEffects.OnRunStart();
                else if(!run && _isRun)    playerCamEffects.OnRunEnd();
            }
        }

        public void OnStop(bool stp)
        {
            _isStopping = stp;
        }

        public void GroundedChangedEvent(bool val)
        {
            Debug.Log("Grounded Changed Event");
            animator.SetBool(IsGrounded, val);
        }

        public void OnJumpEvent()
        {
            Debug.Log("Jump Event");
            animator.CrossFade("Player_Leg_Jump", 0);
            animator.CrossFade("Player_Arm_Jump", 0);
            //playerCam.ClearTilt();
            //EventManager.TriggerEvent("OnJump", gameObject);
        }

        public void OnShootEvent()
        {
            Debug.Log("Shoot Event");
            //animator.SetTrigger(_jumpHash);
            //EventManager.TriggerEvent("OnJump", gameObject);
        }

        public void OnCrouchStartEvent()
        {
            Debug.Log("Crouch Start Event");
            //animator.SetBool(_crouchHash, isCrouching);
        }

        public void OnCrouchEndEvent()
        {
            Debug.Log("Crouch End Event");
            //animator.SetBool(_crouchHash, isCrouching);
        }

        public void OnVaultStartEvent()
        {
            Debug.Log("Vault Start Event");
            animator.CrossFade("Player_Leg_Vault", 0.1f);
            animator.CrossFade("Player_Arm_Vault", 0.1f);
            animator.applyRootMotion = false;
            playerCamEffects.VaultStart();
            //animator.SetTrigger(_vaultHash);
        }

        public void OnVaultEndEvent()
        {
            Debug.Log("Vault End Event");
            playerCamEffects.VaultEnd();
        }

        public void OnClimbStartEvent()
        {
            Debug.Log("Climb Start Event");
            //EventManager.TriggerEvent("OnClimb", gameObject);
        }

        public void OnClimbEndEvent()
        {
            Debug.Log("Climb End Event");
            //EventManager.TriggerEvent("OnClimb", gameObject);
        }

        public void OnSlideStartEvent()
        {
            Debug.Log("Slide Start Event");
        }

        public void OnSlideEndEvent()
        {
            Debug.Log("Slide End Event");
        }

        public void OnWallrunStartEvent(float dir)
        {
            Debug.Log($"Wall Slide Event with dir: {dir}");
            animator.CrossFade(dir > 0 ? "Player_Leg_Wallrun_Left" : "Player_Leg_Wallrun_Right", 0);
            animator.CrossFade(dir > 0 ? "Player_Arm_Wallrun_Left" : "Player_Arm_Wallrun_Right", 0);
            playerCamEffects.WallrunStart();
        }

        public void OnWallrunEndEvent()
        {
            playerCamEffects.WallrunEnd();
        }

        public void OnDamageEvent()
        {
            Debug.Log("Damage Event");
        }

        public void OnDeathEvent()
        {
            Debug.Log("Death Event");
        }

        public void OnGrabEvent()
        {
            Debug.Log("Grab Event");
            //animator.SetTrigger(_grabHash);
            //EventManager.TriggerEvent("OnObjectGrab", gameObject);
        }

        public void OnTurnYaw(int dir)
        {
            if (dir < 0)
            {
                animator.CrossFade(TurnLeftHash, 0.1f);
            }
            else
            {
                animator.CrossFade(TurnRightHash, 0.1f);
            }
        }

        public void OnDropEvent()
        {
            Debug.Log("Drop Event");
            //animator.SetTrigger(_dropHash);
        }

        public void OnThrowEvent()
        {
            Debug.Log("Throw Event");
            //animator.SetTrigger(_throwHash);
        }

        public void OnCanInteractEnterEvent()
        {
            Debug.Log("Can Interact Start");
            animator.CrossFade("Player_Arm_CanPickup", 0.1f);
            _canInteract = true;
            animator.SetBool(CanInteract, _canInteract);
        }

        public void OnCanInteractExitEvent()
        {
            Debug.Log("Can Interact Exit");
            _canInteract = false;
            animator.SetBool(CanInteract, _canInteract);
            //animator.SetTrigger(_throwHash);
        }

        public void OnInteractEvent()
        {
            Debug.Log("Interact Event");
            _canInteract = false;
            animator.SetBool(CanInteract, _canInteract);
            animator.CrossFade("Player_Arm_Interact", 0.1f);
            //EventManager.TriggerEvent("OnInteract", gameObject);
        }
    }
}