using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Player.Stasis;
using System;
using CurvedPathGenerator;

public class StasisTipControllerPlatformMovement : MonoBehaviour, IStasis
{
    public bool IsFreezed => _isFreezed;
    [SerializeField]private bool _isFreezed = false;
    [Header("Stasis")]
    public Material matStasis;
    public readonly string _outlineThicknessName = "_BorderThickness";
    public MaterialPropertyBlock _mpb;


    // Soporta múltiples renderers
    public Renderer[] renderers;

    public event Action OnFreezeEvent;
    public event Action OnUnFreezeEvent;

    [SerializeField] private PathFollower1 _pathFollower1;
    public void StatisEffectActivate()
    {

        FreezeObject();
        _pathFollower1.IsMove = false;
    }

    public void StatisEffectDeactivate()
    {
        UnfreezeObject();
        _pathFollower1.IsMove = true;
    }

    // Start is called before the first frame update
    void Start()
    {
        _mpb = new MaterialPropertyBlock();
        _pathFollower1 = GetComponent<PathFollower1>();
    }

    // Update is called once per frame
    void Update()
    {

    }
    private void FreezeObject()
    {
        if (!_isFreezed)
        {
            _isFreezed = true;
            SetOutlineThickness(1.05f);
            SetColorOutline(Color.green, 1f);
        }
    }

    private void UnfreezeObject()
    {
        if (!_isFreezed) return;
        _isFreezed = false;
        SetOutlineThickness(0f);
        Color lightGreen = new Color(0.6f, 1f, 0.6f);
        SetColorOutline(lightGreen, 1f);
    }
    public void SetOutlineThickness(float thickness)
    {
        if (renderers == null || _mpb == null) return;

        foreach (var rend in renderers)
        {
            if (!rend) continue;
            rend.GetPropertyBlock(_mpb);
            _mpb.SetFloat(_outlineThicknessName, thickness);
            rend.SetPropertyBlock(_mpb);
        }
    }

    public void SetColorOutline(Color color, float alpha)
    {
        if (renderers == null) return;

        foreach (var rend in renderers)
        {
            if (!rend) continue;
            rend.GetPropertyBlock(_mpb);
            _mpb.SetColor("_Color", color);
            rend.SetPropertyBlock(_mpb);
        }
    }
}
