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

        [SerializeField] private IStasis _lastLookedStasisObject;

        // Nueva variable para tolerancia temporal
        private float _lastCrosshairChangeTime = -0.2f;
        private float _changeCooldown = 0.2f; // 200 ms de espera mínima

        private void Update()
        {
            if (crosshair.sprite == crosshairStasis)
            {
                crosshair.rectTransform.Rotate(Vector3.forward * rotationSpeed * Time.deltaTime);
            }
            else
            {
                crosshair.rectTransform.rotation = Quaternion.Euler(0f, 0f, 0f);
            }
        }

        public void HandleVisualStasisFeedback(IStasis lookedStasisObject, UnityEngine.Camera cam)
        {
            Vector3 origin = cam.transform.position;
            Vector3 direction = cam.transform.forward;
            float radius = 0.3f;
            float maxDistance = 100f;

            bool hitSomething = Physics.SphereCast(origin, radius, direction, out RaycastHit hit, maxDistance);
            IStasis hitStasis = hitSomething ? hit.collider.GetComponent<IStasis>() : null;

            bool confirmed = lookedStasisObject != null && hitStasis == lookedStasisObject;

            if (confirmed && _lastLookedStasisObject != lookedStasisObject)
            {
                _lastLookedStasisObject = lookedStasisObject;
                crosshair.sprite = crosshairStasis;
                EventManager.TriggerEvent(selectEvent, gameObject);
            }

            if (!confirmed && _lastLookedStasisObject != null)
            {
                if (_lastCrosshairChangeTime < 0f)
                    _lastCrosshairChangeTime = Time.time;

                if (Time.time - _lastCrosshairChangeTime >= _changeCooldown)
                {
                    _lastLookedStasisObject = null;
                    _lastCrosshairChangeTime = -1f;
                    crosshair.sprite = crosshairBasic;
                    Debug.Log("Crosshair reseteado a Básico");
                }
            }

            // ▶ Si sigue apuntando, cancelar cooldown de reseteo
            if (confirmed)
                _lastCrosshairChangeTime = -1f;
        }
    }
}