using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Player.Stasis;

public class FreezeableObject : MonoBehaviour, IStasis
{
    [SerializeField] private Renderer _renderer;
    [SerializeField] private FreezableErraticObject _freezeableErraticObject;
    public bool IsFreezed => isFreezed;
    public bool isFreezed = false;
    public StasisEffect StasisEffect { get; set; }

    private void Start()
    {
        _renderer = GetComponent<Renderer>();
        StasisEffect = new StasisEffect(_renderer);
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
        if (isFreezed) return;
        isFreezed = true;
        StasisEffect.StasisEffectStart();
    }

    private void UnfreezeObject()
    {
        if (!isFreezed) return;
        isFreezed = false;
        StasisEffect.StasisEffectStop();
    }
}