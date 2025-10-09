using Player.Stasis;
using UnityEngine;
using System.Collections.Generic;

public class StasisTrain : MonoBehaviour, IStasis
{
    private bool _isFreezed;
    public bool IsFreezed => _isFreezed;

    [SerializeField] private List<Renderer> rends = new List<Renderer>();

    [SerializeField] private List<StasisPartTrain> _listaObjetosStasisPartTrain = new List<StasisPartTrain>();

    private float _saveVelocity;
    private TrainSystem _trainSystem;

    public StasisEffect StasisEffect { get; private set; }
    
    private void Awake()
    {
        _trainSystem = GetComponentInParent<TrainSystem>();

        if (rends.Count == 0)
        {
            rends.AddRange(GetComponentsInChildren<Renderer>());
        }
        
        StasisEffect = new StasisEffect(null, rends.ToArray());
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
            StasisEffect.StasisEffectStart();
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
        StasisEffect.StasisEffectStop();
    }
}
