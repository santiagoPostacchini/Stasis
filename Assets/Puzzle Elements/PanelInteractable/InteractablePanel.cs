using Player.Scripts.Interactor;
using UnityEngine;

namespace Player.Scripts.Interactor
{
    public class InteractablePanel : MonoBehaviour, IInteractable
    {
        [SerializeField] private HedronContainerIn _hedronContainer;

        public void Interact()
        {
            _hedronContainer.Interact();
            Debug.Log("Interactuo con el panel");
        }
    }
}