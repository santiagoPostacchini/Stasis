using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("Scan/Scanner Manager")]
public class ScannerManager : MonoBehaviour
{
    public static readonly HashSet<Scannable> Registry = new();

    public static void Register(Scannable s)   { if (s != null) Registry.Add(s); }
    public static void Unregister(Scannable s) { if (s != null) Registry.Remove(s); }

    [Header("Input")]
    public KeyCode key = KeyCode.LeftControl;
    [Tooltip("Si está ON, se muestran mientras mantenés la tecla. OFF = toggle con duración.")]
    public bool holdToShow = true;
    public float pulseDuration = 1.5f;

    [Header("Filtro")]
    public Transform player;                // si lo dejás vacío, usa Camera.main.transform
    public float maxDistance = 25f;

    [Header("UI")]
    public Transform uiRoot;                // Canvas (Screen Space – Overlay)
    public ScanLabelUI labelPrefab;

    private bool _showing;
    private Coroutine _pulseCo;

    void Awake()
    {
        if (player == null && Camera.main != null) player = Camera.main.transform;
    }

    void Update()
    {
        bool down = Input.GetKeyDown(key);
        bool up   = Input.GetKeyUp(key);
        bool held = Input.GetKey(key);

        if (holdToShow)
        {
            if (down) StartShow();
            if (!held && _showing) StopShow();
        }
        else
        {
            if (down)
            {
                if (_showing) StopShow();
                else StartPulse(pulseDuration);
            }
        }
    }

    private void StartShow()
    {
        _showing = true;
        UpdateAll(true);
    }

    private void StartPulse(float dur)
    {
        if (_pulseCo != null) StopCoroutine(_pulseCo);
        StartShow();
        _pulseCo = StartCoroutine(Co_Pulse(dur));
    }

    private IEnumerator Co_Pulse(float dur)
    {
        yield return new WaitForSecondsRealtime(dur);
        StopShow();
        _pulseCo = null;
    }

    private void StopShow()
    {
        _showing = false;
        UpdateAll(false);
    }

    private void UpdateAll(bool show)
    {
        foreach (var s in Registry)
        {
            if (s == null || s.data == null) continue;

            if (!WithinDistance(s))
            {
                if (!show && s.spawned != null) s.Hide();
                continue;
            }

            s.EnsureLabel(uiRoot, labelPrefab);
            if (show) s.Show(); else s.Hide();
        }
    }

    private bool WithinDistance(Scannable s)
    {
        if (player == null) return true;
        float limit = s.data.maxShowDistance > 0f ? Mathf.Min(maxDistance, s.data.maxShowDistance) : maxDistance;
        return (player.position - s.WorldPoint).sqrMagnitude <= limit * limit;
    }
}
