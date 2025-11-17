using System.Collections;
using Managers.Game;
using Player.Scripts.MovementFSM.MVC;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class SmokeHazardTrigger : MonoBehaviour
{
    [Header("Filtro de objetivo")]
    [Tooltip("Si está activo, solo afecta al colisionar con este tag (ej: Player).")]
    public bool requireTag = true;
    public string targetTag = "Player";

    [Tooltip("Opcional: filtra por capas (dejar en Nothing para ignorar).")]
    public LayerMask playerLayers;

    [Header("Referencia directa (opcional)")]
    [Tooltip("Si lo completas, siempre usará este Model como objetivo. Si lo dejas vacío, lo buscará en el collider que entra.")]
    public Model forcedPlayerModel;

    [Header("Comportamiento de ahogo")]
    [Tooltip("Tiempo para pasar de multiplicador 1 a 0 dentro del humo.")]
    [Min(0.1f)] public float slowdownDuration = 2f;

    [Tooltip("Tiempo extra en segundos que el player debe permanecer en 0 para morir.")]
    [Min(0f)] public float timeAtZeroBeforeDeath = 1f;

    [Tooltip("Curva de caída de movilidad. X: tiempo normalizado (0–1), Y: multiplicador (1–0).")]
    public AnimationCurve slowdownCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    [Header("Reset")]
    [Tooltip("Restaurar el multiplicador a 1 al salir del humo (si no ha muerto aún).")]
    public bool restoreOnExit = true;

    [Header("Cooldown de muerte")]
    [Tooltip("Evita múltiples muertes seguidas (segundos).")]
    [Min(0f)] public float repeatCooldown = 0.25f;

    private float _nextAllowedDeathTime;

    // Estado interno
    private bool _playerInside;
    private Coroutine _slowRoutine;

    private IHazardSlowTarget _currentSlowTarget;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true; // Asegura Trigger
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsValidTarget(other)) return;

        _playerInside = true;
        StartSlowdown(other);
    }

    private void OnTriggerStay(Collider other)
    {
        if (!IsValidTarget(other)) return;
        _playerInside = true; // mantenemos vivo el flag
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsValidTarget(other)) return;

        _playerInside = false;
        StopSlowdownAndRestore();
    }

    private bool IsValidTarget(Collider other)
    {
        if (requireTag && !other.CompareTag(targetTag)) return false;

        if (playerLayers.value != 0)
        {
            if ((playerLayers.value & (1 << other.gameObject.layer)) == 0)
                return false;
        }

        return true;
    }

    private void StartSlowdown(Collider other)
    {
        if (_slowRoutine != null) return;

        // 1) Referencia a la interfaz
        if (forcedPlayerModel != null)
        {
            _currentSlowTarget = forcedPlayerModel as IHazardSlowTarget;
        }
        else
        {
            _currentSlowTarget = other.GetComponentInParent<IHazardSlowTarget>();
        }

        if (_currentSlowTarget == null)
        {
            Debug.LogWarning($"[{name}] No se encontró IHazardSlowTarget en el player. " +
                             $"Asegúrate de que Model implemente la interfaz y/o arrástralo en 'forcedPlayerModel'.");
            return;
        }

        // 2) Freno inmediato de velocidad horizontal (golpe grave)
        Model model = forcedPlayerModel 
            ? forcedPlayerModel 
            : other.GetComponentInParent<Model>();

        if (model != null && model.rb != null)
        {
            Vector3 v = model.rb.velocity;
            Vector3 horizontal = new Vector3(v.x, 0f, v.z);

            // Dejarle un 50–60% de la velocidad que traía en lugar de matarla casi a cero
            horizontal *= 0.6f;

            model.rb.velocity = horizontal + Vector3.up * v.y;
        }


        _slowRoutine = StartCoroutine(SlowdownRoutine());
    }


    private void StopSlowdownAndRestore()
    {
        if (_slowRoutine != null)
        {
            StopCoroutine(_slowRoutine);
            _slowRoutine = null;
        }

        if (restoreOnExit && _currentSlowTarget != null)
        {
            _currentSlowTarget.SetExternalSpeedMultiplier(1f);
        }

        _currentSlowTarget = null;
    }

    // ReSharper disable Unity.PerformanceAnalysis
    private IEnumerator SlowdownRoutine()
    {
        float t = 0f;

        // Fase 1: reducimos movilidad desde 1 hasta 0
        while (t < slowdownDuration)
        {
            if (!_playerInside || _currentSlowTarget == null)
                yield break;

            float normalizedTime = t / slowdownDuration;
            float multiplier = slowdownCurve.Evaluate(normalizedTime); // 1 → 0

            _currentSlowTarget.SetExternalSpeedMultiplier(multiplier);

            t += Time.deltaTime;
            yield return null;
        }

        // Aseguramos estado final en 0
        if (_currentSlowTarget != null)
            _currentSlowTarget.SetExternalSpeedMultiplier(0f);

        // Fase 2: tiempo detenido antes de la muerte
        float zeroTime = 0f;
        while (zeroTime < timeAtZeroBeforeDeath)
        {
            if (!_playerInside || _currentSlowTarget == null)
                yield break;

            zeroTime += Time.deltaTime;
            yield return null;
        }

        // Ya cumplimos tiempo en 0 dentro del humo: muerte
        if (Time.time >= _nextAllowedDeathTime && GameManager.Instance != null)
        {
            GameManager.Instance.PlayerDeath();
            _nextAllowedDeathTime = Time.time + repeatCooldown;
        }
        else if (GameManager.Instance == null)
        {
            Debug.LogWarning($"[{name}] No hay GameManager.Instance en escena.");
        }

        _slowRoutine = null;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.25f);
        var col = GetComponent<Collider>();
        if (col is BoxCollider b)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(b.center, b.size);
        }
        else
        {
            Gizmos.DrawWireSphere(transform.position, 0.25f);
        }
    }
#endif
}
