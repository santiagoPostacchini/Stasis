using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Scripts_Menu_Folder
{
    public class UIButtonHoverEffectTMP : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [Header("TextMeshPro Settings")]
        public TMP_Text buttonText;
        public Color hoverColor = Color.yellow;
        public float hoverFontSize = 36f;
        public float transitionSpeed = 5f;

        [Header("Audio Settings")]
        public AudioSource audioSource;
        public AudioClip hoverSound;
        public AudioClip clickSound;

        private Color originalColor;
        private float originalFontSize;
        private bool isHovered = false;

        void Start()
        {
            if (buttonText == null)
                buttonText = GetComponentInChildren<TMP_Text>();

            if (buttonText == null)
            {
                Debug.LogError("UIButtonHoverEffectTMP: No TMP_Text found in children.");
                enabled = false;
                return;
            }

            originalColor = buttonText.color;
            originalFontSize = buttonText.fontSize;

            if (audioSource == null)
                Debug.LogWarning("UIButtonHoverEffectTMP: No AudioSource assigned.");
        }

        void Update()
        {
            float deltaTime = Time.unscaledDeltaTime; 

            if (isHovered)
            {
                buttonText.color = Color.Lerp(buttonText.color, hoverColor, deltaTime * transitionSpeed);
                buttonText.fontSize = Mathf.Lerp(buttonText.fontSize, hoverFontSize, deltaTime * transitionSpeed);
            }
            else
            {
                buttonText.color = Color.Lerp(buttonText.color, originalColor, deltaTime * transitionSpeed);
                buttonText.fontSize = Mathf.Lerp(buttonText.fontSize, originalFontSize, deltaTime * transitionSpeed);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovered = true;
            if (hoverSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(hoverSound);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (clickSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(clickSound);
            }
        }
    }
}



