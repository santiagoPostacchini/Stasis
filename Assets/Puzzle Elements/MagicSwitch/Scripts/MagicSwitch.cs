using CurvedPathGenerator;
using Player.Scripts.Interactor;
using Puzzle_Elements.Hedron.Scripts;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MagicSwitch : MonoBehaviour
{
    [Header("Ajustes de atracción")]
    public float attractionForce = 10f;
    public float stopDistance = 0.5f;
    public float radius = 1f;

    [Header("Flotación en el centro")]
    private float amplitude = 0.2f;
    private float forceNoise = 2f;
    private float smoothSpeed = 2f;
    public float rotationSpeed = 50f;
    private Vector3 basePosition;
    private Vector3 lastOffset = Vector3.zero;

    private bool _objInCenter;
    private bool _allready = true;
    [SerializeField] private PathFollower _pathFollower;
    public Color _colorFinishPath;
    private ParticleSystem[] particleSystems;
    private Material[] materialInstances;

    [Header("Puerta")]
    public Renderer rightDoorRenderer;
    public Renderer leftDoorRenderer;
    public Renderer frameRenderer;

    private Material rightMatInstance;
    private Material leftMatInstance;
    private Material frameMatInstance;
    [Header("Brillo Puerta")]
    public float brightnessFrequency = 2f;   // Velocidad del brillo
    public float brightnessAmplitude = 0.05f; // >1 para que sea más brillante
    public Color _colorGrow = Color.white;

    private Color baseEmissionRight;
    private Color baseEmissionLeft;
    private bool isShining = false;
    private float shineTimer = 0f;
    private float totalShineTime;

    private bool _allReady = false;

    private void Awake()
    {
        particleSystems = GetComponentsInChildren<ParticleSystem>();
        materialInstances = new Material[particleSystems.Length];

        basePosition = transform.position; // Guardamos la posición del centro

        if (rightDoorRenderer != null)
        {
            rightMatInstance = Instantiate(rightDoorRenderer.sharedMaterial);
            rightDoorRenderer.material = rightMatInstance;
        }

        if (leftDoorRenderer != null)
        {
            leftMatInstance = Instantiate(leftDoorRenderer.sharedMaterial);
            leftDoorRenderer.material = leftMatInstance;
        }
        if (frameRenderer != null)
        {
            frameMatInstance = Instantiate(frameRenderer.sharedMaterial);
            frameRenderer.material = frameMatInstance;
        }
        if (rightMatInstance != null)
        {
            baseEmissionRight = rightMatInstance.GetColor("_EmissionColor");
            if (baseEmissionRight.maxColorComponent <= 0.01f)
                baseEmissionRight = Color.black;
        }

        if (leftMatInstance != null)
        {
            baseEmissionLeft = leftMatInstance.GetColor("_EmissionColor");
            if (baseEmissionLeft.maxColorComponent <= 0.01f)
                baseEmissionLeft = Color.black;
        }
    }

    public void StartShine()
    {
        isShining = true;
        shineTimer = 0f;
        totalShineTime = (Mathf.PI) / brightnessFrequency;
    }

    private void Start()
    {
        for (int i = 0; i < particleSystems.Length; i++)
        {
            var renderer = particleSystems[i].GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                materialInstances[i] = Instantiate(renderer.sharedMaterial);
                renderer.material = materialInstances[i];
            }
        }
    }

    private void Update()
    {
        if (isShining)
        {
            GrowDoor();
        }
    }

    public void GrowDoor()
    {
        shineTimer += Time.deltaTime;

        float t = shineTimer * brightnessFrequency;
        float brightness = (Mathf.Cos(t) + 1f) / 2f;  // 0 a 1

        Color emissionRight = baseEmissionRight + (_colorGrow * brightness * brightnessAmplitude);
        Color emissionLeft = baseEmissionLeft + (_colorGrow * brightness * brightnessAmplitude);

        if (rightMatInstance != null)
        {
            rightMatInstance.EnableKeyword("_EMISSION");
            rightMatInstance.SetColor("_EmissionColor", emissionRight);
        }

        if (leftMatInstance != null)
        {
            leftMatInstance.EnableKeyword("_EMISSION");
            leftMatInstance.SetColor("_EmissionColor", emissionLeft);
        }

        if (shineTimer >= totalShineTime)
        {
            if (rightMatInstance != null)
            {
                rightMatInstance.SetColor("_EmissionColor", baseEmissionRight);
                rightMatInstance.DisableKeyword("_EMISSION");
            }

            if (leftMatInstance != null)
            {
                leftMatInstance.SetColor("_EmissionColor", baseEmissionLeft);
                leftMatInstance.DisableKeyword("_EMISSION");
            }

            isShining = false;
        }
    }

    void FixedUpdate()
    {
        Attraction();
    }

    public void ChangeColor(Color color)
    {
        for (int i = 0; i < materialInstances.Length; i++)
        {
            if (materialInstances[i] != null)
            {
                materialInstances[i].SetColor("_Color", color);
                StartShine();
                _allReady = true;
            }
        }
    }

    private void Attraction()
    {
        Collider[] objs = Physics.OverlapSphere(transform.position, radius);

        foreach (Collider col in objs)
        {
            PhysicsBox hedro = col.GetComponent<PhysicsBox>();
            
            if (hedro == null)
                continue;
            Rigidbody rb = col.attachedRigidbody;
            if (rb == null || rb == GetComponent<Rigidbody>())
                continue;

            Vector3 direction = basePosition - rb.position;
            float distance = direction.magnitude;

            if (distance > stopDistance)
            {
                rb.useGravity = false;
                rb.velocity = Vector3.zero;

                rb.MovePosition(rb.position + direction.normalized * attractionForce * Time.fixedDeltaTime);

                Quaternion rot = Quaternion.Euler(
                    rotationSpeed * Time.fixedDeltaTime,
                    rotationSpeed * 0.3f * Time.fixedDeltaTime,
                    rotationSpeed * 0.2f * Time.fixedDeltaTime
                );
                rb.MoveRotation(rb.rotation * rot);

                _objInCenter = false;
            }
            else
            {
                // Llega al centro: queda quieto y sin gravedad
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.useGravity = false;
                col.enabled = false;
                rb.MovePosition(basePosition);
                rb.MoveRotation(Quaternion.identity); // opcional: resetea rotación
                _objInCenter = true;

                if (_allready && _objInCenter)
                {
                    _pathFollower.IsMove = true;
                    _allready = false;

                    // Brillo final
                    StartShine();
                }
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        PhysicsBox hedro = other.GetComponent<PhysicsBox>();
        Debug.Log("Hedro");
        if (hedro != null && hedro.transform.parent != null)
        {
            if (hedro.transform.parent.parent.TryGetComponent<PlayerInteractor>(out PlayerInteractor player))
            {
                Debug.Log("Player");
                player.TryDropObject();
            }
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
