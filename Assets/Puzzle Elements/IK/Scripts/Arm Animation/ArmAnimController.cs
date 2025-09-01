using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Player.Stasis;

public class ArmAnimController : MonoBehaviour,IStasis
{
    public Animator anim;
    public string animationClipName = "Target_Mover_128";

    [Range(0, 1)]
    public float position;
    [Range(0, 1)]
    public float shake;
    [Header("Stasis")]
    public Material matStasis;
    public readonly string _outlineThicknessName = "_BorderThickness";
    public MaterialPropertyBlock _mpb;


    // Soporta múltiples renderers
    public Renderer[] renderers;

    private bool _isFreezed = false;
    public bool IsFreezed => _isFreezed;

    
    // Start is called before the first frame update
    void Awake()
    {
        _mpb = new MaterialPropertyBlock();
    }

    private IEnumerator Start()
    {
        float randomDelay = UnityEngine.Random.Range(0, 1);
        yield return new WaitForSeconds(randomDelay);
        anim.enabled = true;
    }
    private void Update()
    {
        anim.Play(animationClipName, -1, position);
        anim.SetLayerWeight(1, shake);
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
    public void EventArmAnimController()
    {
        if (IsFreezed) StatisEffectDeactivate();
        else StatisEffectActivate();
    }
    public void StatisEffectActivate()
    {

        FreezeObject();
    }

    public void StatisEffectDeactivate()
    {
        UnfreezeObject();
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
