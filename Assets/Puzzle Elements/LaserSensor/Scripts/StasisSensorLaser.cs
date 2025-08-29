using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Player.Stasis;

public class StasisSensorLaser : MonoBehaviour, IStasis
{
    private SensorLaser _sensorLaser;
    public bool IsFreezed => isFreezed;
    [SerializeField]private bool isFreezed;

    [Header("Stasis")]
    public Material matStasis;
    public readonly string _outlineThicknessName = "_BorderThickness";
    public MaterialPropertyBlock _mpb;
    void Start()
    {
        _sensorLaser = GetComponent<SensorLaser>();
        _mpb = new MaterialPropertyBlock();
    }
    // Soporta múltiples renderers
    public Renderer[] renderers;

    public void StatisEffectActivate()
    {
        FreezeObject();
        _sensorLaser.CanShootLasers(false);
    }

    public void StatisEffectDeactivate()
    {
        UnfreezeObject();
        _sensorLaser.CanShootLasers(true);
    }
  

    private void FreezeObject()
    {
        if (!isFreezed)
        {
            isFreezed = true;
            SetOutlineThickness(1.05f);
            SetColorOutline(Color.green, 1f);
        }
    }

    private void UnfreezeObject()
    {
        if (!isFreezed) return;
        isFreezed = false;
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