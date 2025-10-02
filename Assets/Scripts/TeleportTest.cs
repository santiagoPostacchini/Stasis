using System.Collections.Generic;
using UnityEngine;

public class TeleportHotkeys : MonoBehaviour
{
    [Header("Asigná los puntos en orden")]
    [SerializeField] private List<Transform> teleportPoints = new List<Transform>(9);

    [Header("Jugador a teletransportar")]
    [SerializeField] private GameObject player;

    [Header("Teclas")]
    [Tooltip("Cantidad de teclas numéricas por página (1–9).")]
    [SerializeField] private int pageSize = 9;

    private int _page = 0; // 0-based

    void Update()
    {
        if (player == null || teleportPoints == null || teleportPoints.Count == 0) return;

        // Navegación de páginas (para >9 puntos)
        if (Input.GetKeyDown(KeyCode.LeftBracket) || Input.GetKeyDown(KeyCode.PageDown))
            SetPage(_page - 1);
        if (Input.GetKeyDown(KeyCode.RightBracket) || Input.GetKeyDown(KeyCode.PageUp))
            SetPage(_page + 1);

        // Teclas 1..9 (fila superior y keypad)
        for (int d = 1; d <= Mathf.Min(pageSize, 9); d++)
        {
            if (PressedDigit(d))
            {
                int idx = _page * pageSize + (d - 1);
                if (idx < teleportPoints.Count && teleportPoints[idx] != null)
                {
                    TeleportTo(teleportPoints[idx]);
                }
            }
        }
    }

    private int MaxPage => Mathf.Max(0, (teleportPoints.Count - 1) / pageSize);

    private void SetPage(int newPage)
    {
        int clamped = Mathf.Clamp(newPage, 0, MaxPage);
        if (clamped != _page)
        {
            _page = clamped;
            Debug.Log($"[Teleport] Página: {_page + 1}/{MaxPage + 1}");
        }
    }

    private bool PressedDigit(int d)
    {
        // Alpha1..Alpha9
        KeyCode alpha = (KeyCode)((int)KeyCode.Alpha0 + d);
        // Keypad1..Keypad9
        KeyCode keypad = (KeyCode)((int)KeyCode.Keypad0 + d);
        return Input.GetKeyDown(alpha) || Input.GetKeyDown(keypad);
    }

    private void TeleportTo(Transform target)
    {
        // Si tiene CharacterController, deshabilitarlo un instante evita glitches
        var cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        player.transform.SetPositionAndRotation(target.position, target.rotation);

        if (cc != null) cc.enabled = true;
    }

#if UNITY_EDITOR
    // Gizmos para ver los índices en escena (opcional)
    private void OnDrawGizmosSelected()
    {
        if (teleportPoints == null) return;
        Gizmos.color = Color.cyan;

        for (int i = 0; i < teleportPoints.Count; i++)
        {
            var t = teleportPoints[i];
            if (t == null) continue;

            Gizmos.DrawWireSphere(t.position, 0.3f);
#if UNITY_EDITOR
            UnityEditor.Handles.Label(t.position + Vector3.up * 0.5f, $"{i + 1}");
#endif
        }
    }
#endif
}
