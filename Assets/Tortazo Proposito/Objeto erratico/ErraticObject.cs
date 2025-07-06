using UnityEngine;
using System.Collections;

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

    [SerializeField] private float angleStep = 90f;
    [SerializeField] private float angleThreshold = 1f;
    [SerializeField] private float pauseDuration = 1f;

    private bool isPaused = false;
    private Quaternion lastRotation;
    private float totalRotation = 0f;
    private float nextStopAngle = 90f; // primer corte

    private void Start()
    {
        rb = GetComponent<Rigidbody>();

        if (waypoints.Length < 2)
        {
            Debug.LogError("Necesitás al menos 2 puntos para el movimiento errático.");
            enabled = false;
            return;
        }

        rb.mass = 400;
        rb.angularDrag = 200;
        rb.drag = 40;

        currentTargetIndex = 0;
        ChooseNewTarget();

        lastRotation = rb.rotation;
        totalRotation = 0f;
        nextStopAngle = angleStep;
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
        if (isPaused) return;

        // Aplicar rotación
        Quaternion deltaRotation = Quaternion.Euler(rotationAxis.normalized * rotationSpeed * Time.fixedDeltaTime);
        rb.MoveRotation(rb.rotation * deltaRotation);

        // Calcular cuánto rotó en este frame
        float angleThisFrame = Quaternion.Angle(lastRotation, rb.rotation);
        totalRotation += angleThisFrame;
        lastRotation = rb.rotation;

        // ¿Pasamos el ángulo de corte?
        if (totalRotation >= nextStopAngle - angleThreshold)
        {
            StartCoroutine(PauseRotation());

            // Preparamos el próximo ángulo de corte (180°, 270°, etc.)
            nextStopAngle += angleStep;
        }
    }

    private IEnumerator PauseRotation()
    {
        isPaused = true;
        yield return new WaitForSeconds(pauseDuration);
        isPaused = false;
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