using UnityEngine;
using Player.Stasis;

public class FreezableErraticObject : MonoBehaviour,IStasis
{
    [SerializeField] private Renderer _renderer;

    public ErraticObject erraticObject;

    public bool IsFreezed => erraticObject.isFreezed;
    public StasisEffect StasisEffect { get; private set; }

    private void Awake()
    {
        // Intentamos obtener el FallingRoof del mismo objeto si no est� asignado
        if (erraticObject == null)
            erraticObject = GetComponent<ErraticObject>();
    }
    private void Start()
    {
        _renderer = GetComponent<Renderer>();
        StasisEffect = new StasisEffect(_renderer);
    }


    public void StatisEffectActivate()
    {
        FreezeObject();
    }

    public void StatisEffectDeactivate()
    {
        UnfreezeObject();
    }

    private void FreezeObject()
    {
        if (!erraticObject.isFreezed)
        {
            
            erraticObject.rb.velocity = Vector3.zero;
            erraticObject.rb.angularVelocity = Vector3.zero;
            erraticObject.rb.useGravity = false;
            erraticObject.rb.isKinematic = true;
            erraticObject.isFreezed = true;
           

            StasisEffect.StasisEffectStart();
        }
    }

    private void UnfreezeObject()
    {
        if (erraticObject.isFreezed)
        {

            erraticObject.isFreezed = false;

            StasisEffect.StasisEffectStop();
        }
    }
}
