using Player.Camera;
using UnityEngine;
using Managers.Events;


namespace Player.Scripts.MVC
{
    public class View : MonoBehaviour
    {
        private readonly int _jumpHash = Animator.StringToHash("Jump");
        private readonly int _crouchHash = Animator.StringToHash("Crouch");
        private readonly int _landHash = Animator.StringToHash("Land");
        private readonly int _grabHash = Animator.StringToHash("Grab");
        private readonly int _dropHash = Animator.StringToHash("Drop");
        private readonly int _throwHash = Animator.StringToHash("Throw");
        private readonly int _shotHash = Animator.StringToHash("Shot");
        private readonly int _idleHash = Animator.StringToHash("Idle");
        private readonly int _climbHash = Animator.StringToHash("Climb");

        public Animator animator; //hay que modificar el animator para utilizar estos nuevos valores 
        public AudioSource audioSource;
        public PlayerCam cam;
        
        public Material damageMaterialPostProcess;
        public ArmAnimationHandler armAnimationHandler;
        public void OnJumpEvent()
        {
            Debug.Log("Jumping!");
            animator.SetTrigger(_jumpHash);
            //EventManager.TriggerEvent("Jump", gameObject);
        }
        public void Shoot()
        {
            OnShotEvent(); // Animación en cuerpo
            EventManager.TriggerEvent("OnShot", gameObject); // Evento para brazos
        }
        public void OnShotEvent()
        {
            Debug.Log("Shooting!");
            animator.SetTrigger(_shotHash);
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

        public void OnIdleEvent()
        {
            animator.SetTrigger(_idleHash);
        }
        public void GrabObject()
        {
            OnGrabEvent();  // Animación del cuerpo
            EventManager.TriggerEvent("OnObjectGrab", gameObject); // Evento para brazos
        }
        public void OnGrabEvent()
        {
            Debug.Log("Grabing!");
            animator.SetTrigger(_grabHash);
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

        public void OnMoveEvent()
        {
            //Debug.Log("Moving!");
           //EventManager.TriggerEvent("OnFootstep", gameObject);
        }
        
        public void OnVaultStartEvent()
        {
            Debug.Log("Vault Start");
            if (cam)
            {
                cam.DoFov(70f);
                int rv = UnityEngine.Random.Range(0, 2) == 0 ? -1 : 1;
                cam.DoTilt(10f * rv);
            }
        }

        public void OnVaultEndEvent()
        {
            Debug.Log("Vault End");
            if (cam)
            {
                cam.DoFov(65f);
                cam.DoTilt(0f);
            }
        }

        public void OnDamageEvent(float damage)
        {
            Debug.Log("Damaged");
            GetDamageVFX(damage);
        }

        private void GetDamageVFX(float damageAmmount)
        {
            Debug.Log("Damage VFX!");
        }
        public void OnSpeedChangeEvent(float speed)
        {
            animator.SetFloat("Speed", speed);

            if (armAnimationHandler != null)
            {
                armAnimationHandler.UpdateSpeed(speed);
            }
        }
       
        public void OnClimbEvent()
        {
            animator.SetTrigger("Climb");
        }
    }
}