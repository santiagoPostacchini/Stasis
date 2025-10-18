using UnityEngine;

[DisallowMultipleComponent]
public class Scannable : MonoBehaviour
{
    public ScanDescriptor data;

    [Header("Pivot / Offset (opcional)")]
    public Transform pivot;
    public Vector3 worldOffset = new Vector3(0, 1.6f, 0);

    [Header("Área de puntería")]
    [Tooltip("Si se deja vacío, se intentan buscar Renderers en hijos.")]
    public Renderer[] targetRenderers;
    [Tooltip("Padding en pantalla (px) alrededor del rect proyectado.")]
    public float screenPadding = 24f;

    [HideInInspector] public ScanLabelUI spawned;

    void OnEnable() => ScannerManager.Register(this);
    void OnDisable() => ScannerManager.Unregister(this);

    void Reset()
    {
        targetRenderers = GetComponentsInChildren<Renderer>();
    }

    public Vector3 WorldPoint =>
        (pivot ? pivot.position : transform.position) + worldOffset;

    public void EnsureLabel(Transform uiRoot, ScanLabelUI prefab)
    {
        if (spawned) return;
        spawned = Instantiate(prefab, uiRoot);
        spawned.Bind(this);
    }

    public void Show(bool instant = false) { if (spawned) spawned.Show(instant); }
    public void Hide(bool instant = false) { if (spawned) spawned.Hide(instant); }
}
