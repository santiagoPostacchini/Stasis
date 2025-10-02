using Player.Stasis;
using UnityEngine;
using UnityEngine.Splines;
public class StasisPartTrain : MonoBehaviour, IStasis
{
    private bool _isFreezed = false;
    public bool IsFreezed => _isFreezed;

    private StasisTrain _train;


    private void Awake()
    {
        _train = GetComponentInParent<StasisTrain>();
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
            _train.StatisEffectActivate();
        }
    }

    private void UnfreezeObject()
    {
        _train.StatisEffectDeactivate();
    }
    
}


