using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Player.Stasis;

public class StasisPlatformMovement : MonoBehaviour, IStasis
{
    public void StatisEffectActivate()
    {
        throw new System.NotImplementedException();
    }

    public void StatisEffectDeactivate()
    {
        throw new System.NotImplementedException();
    }

    public bool IsFreezed => _isFreezed;
    private bool _isFreezed = false;
    public StasisEffect StasisEffect { get; }
    
}
