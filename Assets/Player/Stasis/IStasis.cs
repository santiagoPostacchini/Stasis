using UnityEngine;

namespace Player.Stasis
{
    public interface IStasis
    {
        void StatisEffectActivate();
        void StatisEffectDeactivate();

        bool IsFreezed { get; }
    }
}