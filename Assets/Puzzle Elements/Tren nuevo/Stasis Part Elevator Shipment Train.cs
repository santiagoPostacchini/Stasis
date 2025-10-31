using Player.Stasis;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StasisPartElevatorShipmentTrain : MonoBehaviour, IStasis
{
    [SerializeField] private ElevatorShipmentTrain _elevatorShipmentTrain;
    public bool IsFreezed => isFreezed;
    public bool isFreezed = false;
    public StasisEffect StasisEffect => throw new System.NotImplementedException();

    void Start()
    {
        _elevatorShipmentTrain = GetComponentInParent<ElevatorShipmentTrain>();
    }
    private void Update()
    {
        _elevatorShipmentTrain.isMoving = !IsFreezed && _elevatorShipmentTrain.canMove;
    }
    public void StatisEffectActivate()
    {
        if (isFreezed)
        {
            _elevatorShipmentTrain.StatisEffectDeactivate();
        }
        else
        {
            _elevatorShipmentTrain.StatisEffectActivate();
        }
    }

    public void StatisEffectDeactivate()
    {
        if (isFreezed)
        {
            _elevatorShipmentTrain.StatisEffectDeactivate();
        }
        else
        {
            _elevatorShipmentTrain.StatisEffectActivate();
        }
    }

}
