using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Player.Stasis;

public class FreezeableObject : MonoBehaviour,IStasis
{
    public Material matStasis;
    private string OutlineThicknessName = "_BorderThickness";
    private MaterialPropertyBlock _mpb;
    [SerializeField] private Renderer _renderer;
    [SerializeField] private FreezableErraticObject _freezeableErraticObject;
    public bool IsFreezed => isFreezed;
    public bool isFreezed = false;

    private void Start()
    {
        _mpb = new MaterialPropertyBlock();
        _renderer = GetComponent<Renderer>();

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
            Debug.Log("Aaaaaaaaaaaaaaaaa");
            SetColorOutline(Color.green, 1);
            SetOutlineThickness(1.05f);
        }
    }

    private void UnfreezeObject()
    {
        if (isFreezed)
        {

            isFreezed = false;
            SetColorOutline(Color.white, 1);
            SetOutlineThickness(1f);
        }
    }
    public void SetOutlineThickness(float thickness)
    {
        if (_renderer != null && _mpb != null)
        {
            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetFloat(OutlineThicknessName, thickness);
            // _mpb.SetColor("_Color", Color.green);
            _renderer.SetPropertyBlock(_mpb);
            //Glow(false, 1);
        }
    }
    public void SetColorOutline(Color color, float alpha)
    {
        _renderer.GetPropertyBlock(_mpb);
        //_mpb.SetFloat("_Alpha", alpha);

        _mpb.SetColor("_Color", color);
        _renderer.SetPropertyBlock(_mpb);
    }
}
