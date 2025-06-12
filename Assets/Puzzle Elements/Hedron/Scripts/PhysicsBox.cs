using Managers.Events;
using Managers.Game;
using Player.Stasis;
using Puzzle_Elements.AllInterfaces;
using UnityEngine;

namespace Puzzle_Elements.Hedron.Scripts
{
    public class PhysicsBox : MonoBehaviour, IStasis, IPlateActivator
    {
        [Header("Components")] [SerializeField]
        private Collider mainCollider;

        [SerializeField] private Collider playerCollider;
        public Rigidbody rb;

        private Transform _objGrabPointTransform;
        private Vector3 _velocity;

        public bool isFreezed;
        
        private bool _savedKinematic;
        private Vector3 _savedVelocity;
        private Vector3 _savedAngularVelocity;
        private float _savedDrag;

        [SerializeField] private LayerMask originalLayer;

        public bool IsOverlappingAnything { get; private set; }

        public bool IsFreezed => isFreezed;

        public void Grab()
        {
            if (!isFreezed)
            {
                _savedVelocity = Vector3.zero;
                _savedAngularVelocity = Vector3.zero;
            }

            originalLayer = gameObject.layer;
            gameObject.layer = _objGrabPointTransform.gameObject.layer;

            transform.parent = _objGrabPointTransform;
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            rb.isKinematic = true;
            rb.useGravity = false;
            SetPlayerColliderState(true);
        }

        public void Drop()
        {
            transform.parent = null;
            gameObject.layer = originalLayer;

            if (!isFreezed)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
            }
            else
            {
                float speed = _savedVelocity.magnitude;
                _savedVelocity = -_objGrabPointTransform.forward * speed;
            }

            SetPlayerColliderState(false);
        }

        public void Throw(float force)
        {
            Drop();
            if (!isFreezed)
            {
                Vector3 throwVelocity = _objGrabPointTransform.forward * (force / rb.mass);
                rb.AddForce(throwVelocity);
            }
        }

        private void SetPlayerColliderState(bool asTrigger)
        {
            playerCollider.isTrigger = asTrigger;
        }

        public void SetReferences(Transform grabHolder)
        {
            _objGrabPointTransform = grabHolder;
        }

        private void OnTriggerEnter(Collider other)
        {
            if(other.gameObject.layer != GameManager.Instance.triggerLayers)
                IsOverlappingAnything = true;
        }

        private void OnTriggerExit(Collider other)
        {
            if(other.gameObject.layer != GameManager.Instance.triggerLayers)
                IsOverlappingAnything = false;
        }

        public void StatisEffectActivate()
        {
            EventManager.TriggerEvent("StasisStart", gameObject);
            FreezeObject();
        }

        public void StatisEffectDeactivate()
        {
            EventManager.TriggerEvent("StasisEnd", gameObject);
            UnfreezeObject();
        }
        private void FreezeObject()
        {
            if (!isFreezed)
            {
                SaveObjectState();
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.useGravity = false;
                rb.isKinematic = true;
                isFreezed = true;
            }
        }

        private void SaveObjectState()
        {
            if (!rb) return;
            _savedKinematic = rb.isKinematic;
            _savedVelocity = rb.velocity;
            _savedAngularVelocity = rb.angularVelocity;
            _savedDrag = rb.drag;
        }

        private void RestoreObjectState()
        {
            if (!rb) return;
            rb.isKinematic = _savedKinematic;
            rb.velocity = _savedVelocity;
            rb.angularVelocity = _savedAngularVelocity;
            rb.drag = _savedDrag;
            rb.WakeUp();
        }

        private void UnfreezeObject()
        {
            if (!isFreezed) return;
            RestoreObjectState();
            isFreezed = false;
            rb.useGravity = true;
            rb.isKinematic = false;
        }
    }
}