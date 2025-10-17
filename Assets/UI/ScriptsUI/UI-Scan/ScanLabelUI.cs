using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ScanLabelUI : MonoBehaviour
{
    [Header("Refs")]
    public CanvasGroup group;
    public Image icon;
    public TextMeshProUGUI title;
    public TextMeshProUGUI hint;

    [Header("Apariencia")]
    public float fadeTime = 0.15f;         // unscaled
    public Vector2 screenOffset = new Vector2(0f, 32f);
    public bool scaleWithDistance = true;
    public Vector2 scaleRange = new Vector2(0.85f, 1.15f);  // min..max

    private Scannable target;
    private Camera _cam;

    public void Bind(Scannable sc)
    {
        target = sc;
        ApplyDescriptor(sc.data);
        group.alpha = 0f;
        group.interactable = group.blocksRaycasts = false;
        if (_cam == null) _cam = Camera.main;
    }

    void LateUpdate()
    {
        if (target == null || _cam == null) return;

        // Posicionar en pantalla
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(_cam, target.WorldPoint);
        ((RectTransform)transform).position = screen + screenOffset;

        // Escalar con distancia (sutil)
        if (scaleWithDistance)
        {
            float d = Vector3.Distance(_cam.transform.position, target.WorldPoint);
            float k = Mathf.InverseLerp(2f, 20f, d);
            float s = Mathf.Lerp(scaleRange.y, scaleRange.x, k);
            ((RectTransform)transform).localScale = Vector3.one * s;
        }
    }

    public void Show(bool instant = false)  => StartCoroutine(Fade(1f, instant));
    public void Hide(bool instant = false)  => StartCoroutine(Fade(0f, instant));

    private IEnumerator Fade(float to, bool instant)
    {
        if (instant)
        {
            group.alpha = to;
        }
        else
        {
            float t = 0f;
            float from = group.alpha;
            while (t < fadeTime)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, t / fadeTime);
                yield return null;
            }
            group.alpha = to;
        }
        bool on = to > 0.999f;
        group.interactable = on;
        group.blocksRaycasts = on;
    }

    private void ApplyDescriptor(ScanDescriptor d)
    {
        if (title) { title.text = d.displayName; title.color = d.color; }
        if (hint)  { hint.text  = d.hint;        hint.color  = d.color; }
        if (icon)
        {
            icon.sprite = d.icon;
            icon.enabled = d.icon != null;
            icon.color = d.color;
        }
    }
}
