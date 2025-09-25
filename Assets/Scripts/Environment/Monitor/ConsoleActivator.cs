using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class ConsoleActivator : MonoBehaviour
{
    [Header("Detección")]
    public string playerTag = "Player";
    public Transform explicitPlayer; // opcional si no usás tag

    [Header("Modo de activación")]
    public bool activateOnEnter = true;    // activa apenas el jugador entra
    public bool requireInteractKey = false; // si true, requiere tecla
    public KeyCode interactKey = KeyCode.E;
    public bool onlyOnce = false;

    [Header("Animator (opcional)")]
    public Animator animator;
    public ParamType paramType = ParamType.Bool;
    public string animatorParam = "Activated";

    [Header("Eventos")]
    public UnityEvent onActivated;

    // Interno
    private bool _playerInside;
    private Transform _currentPlayer;
    private bool _hasFired;

    public enum ParamType { Bool, Trigger }

    void Reset()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
    }

    void Update()
    {
        if (!_playerInside || _hasFired) return;

        if (requireInteractKey)
        {
            if (Input.GetKeyDown(interactKey))
            {
                Fire();
            }
        }
        else if (!activateOnEnter)
        {
            // Modo “permanecer dentro”: se activa apenas detectamos que está dentro y no pedimos tecla
            Fire();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_hasFired && onlyOnce) return;

        if (IsPlayer(other.transform))
        {
            _playerInside = true;
            _currentPlayer = other.transform;

            if (activateOnEnter && !requireInteractKey)
            {
                Fire();
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.transform == _currentPlayer)
        {
            _playerInside = false;
            _currentPlayer = null;
        }
    }

    private bool IsPlayer(Transform t)
    {
        if (explicitPlayer != null) return t == explicitPlayer;
        if (!string.IsNullOrEmpty(playerTag)) return t.CompareTag(playerTag) || (t.root != null && t.root.CompareTag(playerTag));
        return false;
    }

    private void Fire()
    {
        if (_hasFired && onlyOnce) return;

        // Animator
        if (animator && !string.IsNullOrEmpty(animatorParam))
        {
            if (paramType == ParamType.Bool)
                animator.SetBool(animatorParam, true);
            else
                animator.SetTrigger(animatorParam);
        }

        // Evento
        onActivated?.Invoke();

        if (onlyOnce) _hasFired = true;
    }

    // Método público por si querés llamarlo desde otros scripts/botones
    public void ActivateManually()
    {
        Fire();
    }
}
