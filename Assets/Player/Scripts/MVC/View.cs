using Player.Camera;
using UnityEngine;
using Managers.Events;
namespace Player.Scripts.MVC
{
    public class View : MonoBehaviour
    {
        private readonly int _speedHash = Animator.StringToHash("Speed");
        private readonly int _climbHash = Animator.StringToHash("Climb");
        private readonly int _jumpHash = Animator.StringToHash("Jump");
        private readonly int _crouchHash = Animator.StringToHash("Crouch");
        private readonly int _landHash = Animator.StringToHash("Land");
        private readonly int _grabHash = Animator.StringToHash("Grab");
        private readonly int _dropHash = Animator.StringToHash("Drop");
        private readonly int _throwHash = Animator.StringToHash("Throw");
        private readonly int _shotHash = Animator.StringToHash("Shot");
        private readonly int _vaultHash = Animator.StringToHash("Vault");
        
        public Animator animator;
        public PlayerCam cam;
        
        public Material damageMaterialPostProcess;
        public ArmAnimationHandler armAnimationHandler;

        [SerializeField] private HurtEffect hurtEffect;

        private Quaternion _originalRotation;                 // Guarda rotaci�n original (hacia abajo)
        private bool _originalRotationSaved;

        [SerializeField] private CinematicBars cinematicBars;
        
        private void Start()
        {
            hurtEffect = GetComponentInChildren<HurtEffect>();
        }
        
        public void OnJumpEvent()
        {
            Debug.Log("Jumping!");
            animator.SetTrigger(_jumpHash);
            EventManager.TriggerEvent("OnJump", gameObject);
        }
        
        public void OnShotEvent()
        {
            Debug.Log("Shooting!");
            animator.SetTrigger(_shotHash);
            EventManager.TriggerEvent("OnShot", gameObject);
        }

        public void OnLandEvent()
        {
            Debug.Log("Landed!");
            animator.SetTrigger(_landHash);
        }

        public void OnCrouchEvent(bool isCrouching)
        {
            string txt = isCrouching ? "Crouching" : "Uncrouching";
            Debug.Log(txt);
            animator.SetBool(_crouchHash, isCrouching);
        }
        
        public void OnGrabEvent()
        {
            Debug.Log("Grabing!");
            animator.SetTrigger(_grabHash);
            EventManager.TriggerEvent("OnObjectGrab", gameObject);
        } 
        
        public void OnDropEvent()
        {
            Debug.Log("Dropping!");
            animator.SetTrigger(_dropHash);
        }

        public void OnThrowEvent()
        {
            Debug.Log("Throwing!");
            animator.SetTrigger(_throwHash);
        }
        
        public void OnVaultStartEvent()
        {
            Debug.Log("Vault Start");
            animator.SetTrigger(_vaultHash);
            var rand = Random.Range(0, 2) * 2 - 1;
            cam.DoTilt(10f * rand);
        }
        
        public void OnVaultEndEvent()
        {
            Debug.Log("Vault End");
            cam.DoTilt(0f);
        }

        public void OnDamageEvent()
        {
            Debug.Log("Damaged");
            hurtEffect.ShowHurtEffect();
            EventManager.TriggerEvent("Hit", gameObject);
            GetDamageVFX();
        }

        private void GetDamageVFX()
        {
            Debug.Log("Damage VFX!");
        }
        public void OnSpeedChangeEvent(float speed)
        {
            animator.SetFloat(_speedHash, speed);

            if (armAnimationHandler)
            {
                armAnimationHandler.UpdateSpeed(speed);
            }
        }
       
        public void OnClimbEvent()
        {
            animator.SetTrigger(_climbHash);
            EventManager.TriggerEvent("OnClimb", gameObject);
        }

        public void OnSlideStart()
        {
            cam.DoTilt(5f);
            cam.DoFov(105f);
        }

        public void OnSlideEnd()
        {
            cam.DoTilt(0f);
            cam.DoFov(90f);
        }
        
    }
}