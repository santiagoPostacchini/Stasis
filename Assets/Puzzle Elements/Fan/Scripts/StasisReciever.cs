using Player.Stasis;
using UnityEngine;

namespace Puzzle_Elements.Fan.Scripts
{
    public class FanStasisController : MonoBehaviour, IStasis
    {
        public GameObject StasisObjToActivate;
        private Collider _col;
        public bool IsFreezed { get; }
        public StasisEffect StasisEffect { get; }
        private void Start()
        {
            _col = GetComponent<Collider>();
        }

        public void StatisEffectActivate()
        {
            StasisObjToActivate.GetComponent<IStasis>().StatisEffectActivate();
            _col.isTrigger = true;
        }

        public void StatisEffectDeactivate()
        {
            StasisObjToActivate.GetComponent<IStasis>().StatisEffectDeactivate();
            _col.isTrigger = false;
        }

    }
}
