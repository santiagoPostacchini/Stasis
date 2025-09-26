using UnityEngine;
using UnityEngine.Events;
using System.Collections;

[RequireComponent(typeof(Collider))]
public class ConsoleActivator : MonoBehaviour
{
    [Header("Detección")]
    public string playerTag = "Player";
    public Transform explicitPlayer; // opcional si no usás tag

    [Header("Modo de activación")]
    public KeyCode interactKey = KeyCode.E; // SOLO tecla
    public bool onlyOnce = false;

    [Header("Animator (opcional)")]
    public Animator animator;
    public ParamType paramType = ParamType.Bool;
    public string animatorParam = "Active";

    [Header("Eventos")]
    public UnityEvent onActivated;

    // Interno
    private bool _playerInside;
    private Transform _currentPlayer;
    private bool _hasFired;
    private bool activated = false;

    public enum ParamType { Bool, Trigger }

    [SerializeField] private Collider _collider;

    void Reset()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true; // mantiene trigger, solo detecta área
    }

    void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
    }

    void Update()
    {

        if (Input.GetKeyDown(interactKey) && _playerInside)
        {
            Fire();
        }
    }

    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            _playerInside = true;
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            _playerInside = false;
        }
    }


    private bool IsPlayer(Transform t)
    {
        if (explicitPlayer != null) return t == explicitPlayer;
        if (!string.IsNullOrEmpty(playerTag))
            return t.CompareTag(playerTag) || (t.root != null && t.root.CompareTag(playerTag));
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
        if (!activated)
        {
            StartCoroutine(wait());
        }

        if (onlyOnce) _hasFired = true;
    }

    IEnumerator wait()
    {
        yield return new WaitForSeconds(1f);
        onActivated?.Invoke();
        activated = true;
    }

    // Método público por si querés llamarlo desde otros scripts/botones
    public void ActivateManually()
    {
        Fire();
    }
}
