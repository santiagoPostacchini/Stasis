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

    [Header("Control")]
    public bool canMove = true;

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

    private Vector3 aPos;
    private Quaternion aRot;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    private void Start()
    {
        aPos = rb.position;
        aRot = rb.rotation;

        startAnchor = new GameObject(name + "_StartAnchor").transform;
        currentTip = startAnchor;
        startAnchor.SetPositionAndRotation(rb.position, rb.rotation);
        atStart = true;

        if (rig != null)
        {
            currentWeight = Mathf.Clamp01(rig.weight);
            targetWeight = currentWeight;
            rig.weight = currentWeight;
        }

        if (player != null)
        {
            Vector3 d = player.position - rb.position;
            if (onlyHorizontalDistance) d.y = 0f;
            distRaw = distFiltered = d.magnitude;
        }
    }

    private void FixedUpdate()
    {
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
        targetWeight = Mathf.Clamp01(curved);

        float desired = targetWeight;
        if (Mathf.Abs(desired - currentWeight) < weightDeadZone)
            desired = currentWeight;

        if (rig != null)
        {
            currentWeight = Mathf.SmoothDamp(currentWeight, desired, ref weightVel, weightSmoothTime, Mathf.Infinity, Time.deltaTime);
            currentWeight = Mathf.Clamp01(currentWeight);
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
        moveRoutine = null;
    }

    private void ApplyPathByWeight()
    {
        Transform dest = currentTip != null ? currentTip : (brother != null ? brother : startAnchor);
        if (dest == null) return;

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
            Vector3 refPos = toB ? (startAnchor != null ? startAnchor.position : destPos) : (brother != null ? brother.position : destPos);
            Quaternion refRot = toB ? (startAnchor != null ? startAnchor.rotation : destRot) : (brother != null ? brother.rotation : destRot);
            midPos = 0.5f * (aPos + refPos);
            midRot = Quaternion.Slerp(aRot, refRot, 0.5f);
        }

        float t = currentWeight;
        t = Mathf.Clamp01(t);

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
