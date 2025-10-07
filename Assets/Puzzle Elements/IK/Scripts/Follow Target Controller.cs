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
    public Transform mid1;
    public Transform mid2;

    [Header("Rig weight por distancia")]
    public float inMin = 2f;
    public float inMax = 5f;
    public float outMin = 0f;
    public float outMax = 1f;
    public AnimationCurve remapLerp = AnimationCurve.Linear(0, 0, 1, 1);

    [Header("Estabilidad del cálculo")]
    public bool onlyHorizontalDistance = true;
    public float distanceSmoothHz = 12f;
    public float distanceDeadZone = 0.03f;
    public float weightDeadZone = 0.01f;
    public float weightSmoothTime = 0.15f;

    [Header("Movimiento ChangePosition")]
    public float moveDuration = 1f;

    [Header("Seguridad de movimiento")]
    public float maxMoveSpeed = 5f;

    [Header("Control")]
    public bool canMove = true;

    [Header("Brother settings")]
    [Range(0f, 1f)] public float brotherMinWeight = 0.6f;
    public float brotherApproachSmooth = 0.2f;

    [Header("Frame de referencia (toggle para comparar)")]
    [Tooltip("ON: anclas y Punto A viven en el marco local (tren). OFF: en mundo (comportamiento anterior).")]
    public bool anchorInParentFrame = true;
    [Tooltip("Opcional: marco explícito. Si es null usa transform.parent (o este transform si no hay padre).")]
    public Transform frame;

    private Rigidbody rb;
    private Transform startAnchor;
    public Transform currentTip;
    private bool atStart = true;
    private Coroutine moveRoutine;

    private float targetWeight = 0f;
    private float currentWeight = 0f;
    private float weightVel;

    private float distRaw;
    private float distFiltered;
    public float dist { get; private set; }

    private Vector3 aPosWorld;
    private Quaternion aRotWorld;
    private Vector3 aPosLocal;
    private Quaternion aRotLocal;

    private float brotherCurrentMin = 0f;

    private Transform GetFrame()
    {
        if (frame) return frame;
        if (transform.parent) return transform.parent;
        return transform;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
    }

    private void Start()
    {
        if (GameManager.Instance != null && player == null)
            player = GameManager.Instance.player;

        startAnchor = new GameObject(name + "_StartAnchor").transform;

        if (anchorInParentFrame)
        {
            Transform f = GetFrame();
            startAnchor.SetParent(f, true);
            startAnchor.SetPositionAndRotation(transform.position, transform.rotation);

            aPosLocal = f.InverseTransformPoint(transform.position);
            aRotLocal = Quaternion.Inverse(f.rotation) * transform.rotation;

            rb.interpolation = RigidbodyInterpolation.None;
        }
        else
        {
            startAnchor.SetParent(null, true);
            startAnchor.SetPositionAndRotation(transform.position, transform.rotation);

            aPosWorld = transform.position;
            aRotWorld = transform.rotation;

            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        currentTip = startAnchor;
        atStart = true;

        if (rig != null)
        {
            currentWeight = Mathf.Clamp01(rig.weight);
            if (currentWeight > 0.95f) currentWeight = 1f; // snap a 1
            targetWeight = currentWeight;
            rig.weight = currentWeight;
        }

        if (player != null)
        {
            Vector3 d = player.position - transform.position;
            if (onlyHorizontalDistance) d.y = 0f;
            distRaw = distFiltered = d.magnitude;
        }
    }

    private void FixedUpdate()
    {
        if (anchorInParentFrame)
        {
            rb.position = transform.position;
            rb.rotation = transform.rotation;
        }

        if (player != null)
        {
            Vector3 delta = player.position - rb.position;
            if (onlyHorizontalDistance) delta.y = 0f;

            float newDist = delta.magnitude;

            if (Mathf.Abs(newDist - distFiltered) > distanceDeadZone)
                distRaw = newDist;

            if (distanceSmoothHz > 0f)
            {
                float alpha = 1f - Mathf.Exp(-distanceSmoothHz * Time.deltaTime);
                distFiltered = Mathf.Lerp(distFiltered, distRaw, alpha);
            }
            else
            {
                distFiltered = distRaw;
            }

            dist = distFiltered;
        }

        if (!canMove) return;

        float raw = math.remap(inMin, inMax, outMin, outMax, distFiltered);
        float curved = remapLerp.Evaluate(raw);

        if (currentTip == startAnchor)
        {
            targetWeight = Mathf.Clamp01(curved);
            brotherCurrentMin = 0f;
        }
        else if (currentTip == brother)
        {
            brotherCurrentMin = Mathf.MoveTowards(
                brotherCurrentMin,
                brotherMinWeight,
                Time.fixedDeltaTime / Mathf.Max(0.0001f, brotherApproachSmooth)
            );

            targetWeight = Mathf.Max(curved, brotherCurrentMin);
        }
        else
        {
            targetWeight = Mathf.Clamp01(curved);
            brotherCurrentMin = 0f;
        }

        float desired = targetWeight;
        if (Mathf.Approximately(desired, currentWeight) || Mathf.Abs(desired - currentWeight) < weightDeadZone)
            desired = currentWeight;

        if (rig != null)
        {
            currentWeight = Mathf.SmoothDamp(currentWeight, desired, ref weightVel, weightSmoothTime, Mathf.Infinity, Time.deltaTime);
            currentWeight = Mathf.Clamp01(currentWeight);
            if (currentWeight > 0.95f) currentWeight = 1f; // snap a 1
            rig.weight = currentWeight;
        }

        ApplyPathByWeight();
    }

    public void ResetObject()
    {
        atStart = false;
        ChangePosition();
    }

    public void ChangePosition()
    {
        if (brother == null) return;

        Transform to = atStart ? brother : startAnchor;
        currentTip = to;

        if (moveRoutine != null) StopCoroutine(moveRoutine);
        moveRoutine = StartCoroutine(MoveRB_Pausable(to, moveDuration));
    }

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

        if (anchorInParentFrame)
        {
            Transform f = GetFrame();
            aPosLocal = f.InverseTransformPoint(rb.position);
            aRotLocal = Quaternion.Inverse(f.rotation) * rb.rotation;
        }
        else
        {
            aPosWorld = rb.position;
            aRotWorld = rb.rotation;
        }

        moveRoutine = null;
    }

    private void ApplyPathByWeight()
    {
        Transform dest = currentTip != null ? currentTip : (brother != null ? brother : startAnchor);
        if (dest == null) return;

        Vector3 aPos;
        Quaternion aRot;
        if (anchorInParentFrame)
        {
            Transform f = GetFrame();
            aPos = f.TransformPoint(aPosLocal);
            aRot = f.rotation * aRotLocal;
        }
        else
        {
            aPos = aPosWorld;
            aRot = aRotWorld;
        }

        Vector3 destPos = dest.position;
        Quaternion destRot = dest.rotation;

        bool toB = (dest == startAnchor);
        Transform midT = toB ? mid1 : mid2;

        Vector3 midPos;
        Quaternion midRot;

        if (midT != null)
        {
            midPos = midT.position;
            midRot = midT.rotation;
        }
        else
        {
            Vector3 refPos = toB ? startAnchor.position : (brother != null ? brother.position : destPos);
            Quaternion refRot = toB ? startAnchor.rotation : (brother != null ? brother.rotation : destRot);
            midPos = 0.5f * (aPos + refPos);
            midRot = Quaternion.Slerp(aRot, refRot, 0.5f);
        }

        float t = Mathf.Clamp01(currentWeight);

        Vector3 p;
        Quaternion r;

        if (t <= 0.5f)
        {
            float u = t * 2f;
            p = Vector3.LerpUnclamped(aPos, midPos, u);
            r = Quaternion.SlerpUnclamped(aRot, midRot, u);
        }
        else
        {
            float u = (t - 0.5f) * 2f;
            p = Vector3.LerpUnclamped(midPos, destPos, u);
            r = Quaternion.SlerpUnclamped(midRot, destRot, u);
        }

        Vector3 desiredMove = p - rb.position;
        float maxStep = maxMoveSpeed * Time.fixedDeltaTime;

        if (desiredMove.magnitude > maxStep)
        {
            desiredMove = desiredMove.normalized * maxStep;
            p = rb.position + desiredMove;
        }

        rb.MovePosition(p);
        rb.MoveRotation(r);
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
