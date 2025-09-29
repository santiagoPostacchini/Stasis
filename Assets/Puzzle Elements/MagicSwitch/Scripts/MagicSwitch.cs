using CurvedPathGenerator;
using Player.Scripts.Interactor;
using Puzzle_Elements.Hedron.Scripts;
using System.Collections.Generic;
using UnityEngine;

public class MagicSwitch : MonoBehaviour
{
    [Header("Ajustes de atracción")]
    public float stopDistance = 0.5f; // radio de captura (fase 2)
    public float radius = 1f;         // radio total de atracción (fase 1 + 2)

    [Header("Spring-Damper")]
    [SerializeField] private float spring = 40f;          // rigidez hacia el centro
    [SerializeField] private float dampingRatio = 1.1f;   // 1 = crítico; >1 sobreamortiguado
    [SerializeField] private float maxAcceleration = 60f; // límite para evitar picos
    [SerializeField] private float captureDrag = 6f;      // drag extra cuando “flota”
    [SerializeField] private float holdStiffness = 18f;   // rigidez del hold exponencial

    [SerializeField] private LayerMask attractionMask = ~0; // filtra qué puede atraer

    [Header("Flotación en el centro")]
    [SerializeField] private float amplitude = 0.2f;
    [SerializeField] private float forceNoise = 2f;
    [SerializeField] private float smoothSpeed = 2f;
    public float rotationSpeed = 50f;

    private Vector3 basePosition;       // se setea al entrar en captura
    private Vector3 lastOffset = Vector3.zero;

    // Estados internos
    private bool _objInCenter;
    private bool _already = true;

    [SerializeField] private PathFollower _pathFollower;
    public Color _colorFinishPath;

    private ParticleSystem[] particleSystems;
    private Material[] materialInstances;

    // === Puerta / Emisión ===
    [Header("Puerta")]
    public Renderer rightDoorRenderer;
    public Renderer leftDoorRenderer;
    public Renderer frameRenderer;

    private Material rightMatInstance;
    private Material leftMatInstance;
    private Material frameMatInstance;

    [Header("Brillo Puerta")]
    public float brightnessFrequency = 2f;   // velocidad del brillo
    public float brightnessAmplitude = 0.05f; // intensidad extra
    public Color _colorGrow = Color.white;

    private Color baseEmissionRight;
    private Color baseEmissionLeft;
    private bool isShining = false;
    private float shineTimer = 0f;
    private float totalShineTime;

