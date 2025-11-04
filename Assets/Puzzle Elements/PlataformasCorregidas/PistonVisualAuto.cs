using UnityEngine;

[DisallowMultipleComponent]
public class PistonVisualAuto : MonoBehaviour
{
    public enum Axis { X, Y, Z }

    [Header("Anclajes")]
    public Transform baseAnchor;
    public Transform platformTopAnchor;

    [Header("Piezas visuales")]
    public Transform rodTube;
    public Transform headCap;

    [Header("Alineación y escala")]
    public Axis stretchAxis = Axis.Y;
    public float minRodLength = 0.1f;
    public float lengthOffset = 0.0f;
    public bool keepVertical = true;

    Vector3 _baseScale = Vector3.one;

    [SerializeField] private ElevatorShipmentTrain _elevatorShipmentTrain;
    void Awake()
    {
        if (rodTube) _baseScale = rodTube.localScale;
        _elevatorShipmentTrain = GetComponentInParent<ElevatorShipmentTrain>();
    }
    void FixedUpdate()
    {
        if (!baseAnchor || !platformTopAnchor || !rodTube || _elevatorShipmentTrain.IsFreezed || !_elevatorShipmentTrain.canMove) return;

        Vector3 a = baseAnchor.position;
        Vector3 b = platformTopAnchor.position;
        Vector3 dir = b - a;
        float dist = dir.magnitude;
        if (dist < 1e-5f) dist = 0f;

        if (!keepVertical && dist > 1e-5f) rodTube.up = dir.normalized;
        else rodTube.up = Vector3.up;

        rodTube.position = a + dir * 0.5f;

        Vector3 s = _baseScale;
        float L = Mathf.Max(minRodLength, dist + lengthOffset);
        switch (stretchAxis)
        {
            case Axis.X: s.x = L; break;
            case Axis.Y: s.y = L; break;
            case Axis.Z: s.z = L; break;
        }
        rodTube.localScale = s;

        if (headCap)
        {
            headCap.position = b;
            headCap.up = (!keepVertical && dist > 1e-5f) ? dir.normalized : Vector3.up;
        }
    }
}
