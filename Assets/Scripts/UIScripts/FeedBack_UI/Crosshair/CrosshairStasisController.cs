using Player.Stasis;
using UnityEngine;
using UnityEngine.UI;

namespace UIScripts.FeedBack_UI.Crosshair
{
    /// <summary>
    /// Colocar en la cámara del jugador. Cambia color/escala del crosshair
    /// cuando el raycast impacta un proxy registrado (layer "StasisProxy").
    /// Expone eventos: OnAcquireTarget / OnLoseTarget.
    /// </summary>
    [AddComponentMenu("Assets/Scripts/UIScripts/FeedBack_UI/Crosshair")]
    public class CrosshairStasisController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Cámara que dispara el raycast. Si null, usa Camera.main.")]
        public Camera cam;

        [Tooltip("UI Image del crosshair.")]
        public Image crosshairImage;

        [Tooltip("RectTransform del crosshair (para escalar). Si null, usa el de la Image.")]
        public RectTransform crosshairRect;

        [Header("Raycast (solo proxies)")]
        [Tooltip("Distancia máxima del raycast.")]
        public float maxDistance = 40f;

        [Tooltip("LayerMask de proxies (ej. solo 'StasisProxy').")]
        public LayerMask proxyLayerMask;

        [Tooltip("Raycast también contra triggers (debe ser 'Collide' para impactar proxies trigger).")]
        public QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;

        [Header("Crosshair Visuals")]
        public Color baseColor = new Color(1f, 1f, 1f, 0.9f);
        public Color stasisColor = new Color(0.3f, 1f, 0.4f, 1f); // verde
        [Tooltip("Escala base del crosshair (1 = 100%).")]
        public float baseScale = 1f;
        [Tooltip("Escala cuando hay objetivo stasiable.")]
        public float stasisScale = 1.35f;

        [Header("Smoothing (Hz = respuesta por segundo)")]
        public float onLerpHz = 10f;
        public float offLerpHz = 6f;

        [Header("Hysteresis (anti-parpadeo)")]
        public float acquireHoldSeconds = 0.04f;
        public float loseHoldSeconds = 0.08f;

        [Header("Debug")]
        public bool drawRay = true;
        public Color rayColorHit = Color.green;
        public Color rayColorMiss = Color.red;

        // ---------------- Eventos (Paso 1) ----------------
        /// <summary>Se dispara cuando el crosshair adquiere un objetivo (tras el hold).</summary>
        public event System.Action<IStasis> OnAcquireTarget = delegate { };

        /// <summary>Se dispara cuando el crosshair pierde el objetivo (tras el hold de salida).</summary>
        public event System.Action<IStasis> OnLoseTarget = delegate { };

        // Runtime
        private bool _wantTarget;
        private bool _hasTarget;
        private float _acquireTimer;
        private float _loseTimer;
        private Color _currentColor;
        private float _currentScale;

        // Exposición del target actual
        public IStasis currentStasis { get; private set; }

        // Cache
        private static readonly RaycastHit[] _hit = new RaycastHit[1];

        private void Reset()
        {
            cam = Camera.main;
        }

        private void Awake()
        {
            if (cam == null) cam = Camera.main;
            if (crosshairImage != null && crosshairRect == null)
                crosshairRect = crosshairImage.rectTransform;

            _currentColor = baseColor;
            _currentScale = baseScale;
            ApplyVisualsImmediate();
        }

        private void Update()
        {
            // 1) Detección en layer de proxies
            _wantTarget = TryDetectStasis(out var stasisFound);

            if (_wantTarget)
            {
                // reinicio del temporizador de pérdida
                _loseTimer = 0f;

                // si es un nuevo target diferente al current, actualizo referencia
                currentStasis = stasisFound;

                // hold de adquisición
                _acquireTimer += Time.unscaledDeltaTime;
                if (!_hasTarget && _acquireTimer >= acquireHoldSeconds)
                {
                    _hasTarget = true;
                    // -----> Evento: adquirimos objetivo
                    OnAcquireTarget(currentStasis);
                }
            }
            else
            {
                // reseteo del hold de adquisición
                _acquireTimer = 0f;

                // si teníamos target activo, aplico hold de pérdida
                if (_hasTarget)
                {
                    _loseTimer += Time.unscaledDeltaTime;
                    if (_loseTimer >= loseHoldSeconds)
                    {
                        _hasTarget = false;
                        _loseTimer = 0f;

                        // guardo referencia previa para evento
                        var lost = currentStasis;
                        currentStasis = null;

                        // -----> Evento: perdimos objetivo
                        OnLoseTarget(lost);
                    }
                }
            }

            // 2) Interpolación de visuals (unscaled para consistencia con pausas)
            float dt = Time.unscaledDeltaTime;
            if (_hasTarget)
                LerpTo(stasisColor, stasisScale, onLerpHz, dt);
            else
                LerpTo(baseColor, baseScale, offLerpHz, dt);

            // 3) Debug ray
            if (drawRay && cam != null)
            {
                Vector3 o = cam.transform.position;
                Vector3 d = cam.transform.forward * maxDistance;
                Debug.DrawRay(o, d, _hasTarget ? rayColorHit : rayColorMiss, 0f, false);
            }
        }

        private bool TryDetectStasis(out IStasis stasis)
        {
            stasis = null;
            if (cam == null) cam = Camera.main;
            if (cam == null) return false;

            int n = Physics.RaycastNonAlloc(
                cam.transform.position,
                cam.transform.forward,
                _hit,
                maxDistance,
                proxyLayerMask,
                triggerInteraction
            );
            if (n <= 0) return false;

            var h = _hit[0];

            // Lookup instantáneo por collider del proxy
            if (StasisRegistry.TryGet(h.collider, out stasis))
                return true;

            // Fallback: por si algún proxy no se registró (idealmente no debería pasar)
            var proxy = h.collider.GetComponent<StasisProxy>();
            if (proxy != null && proxy.owner is IStasis sOwner)
            {
                stasis = sOwner;
                return true;
            }

            // Último fallback: buscar en padres (más costoso, pero robusto)
            var mono = h.collider.GetComponentInParent<MonoBehaviour>();
            if (mono is IStasis s) { stasis = s; return true; }

            return false;
        }

        private void LerpTo(Color targetColor, float targetScale, float hz, float dt)
        {
            float k = 1f - Mathf.Exp(-hz * dt);
            _currentColor = Color.LerpUnclamped(_currentColor, targetColor, k);
            _currentScale = Mathf.LerpUnclamped(_currentScale, targetScale, k);
            ApplyVisuals();
        }

        private void ApplyVisualsImmediate()
        {
            if (crosshairImage != null) crosshairImage.color = _currentColor;
            if (crosshairRect != null) crosshairRect.localScale = Vector3.one * _currentScale;
        }

        private void ApplyVisuals()
        {
            if (crosshairImage != null) crosshairImage.color = _currentColor;
            if (crosshairRect != null)
            {
                Vector3 s = crosshairRect.localScale;
                float t = _currentScale;
                s.x = t; s.y = t; s.z = 1f;
                crosshairRect.localScale = s;
            }
        }
    }
}
