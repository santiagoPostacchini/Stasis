using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Player.Stasis;

public class StasisPlatformMovement : MonoBehaviour, IStasis
{
    public bool IsFreezed => _isFreezed;
    private bool _isFreezed = false;
    public void StatisEffectActivate()
    {
        throw new System.NotImplementedException();
    }

    public void StatisEffectDeactivate()
    {
        throw new System.NotImplementedException();
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    
}
