using Player.Stasis;
using UnityEngine;

namespace Puzzle_Elements.Tren_nuevo
{
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
}
