using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public Transform player;

    private void Awake()
    {
        // Verifica si ya existe una instancia
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject); // Evita duplicados
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject); // Persiste entre escenas
    }

    // Ejemplo de variable global
    public int score = 0;

    // Ejemplo de método global
    public void AddScore(int amount)
    {
        score += amount;
        Debug.Log("Puntos: " + score);
    }
}