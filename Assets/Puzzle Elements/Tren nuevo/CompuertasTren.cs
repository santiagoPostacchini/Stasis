using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CompuertasTren : MonoBehaviour
{
    private Animator _anim;

    private void Start()
    {
        _anim = GetComponent<Animator>();
    }
    public void Open()
    {
        _anim.SetBool("Open", true);
    }
    public void Close()
    {
        _anim.SetBool("Open", false);
    }
}
