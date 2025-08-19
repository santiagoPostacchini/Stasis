using UnityEngine;
using Unity.Mathematics;
using System.Threading.Tasks;
using UnityEngine.Animations.Rigging;

public class FollowTargetController : MonoBehaviour
{
    public Transform player;
    public Rig rig;
    public Transform brother;
    Transform startTransform;

    public float dist;
    public float inMin = 2f;
    public float inMax = 5f;
    public float outMin = 0f;
    public float outMax = 1f;
    public AnimationCurve remapLerp;

    private void Start()
    {
        startTransform = new GameObject().transform;
        startTransform.position = transform.position;
        startTransform.rotation = transform.rotation;
    }

    private void Update()
    {
        dist = Vector3.Distance(player.position, transform.position);
        float value = math.remap(inMin, inMax, outMin, outMax, dist);
        rig.weight = remapLerp.Evaluate(value);
    }

    public void ChangePosition()
    {
        if(transform.position == startTransform.position)
        {
            SomeAsyncMethod(startTransform, brother);
        }
        else
        {
            SomeAsyncMethod(brother, startTransform);
        }
    }
    async Task SomeAsyncMethod(Transform from, Transform to)
    {
        for (float time = 0; time < 1; time += Time.deltaTime)
        {
            transform.position = Vector3.Lerp(from.position, to.position, remapLerp.Evaluate(time));
            transform.rotation = Quaternion.Lerp(from.rotation, to.rotation, remapLerp.Evaluate(time));
            await Task.Yield();

        }
        transform.position = Vector3.Lerp(from.position, to.position, remapLerp.Evaluate(1));
        transform.rotation = Quaternion.Lerp(from.rotation, to.rotation, remapLerp.Evaluate(1));
    }
}
