using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnimatorController : MonoBehaviour
{
    [Header("<color=red>Dependencies</color>")]
    [SerializeField] private Movement _playerMovement;
    [SerializeField] private Jump _playerJump;
    [SerializeField] private GroundCheck _playerGroundCheck;

    [Header("<color=yellow>Animator</color>")]
    [SerializeField] public Animator _playerAnimator;

    [Header("<color=blue>Animations Transitions Settings</color>")]
    //[SerializeField] private float transitionDuration = 0.2f;

    [Header("<color=green>Animator Parameters</color>")]
    [SerializeField] private float _xAxis;
    [SerializeField] private float _zAxis;
    [SerializeField] private bool _running;
    [SerializeField] private bool _moving;
    [SerializeField] private bool _isJumping;
    [SerializeField] private bool _isDelayJumping;
    [SerializeField] private bool _isGrounded;
    [SerializeField] private bool _isCrouched;
    [SerializeField] private bool _isAttacking;
    [SerializeField] private bool _isPreparingHeavyAttack;
    [SerializeField] private bool _isHoldingHeavyAttack;
    [SerializeField] private bool _isHeavyAttacking;
    [SerializeField] private bool _onJump;

    [Header("<color=orange>Animator Parameters Names</color>")]
    [SerializeField] private string _xAxisName = "xAxis";
    [SerializeField] private string _zAxisName = "zAxis";
    [SerializeField] private string _isMovingName = "isMoving";
    [SerializeField] private string _isJumpingName = "isJumping";
    [SerializeField] private string _isGroundedName = "isGrounded";

    void Update()
    {
        MovementAnimations();
        JumpAnimations();
        GroundCheckAnimations();
    }
    void MovementAnimations()
    {
        _playerAnimator?.SetFloat(_xAxisName, _playerMovement._animX);
        _playerAnimator?.SetFloat(_zAxisName, _playerMovement._animZ);

        _xAxis = _playerMovement._animX;
        _zAxis = _playerMovement._animZ;

        _playerAnimator?.SetBool(_isMovingName, (_playerMovement._xAxis != 0 || _playerMovement._zAxis != 0));
    }

    void JumpAnimations()
    {
        _onJump = _playerJump._onJump;
        _isJumping = _playerJump._isJumping;
        _isDelayJumping = _playerJump._isDelayJumping;

        if (_onJump == true)
        {
            _playerAnimator.SetTrigger(_isJumpingName);
            _playerAnimator.Play("Player_Leg_Jump");
            _playerAnimator.Play("Player_Arm_Jump");
            //_playerAnimator.CrossFade("Player_Leg_Jump", transitionDuration);
        }

    }

    void GroundCheckAnimations()
    {
        _isGrounded = _playerGroundCheck._isGrounded;

        if(_isGrounded == true)
        {
            _playerAnimator.SetBool(_isGroundedName, true);
        }
        else
        {
            _playerAnimator.SetBool(_isGroundedName, false);
        } 
    }

}