    // Gestión de captura y drags originales
    private readonly HashSet<Rigidbody> _captured = new();
    private readonly Dictionary<Rigidbody, float> _origDrag = new();

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
                baseEmissionRight = Color.black;
        }

        if (leftMatInstance != null)
        {
            baseEmissionLeft = leftMatInstance.GetColor("_EmissionColor");
            if (baseEmissionLeft.maxColorComponent <= 0.01f)
                baseEmissionLeft = Color.black;
        }
    }

    private void Start()
    {
        // Clonar materiales de cada ParticleSystem para tintarlos sin afectar instancias globales
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
            GrowDoor();
    }

    private void FixedUpdate()
    {
        Attraction();
    }

    // === Brillo puerta ===
    public void StartShine()
    {
        isShining = true;
        shineTimer = 0f;
        totalShineTime = (Mathf.PI) / brightnessFrequency;
    }

    public void GrowDoor()
    {
        shineTimer += Time.deltaTime;

        float t = shineTimer * brightnessFrequency;
        float brightness = (Mathf.Cos(t) + 1f) / 2f; // 0..1

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

    // === Tint de partículas + habilitar disparo ===
    public void ChangeColor(Color color)
    {
        for (int i = 0; i < materialInstances.Length; i++)
        {
            if (materialInstances[i] != null)
            {
                materialInstances[i].SetColor("_Color", color);
            }
        }
        StartShine();
        _already = true; // habilita un nuevo disparo si corresponde
    }

    // === Núcleo: Atracción con spring-damper y captura estable ===
    private void Attraction()
    {
        Vector3 center = transform.position;

        // Buscar solo en la capa indicada y sin triggers
        Collider[] objs = Physics.OverlapSphere(center, radius, attractionMask, QueryTriggerInteraction.Ignore);

        // Trackear cuáles están dentro este frame para soltar los que salieron
        var seenThisFrame = new HashSet<Rigidbody>();

        foreach (Collider col in objs)
        {
            PhysicsBox hedro = col.GetComponent<PhysicsBox>();
            if (hedro == null) continue;

            Rigidbody rb = col.attachedRigidbody;
            if (rb == null || rb == GetComponent<Rigidbody>()) continue;

            seenThisFrame.Add(rb);

            Vector3 disp = center - rb.position;
            float dist = disp.magnitude;

            // === FASE 1: ATRACCIÓN CON AMORTIGUACIÓN ===
            if (dist > stopDistance)
            {
                // Si venía capturado, restaurar drag
                if (_captured.Contains(rb))
                {
                    if (_origDrag.TryGetValue(rb, out float d)) rb.drag = d;
                    _origDrag.Remove(rb);
                    _captured.Remove(rb);
                }

                rb.useGravity = false;

                // F = k*x - c*v, con c crítico por masa y ajustado por dampingRatio
                float k = spring;
                float c = 2f * Mathf.Sqrt(k * rb.mass) * dampingRatio;
                Vector3 force = disp * k - rb.velocity * c;

                // Limitar aceleración para estabilidad
                Vector3 accel = force / rb.mass;
                accel = Vector3.ClampMagnitude(accel, maxAcceleration);

                rb.AddForce(accel, ForceMode.Acceleration);
            }
            else
            {
                // === FASE 2: CAPTURA Y FLOTE CONTROLADO ===
                if (!_captured.Contains(rb))
                {
                    if (!_origDrag.ContainsKey(rb)) _origDrag[rb] = rb.drag;
                    rb.drag = captureDrag;
                    _captured.Add(rb);
                }

                rb.useGravity = false;

                // Set basePosition una vez al capturar
                if (basePosition == Vector3.zero) basePosition = center;

                // Bobbing Perlin
                float t = Time.time / smoothSpeed;
                float offsetX = (Mathf.PerlinNoise(t * forceNoise, 0f) - 0.5f) * 2f * amplitude;
                float offsetY = (Mathf.PerlinNoise(0f, t * forceNoise) - 0.5f) * 2f * amplitude;
                float offsetZ = (Mathf.PerlinNoise(t * forceNoise, t * 0.5f / smoothSpeed) - 0.5f) * 2f * amplitude;

                Vector3 targetOffset = new Vector3(offsetX, offsetY, offsetZ);
                lastOffset = Vector3.Lerp(lastOffset, targetOffset, Time.fixedDeltaTime * 6f);

                Vector3 targetPos = basePosition + lastOffset;

                // Hold exponencial hacia el punto objetivo (evita jitter)
                float alpha = 1f - Mathf.Exp(-holdStiffness * Time.fixedDeltaTime);
                Vector3 held = Vector3.Lerp(rb.position, targetPos, alpha);
                rb.MovePosition(held);

                // Rotación suave decorativa
                Quaternion rot = Quaternion.Euler(
                    rotationSpeed * Time.fixedDeltaTime,
                    rotationSpeed * 0.3f * Time.fixedDeltaTime,
                    rotationSpeed * 0.2f * Time.fixedDeltaTime
                );
                rb.MoveRotation(rb.rotation * rot);

                // Apagar energía residual
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;

                // Señal de centro y disparo de PathFollower una sola vez
                _objInCenter = true;
                if (_already && _objInCenter && _pathFollower != null)
                {
                    _pathFollower.IsMove = true;
                    _already = false;

                    // Si quisieras congelarlo totalmente:
                    // rb.isKinematic = true; // y luego lo devolvés a false cuando termine el path
                }
            }
        }

        // Soltar cualquier rigidbody que haya quedado fuera del radio esta iteración
        // (restaura su drag original si estaba capturado)
        if (_captured.Count > 0)
        {
            _toReleaseBuffer.Clear();
            foreach (var r in _captured)
                if (!seenThisFrame.Contains(r))
                    _toReleaseBuffer.Add(r);

            foreach (var r in _toReleaseBuffer)
            {
                if (_origDrag.TryGetValue(r, out float d)) r.drag = d;
                _origDrag.Remove(r);
                _captured.Remove(r);
            }
        }
    }

    // Buffer para liberar capturas sin modificar el set durante el foreach
    private readonly List<Rigidbody> _toReleaseBuffer = new();

    private void OnTriggerEnter(Collider other)
    {
        PhysicsBox hedro = other.GetComponent<PhysicsBox>();
        Debug.Log("Hedro");
        if (hedro != null && hedro.transform.parent != null)
        {
            if (hedro.transform.parent.parent != null &&
                hedro.transform.parent.parent.TryGetComponent<PlayerInteractor>(out PlayerInteractor player))
            {
                Debug.Log("Player");
                player.TryDropObject();
            }
        }
    }

    private void OnDisable()
    {
        // Restaurar drag de cualquier capturado si el objeto se desactiva
        foreach (var kv in _origDrag)
        {
            if (kv.Key != null) kv.Key.drag = kv.Value;
        }
        _origDrag.Clear();
        _captured.Clear();
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, radius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, stopDistance);
    }
}
