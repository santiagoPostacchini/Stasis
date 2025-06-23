using UnityEngine;

public class Stone : MonoBehaviour
{
    private Animator _animator;

    

    private void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    // Llamar para activar animación de impacto
    public void PlayImpactAnimation()
    {
        if (_animator != null)
        {
            _animator.SetTrigger("stoneFall");
        }
    }

    // Ejemplo de cómo detener una animación si usás bools
    public void StopAllAnimations()
    {
        if (_animator != null)
        {
            _animator.Play("Idle", -1, 0f); // Vuelve a animación Idle o cualquier otra
        }
    }
}