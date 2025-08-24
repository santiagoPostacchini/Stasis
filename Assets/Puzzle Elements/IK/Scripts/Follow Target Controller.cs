using UnityEngine;
using Unity.Mathematics;
using System.Collections;
using UnityEngine.Animations.Rigging;

[RequireComponent(typeof(Rigidbody))]
public class FollowTargetController : MonoBehaviour
{
    [Header("Refs")]
    public Transform player;
    public Rig rig;
    public Transform brother;

    [Header("Rig weight por distancia")]
    public float inMin = 2f;
    public float inMax = 5f;
    public float outMin = 0f;
    public float outMax = 1f;
    public AnimationCurve remapLerp = AnimationCurve.Linear(0, 0, 1, 1);

    [Header("Suavizado del weight")]
    public float weightSpeed = 2f; // unidades de weight por segundo

    [Header("Movimiento ChangePosition")]
    public float moveDuration = 1f; // segundos para ir de A a B o viceversa

    [Header("Control")]
    public bool canMove = true;

    // Estado interno
    private Rigidbody rb;
    private Transform startAnchor; // ancla A
    private bool atStart = true;   // estamos en A?
    private Coroutine moveRoutine;

    // Weight interno (suavizado)
    private float targetWeight = 0f;
    private float currentWeight = 0f;

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
        startAnchor.SetPositionAndRotation(rb.position, rb.rotation);
        atStart = true;

        // Inicializar weight
        if (rig != null)
        {
            currentWeight = Mathf.Clamp01(rig.weight);
            targetWeight = currentWeight;
            rig.weight = currentWeight;
        }
    }

    private void Update()
    {
        // Distancia a player (no mueve este objeto)
        if (player != null)
            dist = Vector3.Distance(player.position, rb.position);

        // Pausa total
        if (!canMove) return;

        // Objetivo de weight segun distancia con curva (NO setear directo)
        // Remapeamos dist -> [outMin..outMax], curvamos y clamp a [0..1]
        float raw = math.remap(inMin, inMax, outMin, outMax, dist);
        float curved = remapLerp.Evaluate(raw);
        targetWeight = Mathf.Clamp01(curved);

        // Suavizar el weight hacia el objetivo
        if (rig != null)
        {
            currentWeight = Mathf.MoveTowards(rig.weight, targetWeight, weightSpeed * Time.deltaTime);
            rig.weight = currentWeight;
        }
    }

    // Alterna entre Start (ancla A) y Brother con animacion usando Rigidbody
    public void ChangePosition()
    {
        if (brother == null) return;

        Transform to = atStart ? brother : startAnchor;

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
            // Esperar hasta poder mover
            while (!canMove) yield return null;

            // Recalcular destino por si se movio
            Vector3 segEndPos = to.position;
            Quaternion segEndRot = to.rotation;

            float elapsed = 0f;
            // Avanzar el segmento actual hasta que termine o se pause
            while (elapsed < remaining && canMove)
            {
                // Normal 0..1 sobre el tiempo restante del segmento
                float u = Mathf.Clamp01(elapsed / remaining);
                float k = remapLerp.Evaluate(u);

                // Destino puede cambiar frame a frame
                segEndPos = to.position;
                segEndRot = to.rotation;

                rb.MovePosition(Vector3.LerpUnclamped(segStartPos, segEndPos, k));
                rb.MoveRotation(Quaternion.SlerpUnclamped(segStartRot, segEndRot, k));

                elapsed += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }

            if (!canMove)
            {
                // Pausa: conservar lo restante y arrancar nuevo segmento desde la pose actual
                remaining -= elapsed;
                segStartPos = rb.position;
                segStartRot = rb.rotation;
                continue;
            }

            // Segmento completado
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
