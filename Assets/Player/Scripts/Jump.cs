using System.Collections;
using Player.FullBody_Scripts;
using UnityEngine;

namespace Player.Scripts
{
    [RequireComponent(typeof(Rigidbody))]
    public class Jump : MonoBehaviour
    {
        [Header("<color=red>Dependencies</color>")]
        [SerializeField] private GroundCheck playerGroundCheck;

        [Header("<color=yellow>Jump Settings</color>")]
        [SerializeField] private KeyCode jumpingKey = KeyCode.Space;
        [Tooltip("Altura objetivo en metros")]
        [SerializeField] private float jumpHeight = 2.5f;
        [Tooltip("Tiempo tras un salto en el que bloqueamos nuevos saltos")]
        [SerializeField] private float jumpCooldown = 0.1f;
        [Tooltip("Ventana de buffer si presionas antes de tocar el suelo")]
        [SerializeField] private float jumpBufferTime = 0.1f;
        [Tooltip("Exigir soltar la tecla antes de permitir otro salto")]
        [SerializeField] private bool requireRelease = true;

        public bool IsJumping { get; private set; }
        public bool OnJump { get; private set; }
        public event System.Action OnJumped;

        Rigidbody _rb;
        float _bufferCounter;
        float _cooldownCounter;
        bool _needsRelease;   // bloquea próximos saltos hasta que se suelte la tecla

        void Reset()
        {
            playerGroundCheck = GetComponentInChildren<GroundCheck>();
        }

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            if (playerGroundCheck)
                playerGroundCheck.OnLanded += OnLanding;
        }

        void OnDestroy()
        {
            if (playerGroundCheck)
                playerGroundCheck.OnLanded -= OnLanding;
        }

        void Update()
        {
            // Capturamos la intención de salto (buffer)
            if (Input.GetKeyDown(jumpingKey))
                _bufferCounter = jumpBufferTime;

            if (Input.GetKeyUp(jumpingKey))
                _needsRelease = false;
        }

        void FixedUpdate()
        {
            if (_bufferCounter > 0f) _bufferCounter -= Time.fixedDeltaTime;
            if (_cooldownCounter > 0f) _cooldownCounter -= Time.fixedDeltaTime;

            bool canUseBuffer = _bufferCounter > 0f;
            bool hasCoyote = playerGroundCheck && playerGroundCheck.CoyoteCounter > 0f;
            bool cooledDown = _cooldownCounter <= 0f;
            bool releaseOk = !requireRelease || !_needsRelease;

            if (canUseBuffer && cooledDown && releaseOk && (playerGroundCheck.IsGrounded || hasCoyote))
            {
                DoJump();
            }
        }

        void DoJump()
        {
            // Estado
            IsJumping = true;
            OnJump = true;

            // Altura -> velocidad inicial (v = sqrt(2 g h))
            float g = Physics.gravity.y; // negativo
            float jumpVelocity = Mathf.Sqrt(2f * Mathf.Abs(g) * Mathf.Max(0.01f, jumpHeight));

            // Normalizamos vertical para consistencia
            Vector3 v = _rb.velocity;
            v.y = 0f;
            _rb.velocity = v;

            // Impulso instantáneo y consistente
            _rb.AddForce(Vector3.up * jumpVelocity, ForceMode.VelocityChange);

            // Señales
            OnJumped?.Invoke();

            // Timers / locks
            _bufferCounter = 0f;
            _cooldownCounter = jumpCooldown;
            _needsRelease = true;

            // pequeña ventana para “onJump” si la quieres conservar
            StartCoroutine(ResetOnJumpWindow(0.1f));
        }

        IEnumerator ResetOnJumpWindow(float duration)
        {
            yield return new WaitForSeconds(duration);
            OnJump = false;
        }

        void OnLanding()
        {
            IsJumping = false;
        }
    }
}