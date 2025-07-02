using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
public class HurtEffect : MonoBehaviour
{
    public Material hurtMaterial;
    
    public void ShowHurtEffect()
    {
        hurtMaterial.SetFloat("_Intensity", 0f);
        hurtMaterial.DOFloat(1f, "_Intensity", 0.5f)  // anima de 0 a 1 en 0.5 seg
            .OnComplete(() => {
                hurtMaterial.DOFloat(0f, "_Intensity", 0.5f); // vuelve a 0 en 0.5 seg
        });
    }
}
