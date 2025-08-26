using UnityEngine;
using Player.Scripts.MVC;

public class TeleportScript : MonoBehaviour
{
    [SerializeField] private Transform pos1;
    [SerializeField] private Transform pos2;
    [SerializeField] private Transform pos3;
    [SerializeField] private Transform pos4;
    [SerializeField] private Model player;

    void Update()
    {
        if (player == null) return;

        if (Input.GetKeyDown(KeyCode.Alpha1))
            TeleportPlayer(pos1);
        if (Input.GetKeyDown(KeyCode.Alpha2))
            TeleportPlayer(pos2);
        if (Input.GetKeyDown(KeyCode.Alpha3))
            TeleportPlayer(pos3);
        if (Input.GetKeyDown(KeyCode.Alpha4))
            TeleportPlayer(pos4);
    }

    private void TeleportPlayer(Transform targetPos)
    {
        player.characterController.enabled = false;
        player.transform.position = targetPos.position;
        player.characterController.enabled = true;
    }
}