using UnityEngine;

namespace Player.Scripts.MVC
{
    public class View : MonoBehaviour
    {
        public Transform cameraTransform;
        public CharacterController characterController;
        [SerializeField] private Animator armAnimator;
        private Model _model;

        private readonly int _jumpHash   = Animator.StringToHash("Jump");
        private readonly int _crouchHash = Animator.StringToHash("Crouch");
        private readonly int _landHash   = Animator.StringToHash("Land");
        private readonly int _grabHash   = Animator.StringToHash("Grab");
        private readonly int _dropHash   = Animator.StringToHash("Drop");
        private readonly int _throwHash  = Animator.StringToHash("Throw");
        private readonly int _shotHash   = Animator.StringToHash("Shot");
        private readonly int _idleHash   = Animator.StringToHash("Idle");

        public void Init(Model model)
        {
            this._model = model;

            model.OnJump        += PlayJumpEffect;
            model.OnLand        += PlayLandEffect;
            model.OnCrouchEnter += PlayCrouchEnterEffect;
            model.OnCrouchExit  += PlayCrouchExitEffect;
            model.OnIdle        += PlayIdleEffect;
            model.OnShot        += PlayShotEffect;
            model.OnGrab        += PlayGrabEffect;
            model.OnDrop        += PlayDropEffect;
            model.OnThrow       += PlayThrowEffect;
        }

        public void UpdateCameraHeight(float centerY, float eyeOffset)
        {
            cameraTransform.localPosition = new Vector3(
                cameraTransform.localPosition.x,
                centerY + eyeOffset,
                cameraTransform.localPosition.z
            );
        }

        public void PlayJumpEffect()         => armAnimator.SetTrigger(_jumpHash);
        public void PlayLandEffect()         => armAnimator.SetTrigger(_landHash);
        public void PlayIdleEffect()         => armAnimator.SetTrigger(_idleHash);
        public void PlayShotEffect()         => armAnimator.SetTrigger(_shotHash);
        public void PlayCrouchEnterEffect()  => armAnimator.SetBool(_crouchHash, true);
        public void PlayCrouchExitEffect()   => armAnimator.SetBool(_crouchHash, false);
        public void PlayGrabEffect()         => armAnimator.SetTrigger(_grabHash);
        public void PlayDropEffect()         => armAnimator.SetTrigger(_dropHash);
        public void PlayThrowEffect()        => armAnimator.SetTrigger(_throwHash);
    }
}