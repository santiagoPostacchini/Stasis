using Player.Stasis;
using UnityEngine;
using UnityEngine.Splines;
public class StasisPartElevator : MonoBehaviour, IStasis
{
    private bool _isFreezed = false;
    public bool IsFreezed => _isFreezed;

    private StasisElevator _elevator;


    private void Awake()
    {
        _elevator = GetComponentInParent<StasisElevator>();
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
            _elevator.StatisEffectActivate();
        }
    }

    private void UnfreezeObject()
    {
        _elevator.StatisEffectDeactivate();
    }
    
    
}


