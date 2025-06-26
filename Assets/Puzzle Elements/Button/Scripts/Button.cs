using Player.Scripts.Interactor;
using UnityEngine;
using UnityEngine.Events;
using Player.Scripts.MVC;
using TMPro;

namespace Puzzle_Elements.Button.Scripts
{
    public class Button : MonoBehaviour, IInteractable
    {
        private static readonly int Click = Animator.StringToHash("Click");

        [SerializeField] private Animator animator;
        public UnityEvent onPressed;
        [SerializeField] private TextMeshProUGUI _textInteract;
        public void Interact()
        {
            animator.SetTrigger(Click);
            onPressed?.Invoke();
        }

        private void OnTriggerEnter(Collider other)
        {
            Model player = other.GetComponent<Model>();
            if(player != null)
            {
                _textInteract.gameObject.SetActive(true);
            }
        }
        private void OnTriggerExit(Collider other)
        {
            Model player = other.GetComponent<Model>();
            if (player != null)
            {
                _textInteract.gameObject.SetActive(false);
            }

        }
    }

    
}