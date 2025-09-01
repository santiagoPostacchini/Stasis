using UnityEngine;
using System.Collections;

public class Jump : MonoBehaviour
{
    [Header("<color=red>Dependencies</color>")]
    [SerializeField] private GroundCheck _playerGroundCheck;
    
    [Header("<color=yellow>Jump Settings</color>")]
    [SerializeField] private KeyCode _jumpingKey = KeyCode.Space;
    [SerializeField] private float _jumpStrength = 2.5f;
    [SerializeField] private float _jumpDelay = 0.3f;
    [SerializeField] private float _onJumpDuration = 0.1f;
    [SerializeField] public bool _canJump;
    [SerializeField] public bool _isJumping;
    [SerializeField] public bool _isDelayJumping;
    [SerializeField] public bool _onJump;

    public event System.Action Jumped;
    new Rigidbody rigidbody;

    void Reset()
    {
        _playerGroundCheck = GetComponentInChildren<GroundCheck>();
    }

    void Awake()
    {
        rigidbody = GetComponent<Rigidbody>();
        _canJump = true;
        _playerGroundCheck._grounded += OnLanding;
    }

    void LateUpdate()
    {
        if (Input.GetKeyDown(_jumpingKey) && _playerGroundCheck._isGrounded && _canJump == true)
        {
            _isJumping = true;
            _onJump = true;

            StartCoroutine(JumpWithDelay());
            StartCoroutine(ResetOnJump());
        }
    }

    private IEnumerator JumpWithDelay()
    {
        yield return new WaitForSeconds(_jumpDelay);

        GetComponent<Rigidbody>().AddForce(Vector3.up * 100 * _jumpStrength);
        Jumped?.Invoke();
  
        _isDelayJumping = true;
    }

    private IEnumerator ResetOnJump()
    {
        yield return new WaitForSeconds(_onJumpDuration);
        _onJump = false;
    }

    private void OnLanding()
    {
        _isJumping = false;
        _isDelayJumping = false;
    }

    void OnDestroy()
    {
        _playerGroundCheck._grounded -= OnLanding;
    }
}



