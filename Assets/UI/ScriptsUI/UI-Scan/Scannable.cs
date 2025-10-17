using UnityEngine;

[DisallowMultipleComponent]
public class Scannable : MonoBehaviour
{
    public ScanDescriptor data;
    [Tooltip("Punto desde donde 'sale' el cartel (opcional)")]
    public Transform pivot;
    [Tooltip("Offset desde el pivot/transform")]
    public Vector3 worldOffset = new Vector3(0, 1.6f, 0);

    [HideInInspector] public ScanLabelUI spawned;

    void OnEnable()  => ScannerManager.Register(this);
    void OnDisable() => ScannerManager.Unregister(this);

    public Vector3 WorldPoint =>
        (pivot ? pivot.position : transform.position) + worldOffset;

    public void EnsureLabel(Transform uiRoot, ScanLabelUI prefab)
    {
        if (spawned != null) return;
        spawned = Instantiate(prefab, uiRoot);
        spawned.Bind(this);
    }

    public void Show(bool instant = false) { if (spawned) spawned.Show(instant); }
    public void Hide(bool instant = false) { if (spawned) spawned.Hide(instant); }
}
