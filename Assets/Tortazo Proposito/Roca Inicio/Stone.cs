using UnityEngine;

public class Stone : MonoBehaviour
{
    private Animator _animator;

    

    private void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    // Llamar para activar animaci�n de impacto
    public void PlayImpactAnimation()
    {
        if (_animator != null)
        {
            _animator.SetTrigger("stoneFall");
        }
    }

    // Ejemplo de c�mo detener una animaci�n si us�s bools
    public void StopAllAnimations()
    {
        if (_animator != null)
        {
            _animator.Play("Idle", -1, 0f); // Vuelve a animaci�n Idle o cualquier otra
        }
    }
}