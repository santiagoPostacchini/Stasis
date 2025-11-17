using Player.Scripts.Interactor;
using UnityEngine;
using UnityEngine.Events;
using TMPro;
using Managers.Events;
using Player.Scripts.MovementFSM;
using Player.Scripts.MovementFSM.MVC;
using System.Threading.Tasks;
using System.Collections;
using Audio.Scripts;
using System;

namespace Puzzle_Elements.Button.Scripts
{
    public class Button : MonoBehaviour, IInteractable, ISoundPlayer
    {
        private static readonly int Click = Animator.StringToHash("Click");
        public string animatorParam = "click";
        [SerializeField] private Animator animator;
        [Tooltip("Evento lanzado al presionar el boton")]
        public UnityEvent OnPressed;
        [Tooltip("Texto que aparece al entrar en colision con el boton")]
        [SerializeField] private TextMeshProUGUI textInteract;
        public Material yellow, green;

        private bool canCallEvent = true;

        public TextMeshProUGUI E;
        public TextMeshProUGUI text;


        public Action OnPressedAudio;
        public void SetText(TextMeshProUGUI texto, string message)
        {
            if (E == null || text == null) return;
            texto.text = message;
        }

        public void Interact()
        {
            Debug.Log("TrayApplyInteract");
            if (!canCallEvent) return;
            canCallEvent = false;
            if(animator != null)
            {
                animator.SetBool(animatorParam, true);
            }
           
            StartCoroutine(ActivateEvent());
            //ChangeMaterial();
            EventManager.TriggerEvent("Click", gameObject);
            Debug.Log("Evento llamado");
            OnPressedAudio?.Invoke();
            StartCoroutine(ReturnToIdle());
        }
        IEnumerator ActivateEvent()
        {
            yield return new WaitForSeconds(1f);
            OnPressed?.Invoke();
        }
       
        IEnumerator ReturnToIdle()
        {
            yield return new WaitForSeconds(4f);
            animator.SetBool(animatorParam, false);
            canCallEvent = true;

        }
        async void ChangeMaterial()
        {
            GetComponent<Renderer>().material = yellow;
            await Task.Delay(1000);
            GetComponent<Renderer>().material = green;
        }

    }

    
}