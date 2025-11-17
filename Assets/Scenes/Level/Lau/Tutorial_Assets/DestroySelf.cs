using UnityEngine;

public class DisableComponents : MonoBehaviour
{
    private MeshRenderer _renderer;
    private Collider _collider;
    private Rigidbody _rigidbody;

    private void Awake()
    {
        _renderer = GetComponent<MeshRenderer>();
        _collider = GetComponent<Collider>();
        _rigidbody = GetComponent<Rigidbody>();
    }

    public void Destroy()
    {
        if (_renderer)
            _renderer.enabled = false;

        if (_collider)
            _collider.enabled = false;

        if (_rigidbody)
        {
            _rigidbody.useGravity = false;
            _rigidbody.velocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
            _rigidbody.isKinematic = true;
        }
    }
}

