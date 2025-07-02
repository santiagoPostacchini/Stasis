using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ErraticObject : MonoBehaviour
{
    [Header("Waypoints")]
    public Transform[] waypoints;

    [Header("Movement Settings")]
    public float speed = 3f;
    public float randomness = 0.5f;
    public float switchTime = 2f;

    [Header("Rotation Settings")]
    public Vector3 rotationAxis = new Vector3(0, 0, 1);
    public float rotationSpeed = 90f;

    [Header("Frenetic Fall Settings")]
    public float fallSpeedMultiplier = 2.5f;
    public Transform pos;

    public bool isFreezed;
    private bool isFalling = false;

    private int currentTargetIndex;
    private float timer;

    public Rigidbody rb;

    private Vector3 lastDirection;
    private Vector3 currentRandomOffset;

    private Quaternion originalRotation;
    [SerializeField] private bool canRotate;
    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        originalRotation = transform.rotation;
        if (waypoints.Length < 2)
        {
            Debug.LogError("Necesitás al menos 2 puntos para el movimiento errático.");
            enabled = false;
            return;
        }

        currentTargetIndex = 0;
        ChooseNewTarget();
    }
    private void LateUpdate()
    {
        if (!canRotate)
        {
            if (Quaternion.Angle(transform.rotation, originalRotation) > 0.1f)
            {
                transform.rotation = originalRotation;
            }
        }
       
    }
    private void FixedUpdate()
    {
        if (isFreezed) return;

        timer += Time.fixedDeltaTime;

        MoveTowardsCurrentTarget();

        if (timer >= switchTime)
        {
            SwitchTarget();
            timer = 0f;
        }

        ApplyContinuousRotation();
    }

    private void MoveTowardsCurrentTarget()
    {
        Vector3 targetPosition = GetTargetWithRandomness();
        Vector3 moveDirection = (targetPosition - rb.position).normalized;
        lastDirection = moveDirection;

        Vector3 movement = moveDirection * speed * Time.fixedDeltaTime;
        rb.MovePosition(rb.position + movement);
    }

    private void ApplyContinuousRotation()
    {
        Quaternion deltaRotation = Quaternion.Euler(rotationAxis * rotationSpeed * Time.fixedDeltaTime);
        rb.MoveRotation(rb.rotation * deltaRotation);
    }

    private Vector3 GetTargetWithRandomness()
    {
        return waypoints[currentTargetIndex].position + currentRandomOffset;
    }

    private void ChooseNewTarget()
    {
        currentRandomOffset = new Vector3(
            Random.Range(-randomness, randomness),
            Random.Range(-randomness, randomness),
            Random.Range(-randomness, randomness)
        );
    }

    private void SwitchTarget()
    {
        currentTargetIndex = (currentTargetIndex + 1) % waypoints.Length;
        ChooseNewTarget();
    }
}