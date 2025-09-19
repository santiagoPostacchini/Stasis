using Player.Scripts.Interactor;
using UnityEngine;
using UnityEngine.Events;
using TMPro;
using Managers.Events;
using Player.FullBody_Scripts.MovementFSM;
using Player.Scripts.MovementFSM;
using Player.Scripts.MovementFSM.MVC;
using System.Threading.Tasks;

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
        public Material yellow, green;
        public void Interact()
        {
            animator.SetTrigger(Click);
            onPressed?.Invoke();
            ChangeMaterial();
            EventManager.TriggerEvent("Click", gameObject);
        }
        
       
        private void OnCollisionStay(Collision collision)
        {
            Model player = collision.gameObject.GetComponent<Model>();
            if (player)
            {
                if (!textInteract.gameObject.activeSelf)
                    textInteract.gameObject.SetActive(true);
            }
        }
        private void OnCollisionExit(Collision collision)
        {
            Model player = collision.gameObject.GetComponent<Model>();
            if (player)
            {
                textInteract.gameObject.SetActive(false);
            }

        }
        async void ChangeMaterial()
        {
            GetComponent<Renderer>().material = yellow;
            await Task.Delay(1000);
            GetComponent<Renderer>().material = green;
        }

    }

    
}