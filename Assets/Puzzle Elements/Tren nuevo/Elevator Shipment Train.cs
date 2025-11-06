using Player.Stasis;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ElevatorShipmentTrain : MonoBehaviour, IStasis
{
    public bool canMove = false;
    [SerializeField] private List<Renderer> rends = new List<Renderer>();
    public bool IsFreezed => _isFreezed;
    private bool _isFreezed = false;
    public StasisEffect StasisEffect { get; private set; }

    private PistonVisualAuto _visual;
    private KinematicPiston _piston;

    private List<StasisPartElevatorShipmentTrain> list = new List<StasisPartElevatorShipmentTrain>();
    // Start is called before the first frame update
    private void Awake()
    {
        _visual = GetComponent<PistonVisualAuto>();
        _piston = GetComponentInChildren<KinematicPiston>();
    }
    void Start()
    {
        canMove = true;
        //_anim = GetComponent<Animator>();
        StasisEffect = new StasisEffect(null, rends.ToArray());
        list.AddRange(GetComponentsInChildren<StasisPartElevatorShipmentTrain>());
    }
    public void ActivateElevatorShipment()
    {

        // _anim.SetBool("On", true);
        canMove = true;
    }
    public void DesactivateElevatorShipment()
    {

        // _anim.SetBool("On", false);
        canMove = false;
    }
    void FreezeObject()
    {
        if (!_isFreezed)
        {
            _isFreezed = true;
            _piston.stasear();
            //AnimatorStateInfo info = _anim.GetCurrentAnimatorStateInfo(0);
            //_timePaused = info.normalizedTime;
            //_anim.speed = 0f;
            StasisEffect.StasisEffectStart();
            foreach (var item in list)
            {
                item.isFreezed = true;
            }
        }

    }

    void UnFreezeObject()
    {
        if (_isFreezed)
        {
            _isFreezed = false;
            _piston.Desestasear();
            //_anim.speed = 1f;
            //_anim.Play(_anim.GetCurrentAnimatorStateInfo(0).fullPathHash, 0, _timePaused);
            StasisEffect.StasisEffectStop();
            foreach (var item in list)
            {
                item.isFreezed = false;
            }
        }

    }

    public void StatisEffectActivate()
    {
        FreezeObject();
    }

    public void StatisEffectDeactivate()
    {
        UnFreezeObject();
    }
}
