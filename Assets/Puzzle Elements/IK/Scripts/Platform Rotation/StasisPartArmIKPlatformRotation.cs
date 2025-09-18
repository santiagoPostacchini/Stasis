using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Player.Stasis;
using IKSuite;

public class StasisPartArmIKPlatformRotation : MonoBehaviour, IStasis, IStasisPartIK
{
    public bool IsFreezed => _isFreezed;
    private bool _isFreezed = false;
    [SerializeField] private StasisTipControllerPlatformRotation _stasisTipControllerPlatformRotation;


    // Start is called before the first frame update
    void Start()
    {
        if(_stasisTipControllerPlatformRotation == null)
        {
            _stasisTipControllerPlatformRotation = GetComponentInParent<StasisTipControllerPlatformRotation>();
        }
        _stasisTipControllerPlatformRotation.OnFreezeEvent += StatisEffectActivate;
        _stasisTipControllerPlatformRotation.OnUnFreezeEvent += StatisEffectDeactivate;
    }


    public void StatisEffectActivate()
    {
        NotifyTipController(true);
    }

    public void StatisEffectDeactivate()
    {
        NotifyTipController(false);
    }

    public void NotifyTipController(bool a)
    {
        if (a == true)
        {
            _stasisTipControllerPlatformRotation.StatisEffectActivate();
        }
        else
        {
            _stasisTipControllerPlatformRotation.StatisEffectDeactivate();
        }
    }

    public void SetTipController(Component tipController)
    {
        var a = tipController.GetComponent<StasisTipControllerPlatformRotation>();
        if (a != null)
        {
            _stasisTipControllerPlatformRotation = a;
        }
    }
}
