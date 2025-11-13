using Managers.Game;
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

        public float dist = 20;
        private void Start()
        {
            _col = GetComponent<Collider>();
        }
        private void Update()
        {
            if (Vector3.Distance(transform.position, GameManager.Instance.player.transform.position) < dist)
            {
                _col.isTrigger = true;
            }
            else _col.isTrigger = false;
        }
        public void StatisEffectActivate()
        {
            StasisObjToActivate.GetComponent<IStasis>().StatisEffectActivate();
        }

        public void StatisEffectDeactivate()
        {
            StasisObjToActivate.GetComponent<IStasis>().StatisEffectDeactivate();
        }

    }
}
