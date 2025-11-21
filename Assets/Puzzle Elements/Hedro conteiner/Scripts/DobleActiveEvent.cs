using UnityEngine;
using UnityEngine.Events;

namespace Puzzle_Elements.Hedro_conteiner.Scripts
{
    public class DobleActiveEvent : MonoBehaviour
    {
        [Header("Estado de activadores")]
        [SerializeField] private bool _leftActivate;
        [SerializeField] private bool _rightActivate;

        [Header("Feedback visual")]
        [Tooltip("Renderer de la esfera que representa el activador izquierdo.")]
        [SerializeField] private Renderer leftIndicator;

        [Tooltip("Renderer de la esfera que representa el activador derecho.")]
        [SerializeField] private Renderer rightIndicator;

        [Tooltip("Color cuando el activador est� apagado.")]
        [SerializeField] private Color offColor = Color.red;

        [Tooltip("Color cuando el activador est� encendido.")]
        [SerializeField] private Color onColor = Color.green;

        [Tooltip("Nombre de la propiedad de color en el shader. En URP suele ser _BaseColor, en shaders est�ndar _Color.")]
        [SerializeField] private string colorPropertyName = "_BaseColor";

        public UnityEvent events;

        public UnityEvent eventsTrainOn;

        // MaterialPropertyBlocks para NO modificar el material original
        private MaterialPropertyBlock _leftBlock;
        private MaterialPropertyBlock _rightBlock;


        private bool _canTrainMove = false;

        void Awake()
        {
            _leftBlock = new MaterialPropertyBlock();
            _rightBlock = new MaterialPropertyBlock();
        }

        void Start()
        {
            _leftActivate = false;
            _rightActivate = false;
            UpdateIndicators();
        }

        public void ChangeLeftActivator()
        {
            _leftActivate = !_leftActivate;
            //if (!_leftActivate)
            //{
            //    LeftActivatorFalse();
            //}

            UpdateIndicators();
            TryEvent();
        }
        public void LeftActivatorFalse()
        {

        }
        public void ChangeRightActivator()
        {
            _rightActivate = !_rightActivate;
            UpdateIndicators();
            TryEvent();
        }

        public void CanTrainMove()
        {
            _canTrainMove = true;
            TryEventOn();
        }
        public void CantTrainMove()
        {
            _canTrainMove = false;
        }

        public void TryEventOn()
        {
            if (!_canTrainMove) return;
            eventsTrainOn?.Invoke();
        }
        public void TryEvent()
        {
            if (!_rightActivate || !_leftActivate) return;
            events?.Invoke();
        }

        private void UpdateIndicators()
        {

            // Indicador izquierdo
            if (leftIndicator != null)
            {
                leftIndicator.GetPropertyBlock(_leftBlock);
                _leftBlock.SetColor(colorPropertyName, _leftActivate ? onColor : offColor);
                leftIndicator.SetPropertyBlock(_leftBlock);
            }

            // Indicador derecho
            if (rightIndicator != null)
            {
                rightIndicator.GetPropertyBlock(_rightBlock);
                _rightBlock.SetColor(colorPropertyName, _rightActivate ? onColor : offColor);
                rightIndicator.SetPropertyBlock(_rightBlock);
            }
        }

    }
}
