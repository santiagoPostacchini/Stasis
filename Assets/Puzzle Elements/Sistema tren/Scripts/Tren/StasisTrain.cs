using Player.Stasis;
using UnityEngine;
using UnityEngine.Splines;
public class StasisTrain : MonoBehaviour, IStasis
{
    private bool _isFreezed = false;
    public bool IsFreezed => _isFreezed;


    public Material matStasis;
    public readonly string _outlineThicknessName = "_BorderThickness";
    public MaterialPropertyBlock _mpb;
    private Renderer _rend;
    private float _saveVelocity;
    private TrainSystem _trainSystem;
    private void Awake()
    {
        _mpb = new MaterialPropertyBlock();
        _rend = GetComponent<Renderer>();
        _trainSystem = GetComponentInParent<TrainSystem>();
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
        if (!_isFreezed)
        {
            _saveVelocity = _trainSystem.trainSpeed;
            _trainSystem.trainSpeed = 0;
            _isFreezed = true;
            //splineAnimate.Pause();
            SetOutlineThickness(1.05f);
            SetColorOutline(Color.green, 1f);
        }
    }

    private void UnfreezeObject()
    {
        if (!_isFreezed) return;
        _isFreezed = false;
        _trainSystem.trainSpeed = _saveVelocity;
        //splineAnimate.Play();
        SetOutlineThickness(0f);
        Color lightGreen = new Color(0.6f, 1f, 0.6f);
        SetColorOutline(lightGreen, 1f);
    }
    public void SetOutlineThickness(float thickness)
    {
        _rend.GetPropertyBlock(_mpb);
        _mpb.SetFloat(_outlineThicknessName, thickness);
        _rend.SetPropertyBlock(_mpb);
    }

    public void SetColorOutline(Color color, float alpha)
    {
        _rend.GetPropertyBlock(_mpb);
        _mpb.SetColor("_Color", color);
        _rend.SetPropertyBlock(_mpb);
    }
}


