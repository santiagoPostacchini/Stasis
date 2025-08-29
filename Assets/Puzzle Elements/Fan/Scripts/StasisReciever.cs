using Player.Stasis;
using UnityEngine;

namespace Puzzle_Elements.Fan.Scripts
{
    public class FanStasisController : MonoBehaviour, IStasis
    {
        public GameObject StasisObjToActivate;

        public void StatisEffectActivate()
        {
            StasisObjToActivate.GetComponent<IStasis>().StatisEffectActivate();
        }

        public void StatisEffectDeactivate()
        {
            StasisObjToActivate.GetComponent<IStasis>().StatisEffectDeactivate();
        }

        public bool IsFreezed { get; }
    }
}
