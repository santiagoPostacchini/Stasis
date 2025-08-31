using Player.Scripts.Interactor;
using UnityEngine;
using UnityEngine.Events;
using Player.Scripts.MVC;
using TMPro;
using Managers.Events;

namespace Puzzle_Elements.Button.Scripts
{
    public class Button : MonoBehaviour, IInteractable
    {
        private static readonly int Click = Animator.StringToHash("Click");

        [SerializeField] private Animator animator;
        [Tooltip("Evento lanzado al presionar el boton")]
        public UnityEvent onPressed;
        [Tooltip("Texto que aparece al entrar en colision con el boton")]
        [SerializeField] private TextMeshProUGUI textInteract;
        public void Interact()
        {
            animator.SetTrigger(Click);
            onPressed?.Invoke();
            EventManager.TriggerEvent("Click", gameObject);
        }
        
        private void OnTriggerStay(Collider other)
        {
            Model player = other.GetComponent<Model>();
            if (player)
            {
                if (!textInteract.gameObject.activeSelf)
                    textInteract.gameObject.SetActive(true);
            }
        }
        private void OnTriggerExit(Collider other)
        {
            Model player = other.GetComponent<Model>();
            if (player)
            {
                textInteract.gameObject.SetActive(false);
            }

        }
    }

    
}