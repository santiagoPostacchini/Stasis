using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Player.Stasis;

public class StasisPartArmIKPlatformRotation : MonoBehaviour, IStasis
{
    public bool IsFreezed => _isFreezed;
    private bool _isFreezed = false;
    [SerializeField] private StasisTipControllerPlatformRotation _stasisTipControllerPlatformRotation;

    [SerializeField] private bool a;

    // Start is called before the first frame update
    void Start()
    {
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

    private void Update()
    {
        if (!a) return;

        if (Input.GetKeyDown(KeyCode.J))
        {
            StatisEffectActivate();
        }
        if (Input.GetKeyDown(KeyCode.K))
        {
            StatisEffectDeactivate();
        }
    }
}
