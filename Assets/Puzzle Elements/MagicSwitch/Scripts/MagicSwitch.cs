using CurvedPathGenerator;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MagicSwitch : MonoBehaviour
{
    [Header("Ajustes de atracción")]
    public float attractionForce = 10f;
    public float stopDistance = 0.5f;
    public float radius= 1f;

    [Header("Flotación en el centro")]
    public float amplitude = 0.2f;
    public float forceNoise = 2f;
    public float rotationSpeed = 50f;

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
                baseEmissionRight = Color.black;  // O un color oscuro que prefieras
        }

        if (leftMatInstance != null)
        {
            baseEmissionLeft = leftMatInstance.GetColor("_EmissionColor");
            if (baseEmissionLeft.maxColorComponent <= 0.01f)
                baseEmissionLeft = Color.black;  // O un color oscuro que prefieras
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
                // Clonamos el material para que sea único por cada Particle System de este objeto
                materialInstances[i] = Instantiate(renderer.sharedMaterial);
                renderer.material = materialInstances[i];  // Asignamos la instancia
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
    private void GrowDoor()
    {
        shineTimer += Time.deltaTime;

        float t = shineTimer * brightnessFrequency;
        float brightness = (Mathf.Cos(t) + 1f) / 2f;  // 0 a 1

        // Suma el brillo multiplicado por amplitude al color base para "añadir" luz
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
            Rigidbody rb = col.attachedRigidbody;
            if (rb != null && rb != GetComponent<Rigidbody>())
            {
                Vector3 direction = transform.position - rb.position;
                float distance = direction.magnitude;

                if (distance > stopDistance)
                {
                    // Atraer suavemente hacia el centro
                    if (rb.useGravity)
                    {
                        rb.velocity = Vector3.zero;
                    }
                    rb.useGravity = false;
                    rb.AddForce(direction.normalized * attractionForce, ForceMode.Acceleration);
                }
                else
                {
                    // Flotar y rotar en el centro


                    float offsetX = (Mathf.PerlinNoise(Time.time * forceNoise, 0f) - 0.5f) * 2f * amplitude;
                    float offsetY = (Mathf.PerlinNoise(0f, Time.time * forceNoise) - 0.5f) * 2f * amplitude;
                    float offsetZ = (Mathf.PerlinNoise(Time.time * forceNoise, Time.time * 0.5f) - 0.5f) * 2f * amplitude;

                    Vector3 floatOffset = new Vector3(offsetX, offsetY, offsetZ);
                    rb.MovePosition(transform.position + floatOffset);

                    Quaternion rot = Quaternion.Euler(
                        rotationSpeed * Time.deltaTime,
                        rotationSpeed * 0.5f * Time.deltaTime,
                        rotationSpeed * 0.3f * Time.deltaTime
                    );
                    rb.MoveRotation(rb.rotation * rot);
                    _objInCenter = true;
                    if (_allready && _objInCenter)
                    {
                        _pathFollower.IsMove = true;
                       
                        _allready = false;
                    }
                }
            }
        }
    }
        private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
