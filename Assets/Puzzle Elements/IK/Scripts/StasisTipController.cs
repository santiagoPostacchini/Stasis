using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Player.Stasis;
using System;
using Managers.Game;

public class StasisTipController : MonoBehaviour,IStasis
{
    public bool IsFreezed => _isFreezed;
    public StasisEffect StasisEffect { get; }
    private bool _isFreezed = false;
    [Header("Stasis")]
    public Material matStasis;
    public readonly string _outlineThicknessName = "_BorderThickness";
    public MaterialPropertyBlock _mpb;


    // Soporta m�ltiples renderers
    public List<Renderer> renderers = new List<Renderer>();

    public event Action OnFreezeEvent;
    public event Action OnUnFreezeEvent;

    [SerializeField] private FollowTargetController _followTargetController;

    [SerializeField] private Transform _root;

    // Start is called before the first frame update
    void Start()
    {
        _mpb = new MaterialPropertyBlock();
        _followTargetController = GetComponent<FollowTargetController>();
        StartCoroutine(wait());
        GameManager.Instance.OnDeathPlayer += PlayerDeath;
    }
    private void PlayerDeath()
    {
        StatisEffectDeactivate();
    }
    IEnumerator wait()
    {
        yield return new WaitForSeconds(2f);
        AddElementsToRenderer();
    }
    public void AddElementsToRenderer()
    {
        if (renderers.Count > 4) return;

        Renderer[] allRenderers = _root.GetComponentsInChildren<Renderer>();

        foreach (Renderer mesh in allRenderers)
        {
            // Evita duplicados si ya lo agregaste
            if (!renderers.Contains(mesh))
                renderers.Add(mesh);
        }
        Renderer[] allRenderers2 = GetComponentsInChildren<Renderer>();
        foreach (Renderer mesh in allRenderers2)
        {
            // Evita duplicados si ya lo agregaste
            if (!renderers.Contains(mesh))
                renderers.Add(mesh);
        }

    }
    public void EventPositiveTipController()
    {
        if(!IsFreezed)
        StatisEffectActivate();
    }
    public void EventNegativeTipController()
    {
        if (IsFreezed) StatisEffectDeactivate();
    }
    public void StatisEffectActivate()
    {

        FreezeObject();
        _followTargetController.canMove = false;
    }

    public void StatisEffectDeactivate()
    {
        UnfreezeObject();
        _followTargetController.canMove = true;
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
