using UnityEngine;
using Unity.Mathematics;
using System.Collections;
using UnityEngine.Animations.Rigging;

[RequireComponent(typeof(Rigidbody))]
public class FollowTargetController : MonoBehaviour
{
    [Header("Refs")]
    public Transform player;          // Solo lectura de posición
    public Rig rig;                   // Peso del rig según distancia
    public Transform brother;         // Punto B

    [Header("Rig weight por distancia")]
    public float inMin = 2f;
    public float inMax = 5f;
    public float outMin = 0f;
    public float outMax = 1f;
    public AnimationCurve remapLerp = AnimationCurve.Linear(0, 0, 1, 1);

    [Header("Movimiento")]
    [Tooltip("Duración (s) del desplazamiento al cambiar de posición")]
    public float moveDuration = 1f;

    [Tooltip("Si es false, no inicia ni continúa desplazamientos")]
    public bool canMove = true;

    // Estado
    private Rigidbody rb;
    private Vector3 startPos;         // Punto A (posición inicial)
    private Quaternion startRot;      // Rotación inicial
    private bool atStart = true;   
    private Coroutine moveRoutine;

    // Expuesto para depurar (solo lectura)
    public float dist { get; private set; }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;                               // Controlado por script
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    private void Start()
    {
        // Ancla A: la pose inicial del rigidbody
        startPos = rb.position;
        startRot = rb.rotation;
        atStart = true;
    }

    private void Update()
    {
        // Si se deshabilita en runtime, detiene todo de inmediato
        if (!canMove && moveRoutine != null)
            CancelMovement();

        // Actualiza el peso del rig según la distancia (lógico que sea en Update)
        if (player != null)
        {
            if (!canMove) return;
            dist = Vector3.Distance(player.position, rb.position);
            float value = math.remap(inMin, inMax, outMin, outMax, dist);
            if (rig != null) rig.weight = remapLerp.Evaluate(value);
        }
    }

    /// <summary>
    /// Alterna entre el punto inicial  y el punto brother .
    /// Si ya hay un movimiento en curso, lo cancela y arranca desde la pose actual.
    /// </summary>
    public void ChangePosition()
    {
        if (!canMove || brother == null) return;

        Vector3 fromPos = rb.position;
        Quaternion fromRot = rb.rotation;

        Vector3 toPos = atStart ? brother.position : startPos;
        Quaternion toRot = atStart ? brother.rotation : startRot;

        // Reinicia movimiento si había uno en curso
        if (moveRoutine != null) StopCoroutine(moveRoutine);
        moveRoutine = StartCoroutine(MoveRB(fromPos, fromRot, toPos, toRot, moveDuration));
    }

    private IEnumerator MoveRB(Vector3 fromPos, Quaternion fromRot, Vector3 toPos, Quaternion toRot, float duration)
    {
        duration = Mathf.Max(0.0001f, duration);
        float t = 0f;

        // Bucle de física
        while (t < duration)
        {
            // Cancelación inmediata si canMove cambia a false
            if (!canMove)
            {
                HardStop();
                yield break;
            }

            float u = t / duration;                 // 0..1 lineal
            float k = remapLerp.Evaluate(u);        // easing temporal

            Vector3 pos = Vector3.LerpUnclamped(fromPos, toPos, k);
            Quaternion rot = Quaternion.SlerpUnclamped(fromRot, toRot, k);

            rb.MovePosition(pos);
            rb.MoveRotation(rot);

            t += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        // Pose final exacta
        rb.MovePosition(toPos);
        rb.MoveRotation(toRot);

        // Solo alternamos A  B cuando realmente LLEGAMOS
        atStart = !atStart;

        moveRoutine = null;
    }

    /// <summary>
    /// Cancela corrutinas y detiene por completo el rigidbody .
    /// </summary>
    private void CancelMovement()
    {
        if (moveRoutine != null)
        {
            StopAllCoroutines();
            moveRoutine = null;
        }
        HardStop();
    }

    /// <summary>
    /// Detiene velocidades y limpia la interpolación para que no se mueva ni un píxel más.
    /// </summary>
    private void HardStop()
    {
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Flush de la interpolación para evitar que siga blendando al último target
        var interp = rb.interpolation;
        rb.interpolation = RigidbodyInterpolation.None;
        // Reafirmamos la pose actual del RB (sin usar transform)
        rb.position = rb.position;
        rb.rotation = rb.rotation;
        rb.interpolation = interp;
    }

    private void OnDisable()
    {
        CancelMovement();
    }
}
