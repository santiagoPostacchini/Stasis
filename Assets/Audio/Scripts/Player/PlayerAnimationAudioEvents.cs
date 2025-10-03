using System;
using UnityEngine;

namespace Audio.Scripts.Player
{
    /// <summary>
    /// Bridge entre Animation Events y el sistema de audio (ISoundPlayer).
    /// Exponer acá los eventos canónicos que querés mapear a sonido.
    /// </summary>
    public class PlayerAnimationAudioEvents : MonoBehaviour, ISoundPlayer
    {
        public event Action OnStepLeft;
        public event Action OnStepRight;

        public void Anim_StepLeft()    => OnStepLeft?.Invoke();
        public void Anim_StepRight()   => OnStepRight?.Invoke();
    }
}