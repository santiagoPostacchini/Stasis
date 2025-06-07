﻿using Managers.Events;
using UnityEngine;

namespace Player.Scripts
{
    public class ArmAnimationHandler : MonoBehaviour
    {
        [SerializeField] private Animator armAnimator;

        // Hashes existentes
        private readonly int _jumpHash = Animator.StringToHash("Jump");
        private readonly int _crouchHash = Animator.StringToHash("Crouch");
        private readonly int _landHash = Animator.StringToHash("Land");
        private readonly int _grabHash = Animator.StringToHash("Grab");
        private readonly int _dropHash = Animator.StringToHash("Drop");
        private readonly int _throwHash = Animator.StringToHash("Throw");
        private readonly int _shotHash = Animator.StringToHash("Shot");
        private readonly int _idleHash = Animator.StringToHash("Idle");
        
        private void OnEnable()
        {
            EventManager.Subscribe("OnJump", HandleEvent);
            EventManager.Subscribe("OnLand", HandleEvent);
            EventManager.Subscribe("OnCrouchEnter", HandleEvent);
            EventManager.Subscribe("OnCrouchExit", HandleEvent);
            EventManager.Subscribe("OnObjectGrab", HandleEvent);
            EventManager.Subscribe("OnObjectDrop", HandleEvent);
            EventManager.Subscribe("OnObjectThrow", HandleEvent);
            EventManager.Subscribe("OnShot", HandleEvent);
            EventManager.Subscribe("OnIdle", HandleEvent);
        }

        private void OnDisable()
        {
            EventManager.Unsubscribe("OnJump", HandleEvent);
            EventManager.Unsubscribe("OnLand", HandleEvent);
            EventManager.Unsubscribe("OnCrouchEnter", HandleEvent);
            EventManager.Unsubscribe("OnCrouchExit", HandleEvent);
            EventManager.Unsubscribe("OnObjectGrab", HandleEvent);
            EventManager.Unsubscribe("OnObjectDrop", HandleEvent);
            EventManager.Unsubscribe("OnObjectThrow", HandleEvent);
            EventManager.Unsubscribe("OnShot", HandleEvent);
            EventManager.Unsubscribe("OnIdle", HandleEvent);
        }

        private void HandleEvent(string eventName, GameObject sender)
        {
            if (sender != transform.root.gameObject)
                return;

            switch (eventName)
            {
                case "OnJump":
                    armAnimator.SetTrigger(_jumpHash);
                    break;
                case "OnShot":
                    armAnimator.SetTrigger(_shotHash);
                    break;
                case "OnLand":
                    armAnimator.SetTrigger(_landHash);
                    break;
                case "OnIdle":
                    armAnimator.SetTrigger(_idleHash);
                    break;
                case "OnCrouchEnter":
                    armAnimator.SetBool(_crouchHash, true);
                    break;
                case "OnCrouchExit":
                    armAnimator.SetBool(_crouchHash, false);
                    break;
                case "OnObjectGrab":
                    armAnimator.SetTrigger(_grabHash);
                    break;
                case "OnObjectDrop":
                    armAnimator.SetTrigger(_dropHash);
                    break;
                case "OnObjectThrow":
                    armAnimator.SetTrigger(_throwHash);
                    break;
            }
        }
    }
}
