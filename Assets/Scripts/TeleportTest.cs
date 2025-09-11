using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TeleportTest : MonoBehaviour
{
    [SerializeField] private Transform pos1;
    [SerializeField] private Transform pos2;
    [SerializeField] private Transform pos3;

    [SerializeField] private GameObject player;
    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            player.transform.position = pos1.position;
        }
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            player.transform.position = pos2.position;
        }
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            player.transform.position = pos3.position;
        }
    }
}
