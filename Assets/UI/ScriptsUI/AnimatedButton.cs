using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

// Define las direcciones de entrada para la animación del botón
public enum EntryDirection { Left, Right, Top, Bottom, Scale }

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(Button))]
[RequireComponent(typeof(AudioSource))]
public class AnimatedButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Entry Animation")]
    public EntryDirection entryDirection = EntryDirection.Scale;
    public float entryDuration = 0.5f;
    public float entryDelay = 0f;
    public Vector2 offset = new Vector2(100, 0);

    [Header("Hover Animation")]
    public bool enableHover = true;
    public float hoverScale = 1.1f;
    public float hoverDuration = 0.2f;
    public bool hoverSound = false;
    public AudioClip hoverClip;

    [Header("Click Animation")]
    public bool enableClick = true;
    public float clickScale = 0.9f;
    public float clickDuration = 0.1f;
    public bool clickSound = false;
    public AudioClip clickClip;

    private RectTransform rect;
    private AudioSource audioSource;
    private Vector2 originalAnchoredPos;
    private Vector3 originalScale;

    void Awake()
    {
        rect = GetComponent<RectTransform>();
        audioSource = GetComponent<AudioSource>();
        originalAnchoredPos = rect.anchoredPosition;
        originalScale = rect.localScale;
    }

    void OnEnable()
    {
        PlayEntry();
    }

    // Ejecuta la animación de entrada
    public void PlayEntry()
    {
        rect.DOKill();
        if (entryDirection == EntryDirection.Scale)
        {
            rect.localScale = Vector3.zero;
            rect.anchoredPosition = originalAnchoredPos;
        }
        else
        {
            rect.localScale = originalScale;
            rect.anchoredPosition = originalAnchoredPos + OffsetForDirection();
        }

        Sequence seq = DOTween.Sequence();
        seq.AppendInterval(entryDelay);

        if (entryDirection == EntryDirection.Scale)
            seq.Append(rect.DOScale(originalScale, entryDuration).SetEase(Ease.OutBack));
        else
            seq.Append(rect.DOAnchorPos(originalAnchoredPos, entryDuration).SetEase(Ease.OutCubic));
    }

    // Calcula el offset inicial según la dirección
    Vector2 OffsetForDirection()
    {
        switch (entryDirection)
        {
            case EntryDirection.Left:   return new Vector2(-offset.x, 0);
            case EntryDirection.Right:  return new Vector2( offset.x, 0);
            case EntryDirection.Top:    return new Vector2(0,  offset.y);
            case EntryDirection.Bottom: return new Vector2(0, -offset.y);
            default: return Vector2.zero;
        }
    }

    // Animación y sonido al pasar el cursor
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!enableHover) return;
        rect.DOKill();
        rect.DOScale(originalScale * hoverScale, hoverDuration).SetEase(Ease.OutBack);
        if (hoverSound && hoverClip) audioSource.PlayOneShot(hoverClip);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!enableHover) return;
        rect.DOKill();
        rect.DOScale(originalScale, hoverDuration).SetEase(Ease.OutBack);
    }

    // Animación y sonido al hacer click
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!enableClick) return;
        rect.DOKill();
        rect.DOPunchScale(Vector3.one * (1 - clickScale), clickDuration).SetEase(Ease.OutFlash);
        if (clickSound && clickClip) audioSource.PlayOneShot(clickClip);
    }
}
