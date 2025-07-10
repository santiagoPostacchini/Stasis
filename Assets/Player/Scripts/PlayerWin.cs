using UnityEngine;
using Player.Scripts.MVC;

public class PlayerWin : MonoBehaviour
{
    [SerializeField] private GameObject winPanel;
    private void OnTriggerEnter(Collider other)
    {
        Model player = other.GetComponent<Model>();
        if(player)
        {
            winPanel.gameObject.SetActive(true);
        }
    }
}
