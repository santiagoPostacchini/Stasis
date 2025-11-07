using Managers.Game;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DoorTrain : MonoBehaviour
{
    private Animator _anim;
    private bool _open = false;

    private void Start()
    {
        _anim = GetComponent<Animator>();
        GameManager.Instance.OnDeathPlayer += CloseDoor;
    }


    public void OpenDoor()
    {
        StartCoroutine(wait());
    }
    IEnumerator wait()
    {
        yield return new WaitForSeconds(16f);
        _open = true;
        _anim.SetBool("Open", true);
    }

    public void CloseDoor()
    {
        if (!_open) return;

        _anim.SetBool("Open", false);
        _open = false;
    }
}
