using Managers.Events;
using Managers.Game;
using Player.Stasis;
using Puzzle_Elements.AllInterfaces;
using UnityEngine;
using Audio.Scripts;

namespace Puzzle_Elements.Hedron.Scripts
{
    public class PhysicsBox : MonoBehaviour, IStasis, IPlateActivator
    {

        public Material matStasis;
        private string OutlineThicknessName = "_BorderThickness";
        private MaterialPropertyBlock _mpb;
        [SerializeField] private Renderer _renderer;


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

        [SerializeField] private bool initFrozen = false;
        [SerializeField] private ParticleSystem _particleFrozen;


        private AudioEventListener _audioEventListener;
        private void Start()
        {
            _mpb = new MaterialPropertyBlock();
            _renderer = GetComponent<Renderer>();
            _audioEventListener = GetComponent<AudioEventListener>();
            if (initFrozen)
            {
                StatisEffectActivate();
            }
        }
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
            FreezeObject();
        }

        public void StatisEffectDeactivate()
        {
            UnfreezeObject();
        }
        private void FreezeObject()
        {
            if (!isFreezed)
            {
               // _audioEventListener.SetStopEventFlag("ObjInStasis", false);
                EventManager.TriggerEvent("ObjInStasis", gameObject);
                SaveObjectState();
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.useGravity = false;
                rb.isKinematic = true;
                isFreezed = true;
                SetColorOutline(Color.green, 1);
                SetOutlineThickness(1.05f);
                _particleFrozen?.Play();
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
            SetColorOutline(Color.white, 0.2f);
            SetOutlineThickness(0f);
            _particleFrozen?.Stop();
            //_audioEventListener.SetStopEventFlag("ObjInStasis", true, true);
            _audioEventListener.StopSound("ObjInStasis");
        }


        public void SetOutlineThickness(float thickness)
        {
            if (_renderer != null && _mpb != null)
            {
                _renderer.GetPropertyBlock(_mpb);
                _mpb.SetFloat(OutlineThicknessName, thickness);
                // _mpb.SetColor("_Color", Color.green);
                _renderer.SetPropertyBlock(_mpb);
                //Glow(false, 1);
            }
        }
        public void SetColorOutline(Color color, float alpha)
        {
            _renderer.GetPropertyBlock(_mpb);
            //_mpb.SetFloat("_Alpha", alpha);

            _mpb.SetColor("_Color", color);
            _renderer.SetPropertyBlock(_mpb);
        }
    }
}