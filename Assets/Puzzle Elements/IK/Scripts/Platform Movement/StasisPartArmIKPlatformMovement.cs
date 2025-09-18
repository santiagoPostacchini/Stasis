using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Player.Stasis;
using IKSuite;

public class StasisPartArmIKPlatformMovement : MonoBehaviour,IStasis,IStasisPartIK
{
    public bool IsFreezed => _isFreezed;
    private bool _isFreezed = false;
    [SerializeField] private StasisTipControllerPlatformMovement _stasisTipControllerPlatformMovement;

    [SerializeField] private bool a;

    // Start is called before the first frame update
    void Start()
    {
        _stasisTipControllerPlatformMovement.OnFreezeEvent += StatisEffectActivate;
        _stasisTipControllerPlatformMovement.OnUnFreezeEvent += StatisEffectDeactivate;
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
            _stasisTipControllerPlatformMovement.StatisEffectActivate();
        }
        else
        {
            _stasisTipControllerPlatformMovement.StatisEffectDeactivate();
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

    public void SetTipController(Component tipController)
    {
        var a = tipController.GetComponent<StasisTipControllerPlatformMovement>();
        if (a != null)
        {
            _stasisTipControllerPlatformMovement = a;
        }
    }
}
