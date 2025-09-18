using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

public class MaterialsToHide : MonoBehaviour
{
    [Header("Materiales que deben mostrarse en Scene pero solo castear sombras en Game")]
    [SerializeField] private List<Material> targetMaterials = new List<Material>();

    private Renderer[] renderers;

    void Start()
    {
        // Buscar todos los renderers que usen alguno de los materiales indicados
        List<Renderer> temp = new List<Renderer>();
        Renderer[] allRenderers = FindObjectsOfType<Renderer>();

        foreach (var rend in allRenderers)
        {
            foreach (var mat in targetMaterials)
            {
                if (rend.sharedMaterial == mat)
                {
                    temp.Add(rend);
                    break;
                }
            }
        }

        renderers = temp.ToArray();

        // Si estamos en modo Game (Play), ponerlos en "Shadows Only"
        if (Application.isPlaying)
        {
            foreach (var rend in renderers)
            {
                rend.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                rend.enabled = true; // Necesario para que las sombras funcionen
            }
        }
    }

#if UNITY_EDITOR
    // En el editor, mientras NO estamos en Play, aseguramos que sean visibles
    void OnValidate()
    {
        if (!Application.isPlaying && renderers != null)
        {
            foreach (var rend in renderers)
            {
                if (rend != null)
                {
                    rend.shadowCastingMode = ShadowCastingMode.On;
                    rend.enabled = true;
                }
            }
        }
    }
#endif
}
