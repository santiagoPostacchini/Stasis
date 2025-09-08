using UnityEngine;

namespace Player.Scripts.MovementFSM
{
    public class View : MonoBehaviour
    {
        private static readonly int XAxisHash = Animator.StringToHash("xAxis");
        private static readonly int ZAxisHash = Animator.StringToHash("zAxis");
        private static readonly int IsStopping = Animator.StringToHash("isStopping");
        private static readonly int IsJumping = Animator.StringToHash("isJumping");
        
        [SerializeField] private Animator animator;
        
        [Header("<color=yellow>Animator Settings</color>")]
        
        private float _animX, _animZ, _xAxis, _zAxis;
        private float _targetAnimX, _targetAnimZ;
        [SerializeField] private float animLerpSpeed = 8f;
        private bool _isRun, _isStopping;

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
        }

        public void OnStop(bool stp)
        {
            _isStopping = stp;
        }

        public void OnLandEvent()
        {
            Debug.Log("Landed Event");
            //animator.SetTrigger(_landHash);
        }
        
        public void OnJumpEvent()
        {
            Debug.Log("Jump Event");
            animator.SetTrigger(IsJumping);
            animator.CrossFade("Player_Leg_Jump", 1);
            animator.CrossFade("Player_Arm_Jump", 0);
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
            //animator.SetTrigger(_vaultHash);
        }

        public void OnVaultEndEvent()
        {
            Debug.Log("Vault End Event");
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
    }
}