using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class Trigger1 : MonoBehaviour
{
    public List<FollowTargetController> followTargetControllers;
    public Material yellow, green;
    private bool _alreadyCollision = false;

    private void OnTriggerEnter(Collider other)
    {
        if (_alreadyCollision) return;
        Debug.Log("Entre en colision");
        foreach (var item in followTargetControllers)
        {
            item.ChangePosition();
        }
        _alreadyCollision = true;
        ChangeMaterial();
    }
    private void OnTriggerExit(Collider other)
    {
        _alreadyCollision = false;
    }
    public void ChangeDirection()
    {
        if (_alreadyCollision) return;
        Debug.Log("Entre en colision");
        foreach (var item in followTargetControllers)
        {
            item.ChangePosition();
        }
        _alreadyCollision = true;
        ChangeMaterial();
    }
    async void ChangeMaterial()
    {
        GetComponent<Renderer>().material = yellow;
        await Task.Delay(1000);
        GetComponent<Renderer>().material = green;
    }
}
