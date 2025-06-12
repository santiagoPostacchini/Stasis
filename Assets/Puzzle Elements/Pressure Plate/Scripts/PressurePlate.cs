using Puzzle_Elements.AllInterfaces;
using UnityEngine;

namespace Puzzle_Elements.Pressure_Plate.Scripts
{
    public class PressurePlate : MonoBehaviour
    {
        private static readonly int Pressed = Animator.StringToHash("Pressed");

        [Header("Plate Settings")]
        [SerializeField] private PressurePlateGroup[] plateGroup;
        [SerializeField] private Animator animator;

        public bool pressed;
        public bool canBeFreezed;
        public bool IsFreezed { get; private set; }
        
        private void OnTriggerEnter(Collider other)
        {
            if (IsFreezed) return;
            if (other.gameObject.GetComponent<IPlateActivator>() != null)
            {
                pressed = true;
                UpdateAnimator(pressed);
                foreach (var group in plateGroup)
                {
                    group.NotifyPlateStateChanged();
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (IsFreezed) return;
            if (other.gameObject.GetComponent<IPlateActivator>() != null)
            {
                pressed = false;
                UpdateAnimator(pressed);
                foreach (var group in plateGroup)
                {
                    group.NotifyPlateStateChanged();
                }
            }
        }

        public void StasisEffectActivate()
        {
            if (!canBeFreezed) return;
            IsFreezed = true;
        }

        public void StasisEffectDeactivate()
        {
            if (!canBeFreezed) return;
            IsFreezed = false;
        }

        void UpdateAnimator(bool value)
        {
            animator.SetBool(Pressed, value);
        }
    }
}