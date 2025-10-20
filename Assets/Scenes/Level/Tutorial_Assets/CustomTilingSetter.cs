using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class CustomTilingSetter : MonoBehaviour
{
    [Header("Tiling Settings")]
    [Tooltip("Controla el tiling del Albedo y Normal Map.")]
    public Vector2 tiling = Vector2.one;

    [Header("Ambient Occlusion")]
    [Tooltip("Textura AO única por objeto.")]
    public Texture2D aoTexture;

    private Renderer rend;
    private MaterialPropertyBlock block;

    void Awake()
    {
        rend = GetComponent<Renderer>();
        block = new MaterialPropertyBlock();
        ApplyProperties();
    }

    void OnEnable()
    {
        ApplyProperties();
    }

    void OnValidate()
    {
        if (rend == null) rend = GetComponent<Renderer>();
        if (block == null) block = new MaterialPropertyBlock();
        ApplyProperties();
    }

    void ApplyProperties()
    {
        rend.GetPropertyBlock(block);

        // Tiling para Albedo + Normal
        block.SetVector("_Tiling", tiling);

        // Textura AO individual por objeto
        if (aoTexture != null)
            block.SetTexture("_AOMap", aoTexture);

        rend.SetPropertyBlock(block);
    }
}

