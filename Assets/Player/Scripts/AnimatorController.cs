using Player.FullBody_Scripts;
using UnityEngine;

namespace Player.Scripts
{
    public class AnimatorController : MonoBehaviour
    {
        [Header("<color=red>Dependencies</color>")]
        [SerializeField] private Movement playerMovement;
        [SerializeField] private Jump playerJump;
        [SerializeField] private GroundCheck playerGroundCheck;

        [Header("<color=yellow>Animator</color>")]
        [SerializeField] public Animator playerAnimator;

        [Header("<color=blue>Animations Transitions Settings</color>")]
        //[SerializeField] private float transitionDuration = 0.2f;

        [Header("<color=green>Animator Parameters</color>")]
        [SerializeField] private float xAxis;
        [SerializeField] private float zAxis;
        [SerializeField] private bool running;
        [SerializeField] private bool moving;
        [SerializeField] private bool isJumping;
        [SerializeField] private bool isGrounded;
        [SerializeField] private bool isCrouched;
        [SerializeField] private bool onJump;

        [Header("<color=orange>Animator Parameters Names</color>")]
        [SerializeField] private string xAxisName = "xAxis";
        [SerializeField] private string zAxisName = "zAxis";
        [SerializeField] private string isMovingName = "isMoving";
        [SerializeField] private string isJumpingName = "isJumping";
        [SerializeField] private string isGroundedName = "isGrounded";

        void Update()
        {
            MovementAnimations();
            JumpAnimations();
            GroundCheckAnimations();
        }
        void MovementAnimations()
        {
            playerAnimator?.SetFloat(xAxisName, playerMovement.animX);
            playerAnimator?.SetFloat(zAxisName, playerMovement.animZ);

            xAxis = playerMovement.animX;
            zAxis = playerMovement.animZ;

            playerAnimator?.SetBool(isMovingName, (playerMovement.xAxis != 0 || playerMovement.zAxis != 0));
        }

        void JumpAnimations()
        {
            onJump = playerJump.OnJump;
            isJumping = playerJump.IsJumping;

            if (onJump)
            {
                playerAnimator.SetTrigger(isJumpingName);
                playerAnimator.CrossFade("Player_Leg_Jump", 1);
                playerAnimator.CrossFade("Player_Arm_Jump", 0);
                //_playerAnimator.CrossFade("Player_Leg_Jump", transitionDuration);
            }

        }

        void GroundCheckAnimations()
        {
            playerAnimator.SetBool(isGroundedName, playerGroundCheck.IsGrounded);
        }

    }
}
