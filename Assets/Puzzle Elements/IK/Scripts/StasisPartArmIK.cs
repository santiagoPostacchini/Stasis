using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Player.Stasis;

public class StasisPartArmIK : MonoBehaviour,IStasis
{
    public bool IsFreezed => _isFreezed;
    private bool _isFreezed = false;
    [SerializeField] private StasisTipController _stasisTipController;

    
  
    // Start is called before the first frame update
    void Start()
    {
        _stasisTipController.OnFreezeEvent += StatisEffectActivate;
        _stasisTipController.OnUnFreezeEvent += StatisEffectDeactivate;
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
        if(a == true)
        {
            _stasisTipController.StatisEffectActivate();
        }
        else
        {
            _stasisTipController.StatisEffectDeactivate();
        }
    }

    private void Update()
    {
       
    }
}
