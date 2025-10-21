using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class AOSetter : MonoBehaviour
{
    [Header("Material Settings")]
    public Material materialToAssign; // Material que todos van a compartir

    [Header("AO Settings")]
    public Texture2D aoTexture;       // AO específico de este objeto

    void Awake()
    {
        var renderer = GetComponent<Renderer>();

        // 🔸 Asigna el material si existe
        if (materialToAssign != null)
            renderer.sharedMaterial = materialToAssign;

        // 🔸 Aplica el AO por objeto usando MaterialPropertyBlock
        if (aoTexture != null)
        {
            var mpb = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(mpb);
            mpb.SetTexture("_AOMap", aoTexture); // nombre interno del property en tu Shader Graph
            renderer.SetPropertyBlock(mpb);
        }
    }
}
