using Player.Stasis;
using UnityEngine;
using UnityEngine.Splines;
using System.Collections.Generic;

public class StasisTrain : MonoBehaviour, IStasis
{
    private bool _isFreezed = false;
    public bool IsFreezed => _isFreezed;

    public Material matStasis;
    public readonly string _outlineThicknessName = "_BorderThickness";
    public MaterialPropertyBlock _mpb;

    [SerializeField] private List<Renderer> _rends = new List<Renderer>();

    [SerializeField] private List<StasisPartTrain> _listaObjetosStasisPartTrain = new List<StasisPartTrain>();

    private float _saveVelocity;
    private TrainSystem _trainSystem;

    private void Awake()
    {
        _mpb = new MaterialPropertyBlock();
        _trainSystem = GetComponentInParent<TrainSystem>();

        // Si no llenaste la lista en el inspector, busca todos los renderers hijos
        if (_rends.Count == 0)
        {
            _rends.AddRange(GetComponentsInChildren<Renderer>());
        }
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
            foreach (var item in _listaObjetosStasisPartTrain)
            {
                item._isFreezed = true;
            }
            SetOutlineThickness(1.05f);
            SetColorOutline(Color.green, 1f);
        }
    }

    private void UnfreezeObject()
    {
        if (!_isFreezed) return;
        _isFreezed = false;
        foreach (var item in _listaObjetosStasisPartTrain)
        {
            item._isFreezed = false;
        }
        _trainSystem.trainSpeed = _saveVelocity;
        SetOutlineThickness(0f);
        Color lightGreen = new Color(0.6f, 1f, 0.6f);
        SetColorOutline(lightGreen, 1f);
    }

    public void SetOutlineThickness(float thickness)
    {
        foreach (var rend in _rends)
        {
            rend.GetPropertyBlock(_mpb);
            _mpb.SetFloat(_outlineThicknessName, thickness);
            rend.SetPropertyBlock(_mpb);
        }
    }

    public void SetColorOutline(Color color, float alpha)
    {
        foreach (var rend in _rends)
        {
            rend.GetPropertyBlock(_mpb);
            _mpb.SetColor("_Color", color);
            rend.SetPropertyBlock(_mpb);
        }
    }
}
