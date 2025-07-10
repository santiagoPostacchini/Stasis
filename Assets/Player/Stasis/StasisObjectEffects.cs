using Managers.Events;
using UnityEngine;
using UnityEngine.UI;
namespace Player.Stasis
{
    [RequireComponent(typeof(AudioSource))]
    public class StasisObjectEffects : MonoBehaviour
    {
        [Header("Animation Settings")]
        public AnimationCurve animCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        public float animSpeed = 2f;
        [Header("Scale Settings")]
        public float normalScale = 1f;
        public float highlightScale = 1.2f;
        [Header("Color Settings")]
        public Color normalColor = Color.white;
        public Color highlightColor = Color.cyan;
        [SerializeField] private float rotationSpeed = 10;
        [Header("Eventos de Sonido")]
       
        public string selectEvent = "SelectStasiable";
        [SerializeField] private Image crosshair;
        [SerializeField] private Sprite crosshairBasic;
        [SerializeField] private Sprite crosshairStasis;
        
        // Variables mejoradas para evitar parpadeo
        private float _lastCrosshairChangeTime;
        private readonly float _changeCooldown = 0.15f; // Cooldown para cambios
        private bool _isCurrentlyLookingAtStasis;
        private bool _audioEventTriggered;
        
        private void Update()
        {
            if (crosshair.sprite == crosshairStasis)
            {
                crosshair.rectTransform.Rotate(Vector3.forward * (rotationSpeed * Time.deltaTime));
            }
            else
            {
                crosshair.rectTransform.rotation = Quaternion.Euler(0f, 0f, 0f);
            }
        }
        
        public void HandleVisualStasisFeedback(IStasis lookedStasisObject, UnityEngine.Camera cam, RaycastHit hit1)
        {
            Vector3 origin = cam.transform.position;
            Vector3 direction = cam.transform.forward;
            float radius = 0.2f;
            float maxDistance = 100f;

            bool hitSomething = Physics.SphereCast(origin, radius, direction, out RaycastHit hit, maxDistance);
            IStasis hitStasis = hitSomething ? hit.collider.GetComponent<IStasis>() : null;

            bool objectConnected = CheckObjectStasisConnected(hit1);
            bool confirmed = lookedStasisObject != null && hitStasis == lookedStasisObject;

            // Verificar si podemos cambiar el estado (cooldown)
            bool canChangeState = Time.time - _lastCrosshairChangeTime >= _changeCooldown;

            // Cambiar a crosshair de stasis
            if (confirmed && !objectConnected && canChangeState)
            {
                if (!_isCurrentlyLookingAtStasis)
                {
                    _isCurrentlyLookingAtStasis = true;
                    crosshair.sprite = crosshairStasis;
                    _lastCrosshairChangeTime = Time.time;
                    
                    // Trigger del evento de audio solo una vez
                    if (!_audioEventTriggered)
                    {
                        EventManager.TriggerEvent(selectEvent, gameObject);
                        _audioEventTriggered = true;
                    }
                }
            }
            
            // Cambiar a crosshair básico
            if (!confirmed && _isCurrentlyLookingAtStasis && canChangeState)
            {
                _isCurrentlyLookingAtStasis = false;
                crosshair.sprite = crosshairBasic;
                _lastCrosshairChangeTime = Time.time;
                _audioEventTriggered = false; // Reset del flag de audio
                Debug.Log("Crosshair reseteado a Básico");
            }
        }
        
        private bool CheckObjectStasisConnected(RaycastHit hit)
        {
            DestroyedPieceController piece = hit.collider.gameObject.GetComponent<DestroyedPieceController>();
            if (!piece) return false;
            if (piece.is_connected)
            {
                // Solo cambiar si no estamos en cooldown
                if (Time.time - _lastCrosshairChangeTime >= _changeCooldown)
                {
                    crosshair.sprite = crosshairBasic;
                    _isCurrentlyLookingAtStasis = false;
                    _audioEventTriggered = false;
                    _lastCrosshairChangeTime = Time.time;
                }
                return true;
            }
            return false;
        }
    }
}