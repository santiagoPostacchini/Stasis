using Player.Scripts.Interactor;
using UnityEngine;
using UnityEngine.Events;

namespace Puzzle_Elements.Button.Scripts
{
    public class Button : MonoBehaviour, IInteractable
    {
        private static readonly int Click = Animator.StringToHash("Click");

        [SerializeField] private Animator animator;
        public UnityEvent onPressed;

        public void Interact()
        {
            animator.SetTrigger(Click);
            onPressed?.Invoke();
        }
    }
}