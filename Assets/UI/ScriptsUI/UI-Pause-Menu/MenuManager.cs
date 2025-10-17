using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
[AddComponentMenu("UI/Menu Manager")]
public class MenuManager : MonoBehaviour
{
    [Serializable]
    public struct MenuPanel
    {
        [Tooltip("ID único para referenciar este panel, ej: 'Pause', 'Settings', 'Inventory'")]
        public string id;
        [Tooltip("GameObject raíz del Canvas/Panel")]
        public GameObject panel;
        [Tooltip("Opcional: Panel inicia activo")]
        public bool startActive;
    }

    [Header("Paneles Registrados")]
    [SerializeField] private List<MenuPanel> panels = new List<MenuPanel>();

    [Header("ID del Panel de Pausa (debe existir en la lista)")]
    [SerializeField] private string pausePanelId = "Pause";

    [Header("Control de entrada")]
    [Tooltip("Usar Input System (si está habilitado) además de la tecla Escape")]
    [SerializeField] private bool useNewInputSystem = true;

#if ENABLE_INPUT_SYSTEM
    [Tooltip("Action que dispara la pausa (ej: <Keyboard>/escape)")]
    [SerializeField] private InputActionReference pauseAction;
#endif

    [Header("Eventos")]
    public UnityEvent OnPaused;
    public UnityEvent OnResumed;

    private readonly Dictionary<string, GameObject> _dict = new();
    private bool _isPaused;

    void Awake()
    {
        _dict.Clear();
        foreach (var p in panels)
        {
            if (string.IsNullOrWhiteSpace(p.id) || p.panel == null) continue;
            if (!_dict.ContainsKey(p.id)) _dict.Add(p.id, p.panel);
            p.panel.SetActive(p.startActive);
        }
        // Asegurar estado inicial no pausado
        ApplyPause(false, true);
    }

    void OnEnable()
    {
#if ENABLE_INPUT_SYSTEM
        if (useNewInputSystem && pauseAction != null)
        {
            pauseAction.action.performed += OnPausePerformed;
            pauseAction.action.Enable();
        }
#endif
    }

    void OnDisable()
    {
#if ENABLE_INPUT_SYSTEM
        if (useNewInputSystem && pauseAction != null)
        {
            pauseAction.action.performed -= OnPausePerformed;
            pauseAction.action.Disable();
        }
#endif
    }

    void Update()
    {
        // Soporte legacy input
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePause();
        }
    }

#if ENABLE_INPUT_SYSTEM
    private void OnPausePerformed(InputAction.CallbackContext ctx)
    {
        TogglePause();
    }
#endif

    public void TogglePause()
    {
        if (!_dict.ContainsKey(pausePanelId))
        {
            Debug.LogWarning($"[MenuManager] No se encontró panel con id '{pausePanelId}'.");
            return;
        }

        if (_isPaused) Resume();
        else Pause();
    }

    public void Pause()
    {
        if (_isPaused) return;
        ShowOnly(pausePanelId);
        ApplyPause(true);
        OnPaused?.Invoke();
    }

    public void Resume()
    {
        if (!_isPaused) return;
        HideAll();
        ApplyPause(false);
        OnResumed?.Invoke();
    }

    /// <summary> Muestra sólo el panel con id y oculta el resto. </summary>
    public void ShowOnly(string id)
    {
        foreach (var kv in _dict)
            kv.Value.SetActive(kv.Key == id);
    }

    /// <summary> Muestra un panel específico sin ocultar los demás. </summary>
    public void Show(string id)
    {
        if (_dict.TryGetValue(id, out var go)) go.SetActive(true);
    }

    public void Hide(string id)
    {
        if (_dict.TryGetValue(id, out var go)) go.SetActive(false);
    }

    public void HideAll()
    {
        foreach (var kv in _dict) kv.Value.SetActive(false);
    }

    private void ApplyPause(bool pause, bool force = false)
    {
        if (!force && _isPaused == pause) return;

        _isPaused = pause;
        Time.timeScale = pause ? 0f : 1f;
        AudioListener.pause = pause;

        // Cursor
        Cursor.visible = pause;
        Cursor.lockState = pause ? CursorLockMode.None : CursorLockMode.Locked;
    }

    public bool IsPaused() => _isPaused;
}
