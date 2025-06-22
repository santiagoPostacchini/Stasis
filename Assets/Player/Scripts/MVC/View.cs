using Player.Camera;
using UnityEngine;

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
        
        public Animator animator; //hay que modificar el animator para utilizar estos nuevos valores 
        public AudioSource audioSource;
        public PlayerCam cam;
        
        public Material damageMaterialPostProcess;

        public void OnJumpEvent()
        {
            Debug.Log("Jumping!");
            animator.SetTrigger(_jumpHash);
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
        }
        
        public void OnVaultStartEvent()
        {
            Debug.Log("Vault Start");
            if (cam)
            {
                cam.DoFov(70f);
                cam.DoTilt(10f);
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
    }
}