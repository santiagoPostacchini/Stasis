using UnityEngine;

public class FirstPersonCameraBackUp : MonoBehaviour
{
    [Header("<color=green>Target</color>")]
    [SerializeField] private Transform _target;

    [Header("<color=yellow>Camera Settings</color>")]
    [SerializeField] private float _sensitivity = 3f;
    [SerializeField] private float _smoothing = 5f;
    [SerializeField] private float _clampLookDown = -90f;
    [SerializeField] private float _clampLookUp = 75f;

    private float rotationX = 0f; 
    private float rotationY = 0f; 

    private float smoothX;
    private float smoothY;

    void Reset()
    {
        //_target = GetComponentInParent<Movement>().transform;
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }

    void FixedUpdate()
    {
        float mouseX = Input.GetAxis("Mouse X") * _sensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * _sensitivity;

        rotationX -= mouseY; 
        rotationX = Mathf.Clamp(rotationX, _clampLookDown, _clampLookUp); 
        rotationY += mouseX; 

        smoothX = Mathf.Lerp(smoothX, rotationX, 1f / _smoothing);
        smoothY = Mathf.Lerp(smoothY, rotationY, 1f / _smoothing);

        transform.localRotation = Quaternion.Euler(smoothX, 0f, 0f);
        _target.localRotation = Quaternion.Euler(0f, smoothY, 0f);
    }
}
