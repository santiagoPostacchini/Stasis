using System.Collections;
using UnityEngine;

public class PlayerBlock : MonoBehaviour
{
    [Header("<color=orange>Animator Override Controller</color>")]
    [SerializeField] private AnimatorOverrideController _overrideController;

    [Header("<color=yellow>Bools</color>")]
    [SerializeField] public bool _blockAnimationsActive = true; 
    [SerializeField] public bool _isBlocking = false;
    [SerializeField] public bool _isPushingBlock = false;
    [SerializeField] public bool _moveStopBool = false;
    [SerializeField] public bool _canPushBlock = true;

    [Header("<color=red>Dependencies</color>")]
    [SerializeField] private Animator _armsAnimator;
    [SerializeField] private Movement _movement;


    [Header("<color=green>Block Settings</color>")]
    [SerializeField] private KeyCode _blockKey = KeyCode.Mouse1;
    [SerializeField] private KeyCode _pushBlockKey = KeyCode.Mouse0;
    [SerializeField] private float _startBlockTime = 0f;
    [SerializeField] private float _holdBlockTime = 0.8f;
    [SerializeField] private float _blockCooldown = 0.5f;
    [SerializeField] private float _pushBlockCooldown = 2f;
    [SerializeField] private float _moveStopCooldown = 1.0f;

    [Header("<color=blue>Animations Transitions Settings</color>")]
    [SerializeField] private float transitionDuration = 0f;

    private float _buttonHoldTime = 0f;
    private bool _isHolding = false;
    private bool _canBlock = true;

    void Start()
    {
        if (_movement == null)
            _movement = GetComponent<Movement>();

    }

    void Update()
    {

        if (!_canBlock || !_blockAnimationsActive) 
            return;

        HandleBlockInput();
        HandlePushBlockInput();
    }

    private void HandleBlockInput()
    {
        if (Input.GetKey(_blockKey))
        {
            _buttonHoldTime += Time.deltaTime;

            if (!_isBlocking && _buttonHoldTime >= _startBlockTime)
            {
                _armsAnimator.CrossFade("Player_Arm_StartBlock", transitionDuration);
                _isBlocking = true;
                _moveStopBool = true;
                _movement._canRun = false;
            }

            if (_isBlocking && !_isHolding && _buttonHoldTime >= _holdBlockTime)
            {
                _armsAnimator.CrossFade("Player_Arm_Block", transitionDuration);
                _isHolding = true;
            }
        }

        if (Input.GetKeyUp(_blockKey))
        {
            HandleEndBlock();
        }
    }

    private void HandlePushBlockInput()
    {
        if (Input.GetKeyDown(_pushBlockKey) && (_isHolding || _isBlocking) && _canPushBlock)
        {
            if (!_armsAnimator.GetCurrentAnimatorStateInfo(0).IsName("Player_Arm_StartBlock"))
            {
                StartPushBlock();
            }
        }

        if (_isPushingBlock && _armsAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f)
        {
            if (Input.GetKey(_blockKey))
            {
                _armsAnimator.CrossFade("Player_Arm_Block", transitionDuration);
                _isHolding = true;
            }
            else
            {
                _armsAnimator.CrossFade("Player_Arm_EndBlock", transitionDuration);
                StartCoroutine(BlockCooldown());
                StartCoroutine(MoveStopCooldown());
                _isHolding = false;
            }

            _isPushingBlock = false;
            _buttonHoldTime = 0f;
        }
    }

    private void HandleEndBlock()
    {
        if (_isBlocking && !_isHolding)
        {
            if (_armsAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f)
            {
                StartCoroutine(WaitForStartBlockAnimationToEnd());
            }
            else
            {
                _armsAnimator.CrossFade("Player_Arm_EndBlock", transitionDuration);
                StartCoroutine(BlockCooldown());
                StartCoroutine(MoveStopCooldown());
            }

            _isHolding = false;
            _buttonHoldTime = 0f;
        }
        else if (_isHolding && !_isPushingBlock)
        {
            _armsAnimator.CrossFade("Player_Arm_EndBlock", transitionDuration);
            StartCoroutine(BlockCooldown());
            StartCoroutine(MoveStopCooldown());
            _isHolding = false;
            _buttonHoldTime = 0f;
        }
    }

    private void StartPushBlock()
    {
        if (!_isPushingBlock)
        {
            _isPushingBlock = true;
            _armsAnimator.CrossFade("Player_Arm_BlockPush", 0f);
            _movement._canRun = false;
            StartCoroutine(PushBlockCooldown());
        }
    }

    private IEnumerator WaitForStartBlockAnimationToEnd()
    {
        while (_armsAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f)
            yield return null;

        _armsAnimator.CrossFade("Player_Arm_EndBlock", transitionDuration);
        StartCoroutine(BlockCooldown());
        StartCoroutine(MoveStopCooldown());
    }

    private IEnumerator BlockCooldown()
    {
        _canBlock = false;
        yield return new WaitForSeconds(_blockCooldown);
        _canBlock = true;
        _isBlocking = false;
        _movement._canRun = true;
    }

    private IEnumerator PushBlockCooldown()
    {
        _canPushBlock = false;
        yield return new WaitForSeconds(_pushBlockCooldown);
        _canPushBlock = true;
    }

    private IEnumerator MoveStopCooldown()
    {
        yield return new WaitForSeconds(_moveStopCooldown);
        _moveStopBool = false;
    }

    public bool IsBlocking()
    {
        return _isBlocking;
    }

    public bool MoveStopBool()
    {
        return _moveStopBool;
    }
}
