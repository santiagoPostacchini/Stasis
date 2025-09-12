using UnityEngine;
using Unity.Mathematics;
using System.Collections;
using UnityEngine.Animations.Rigging;

[RequireComponent(typeof(Rigidbody))]
public class FollowTargetController : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("Referencia al Player; solo se usa para medir la distancia.")]
    public Transform player;

    [Tooltip("Rig que controla el peso del IK (0..1).")]
    public Rig rig;

    [Tooltip("Destino alternativo para ChangePosition (punto B).")]
    public Transform brother;

    [Header("Rig weight por distancia")]
    [Tooltip("Distancia mínima desde la cual empieza el mapeo del peso.")]
    public float inMin = 2f;

    [Tooltip("Distancia máxima para el mapeo del peso.")]
    public float inMax = 5f;

    [Tooltip("Salida mínima del mapeo previo a la curva (antes de remapLerp).")]
    public float outMin = 0f;

    [Tooltip("Salida máxima del mapeo previo a la curva (antes de remapLerp).")]
    public float outMax = 1f;

    [Tooltip("Curva de easing para transformar la distancia en el peso del Rig.")]
    public AnimationCurve remapLerp = AnimationCurve.Linear(0, 0, 1, 1);

    [Header("Estabilidad del cálculo")]
    [Tooltip("Ignorar componente vertical al medir distancia (reduce jitter por bob/animaciones).")]
    public bool onlyHorizontalDistance = true;

    [Tooltip("Filtro exponencial de distancia (Hz). 0 = sin filtro, 10..20 = suave útil.")]
    public float distanceSmoothHz = 12f;

    [Tooltip("Ignorar cambios de distancia menores a este umbral (metros).")]
    public float distanceDeadZone = 0.03f;

    [Tooltip("Ignorar cambios de weight menores a este umbral (absoluto 0..1).")]
    public float weightDeadZone = 0.01f;

    [Tooltip("Tiempo de alisado del weight (segundos) usando SmoothDamp.")]
    public float weightSmoothTime = 0.15f;

    [Header("Movimiento ChangePosition")]
    [Tooltip("Duración (segundos) del movimiento entre Start (ancla A) y Brother (punto B).")]
    public float moveDuration = 1f;

    [Header("Control")]
    [Tooltip("Si es false, no se actualiza el Rig ni avanza el movimiento.")]
    public bool canMove = true;

    // Estado interno
    private Rigidbody rb;
    private Transform startAnchor; // ancla A
    public Transform currentTip;
    private bool atStart = true;   // estamos en A?
    private Coroutine moveRoutine;

    // Weight interno (suavizado)
    private float targetWeight = 0f;
    private float currentWeight = 0f;
    private float weightVel;       // para SmoothDamp

    // Distancias filtradas
    private float distRaw;
    private float distFiltered;

    // Debug solo lectura
    public float dist { get; private set; }

    

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
       
    }

    private void Start()
    {
        // Crear ancla A en la pose inicial del RB
        startAnchor = new GameObject(name + "_StartAnchor").transform;
        currentTip = startAnchor;
        startAnchor.SetPositionAndRotation(rb.position, rb.rotation);
        atStart = true;

       

        // Inicializar weight
        if (rig != null)
        {
            currentWeight = Mathf.Clamp01(rig.weight);
            targetWeight = currentWeight;
            rig.weight = currentWeight;
        }

        // Inicializar filtro de distancia
        if (player != null)
        {
            Vector3 d = player.position - rb.position;
            if (onlyHorizontalDistance) d.y = 0f;
            distRaw = distFiltered = d.magnitude;
        }
    }

    private void Update()
    {
        // Distancia al player (cruda y filtrada)
        if (player != null)
        {
            Vector3 delta = player.position - rb.position;
            if (onlyHorizontalDistance) delta.y = 0f;

            float newDist = delta.magnitude;

            // Deadzone en distancia
            if (Mathf.Abs(newDist - distFiltered) > distanceDeadZone)
                distRaw = newDist; // solo aceptamos cambios significativos

            // Filtro exponencial (frecuencia en Hz)
            if (distanceSmoothHz > 0f)
            {
                float alpha = 1f - Mathf.Exp(-distanceSmoothHz * Time.deltaTime);
                distFiltered = Mathf.Lerp(distFiltered, distRaw, alpha);
            }
            else
            {
                distFiltered = distRaw;
            }

            dist = distFiltered; // expuesto para debug
        }

        // Pausa total
        if (!canMove) return;

        // Objetivo de weight segun distancia filtrada
        float raw = math.remap(inMin, inMax, outMin, outMax, distFiltered);
        float curved = remapLerp.Evaluate(raw);
        targetWeight = Mathf.Clamp01(curved);

        // Deadzone en weight
        float desired = targetWeight;
        if (Mathf.Abs(desired - currentWeight) < weightDeadZone)
            desired = currentWeight;

        // Suavizar con SmoothDamp (más estable que MoveTowards ante jitter)
        if (rig != null)
        {
            currentWeight = Mathf.SmoothDamp(currentWeight, desired, ref weightVel, weightSmoothTime, Mathf.Infinity, Time.deltaTime);
            currentWeight = Mathf.Clamp01(currentWeight);
            rig.weight = currentWeight;
        }
    }

    // Alterna entre Start (ancla A) y Brother con animacion usando Rigidbody
    public void ChangePosition()
    {
        if (brother == null) return;

        Transform to = atStart ? brother : startAnchor;

        currentTip = to;

        if (moveRoutine != null) StopCoroutine(moveRoutine);
        moveRoutine = StartCoroutine(MoveRB_Pausable(to, moveDuration));
    }
   

    // Corrutina pausable: no teletransporta al reactivar canMove
    private IEnumerator MoveRB_Pausable(Transform to, float totalDuration)
    {
        totalDuration = Mathf.Max(0.0001f, totalDuration);
        float remaining = totalDuration;

        Vector3 segStartPos = rb.position;
        Quaternion segStartRot = rb.rotation;

        while (remaining > 0f)
        {
            while (!canMove) yield return null;

            Vector3 segEndPos = to.position;
            Quaternion segEndRot = to.rotation;

            float elapsed = 0f;
            while (elapsed < remaining && canMove)
            {
                float u = Mathf.Clamp01(elapsed / remaining);
                float k = remapLerp.Evaluate(u);

                // Destino puede moverse
                segEndPos = to.position;
                segEndRot = to.rotation;

                rb.MovePosition(Vector3.LerpUnclamped(segStartPos, segEndPos, k));
                rb.MoveRotation(Quaternion.SlerpUnclamped(segStartRot, segEndRot, k));

                elapsed += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }

            if (!canMove)
            {
                remaining -= elapsed;
                segStartPos = rb.position;
                segStartRot = rb.rotation;
                continue;
            }

            rb.MovePosition(segEndPos);
            rb.MoveRotation(segEndRot);
            remaining = 0f;
        }

        atStart = (to == startAnchor);
        moveRoutine = null;
    }

    private void OnDisable()
    {
        if (moveRoutine != null)
        {
            StopCoroutine(moveRoutine);
            moveRoutine = null;
        }
    }
}
