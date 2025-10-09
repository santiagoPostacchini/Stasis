using Player.Stasis;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StasisConteinerWithArms : MonoBehaviour,IStasis
{
    public bool IsFreezed => isFreezed;
    public bool isFreezed = false;

    public Material matStasis;
    public readonly string _outlineThicknessName = "_BorderThickness";
    public MaterialPropertyBlock _mpb;
    private Renderer _rend;
    [SerializeField] private List<Renderer> _renders = new List<Renderer>();

    private void Start()
    {
        _mpb = new MaterialPropertyBlock();
    }
    public void StatisEffectActivate()
    {
        FreezeObject();
    }

    public void StatisEffectDeactivate()
    {
        UnfreezeObject();
    }

    private void FreezeObject()
    {
        if (!isFreezed)
        {
            isFreezed = true;
            //splineAnimate.Pause();
            SetOutlineThickness(1.05f);
            SetColorOutline(Color.green, 1f);
        }
    }

    private void UnfreezeObject()
    {

        if (!isFreezed) return;
        isFreezed = false;
        //splineAnimate.Play();
        SetOutlineThickness(0f);
        Color lightGreen = new Color(0.6f, 1f, 0.6f);
        SetColorOutline(lightGreen, 1f);
    }
    public void SetOutlineThickness(float thickness)
    {
        foreach (var rend in _renders)
        {
            rend.GetPropertyBlock(_mpb);
            _mpb.SetFloat(_outlineThicknessName, thickness);
            rend.SetPropertyBlock(_mpb);
        }
    }

    public void SetColorOutline(Color color, float alpha)
    {
        foreach (var rend in _renders)
        {
            rend.GetPropertyBlock(_mpb);
            _mpb.SetColor("_Color", color);
            rend.SetPropertyBlock(_mpb);
        }
    }
}
