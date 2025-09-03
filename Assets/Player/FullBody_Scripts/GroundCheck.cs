using UnityEngine;

[ExecuteInEditMode]
public class GroundCheck : MonoBehaviour
{
    [Header("<color=orange>GroundCheck Settings</color>")]
    [SerializeField] private float distanceThreshold = 0.15f; 
    [SerializeField] private float groundCheckDuration = 0.2f; 
    [SerializeField] public bool _isGrounded = true;
    
    private float groundCheckTimeRemaining = 0f; 
    private bool _wasGroundedLastFrame = false; 
    public event System.Action _grounded;

    const float OriginOffset = 0.001f; 
    Vector3 RaycastOrigin => transform.position + Vector3.up * OriginOffset;
    float RaycastDistance => distanceThreshold + OriginOffset;

    void Update()
    {
        bool _isGroundedNow = Physics.Raycast(RaycastOrigin, Vector3.down, distanceThreshold * 2);

        if (_isGroundedNow)
        {
            if (!_wasGroundedLastFrame)
            {
                groundCheckTimeRemaining = groundCheckDuration; 
            }
            _wasGroundedLastFrame = true;
        }
        else
        {
            if (groundCheckTimeRemaining > 0f)
            {
                groundCheckTimeRemaining -= Time.deltaTime; 
            }
            else
            {
                _wasGroundedLastFrame = false; 
            }
        }

        if (groundCheckTimeRemaining > 0f)
        {
            _isGrounded = true;
        }
        else
        {
            _isGrounded = false;
        }

        if (_isGrounded && !_wasGroundedLastFrame)
        {
            _grounded?.Invoke();
        }
    }

    void OnDrawGizmosSelected()
    {
        Debug.DrawLine(RaycastOrigin, RaycastOrigin + Vector3.down * RaycastDistance, _isGrounded ? Color.white : Color.red);
    }
}

