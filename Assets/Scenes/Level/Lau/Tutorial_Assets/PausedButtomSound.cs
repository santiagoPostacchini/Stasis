using UnityEngine;

public class PausedButtomSound : MonoBehaviour
{
    public AudioSource uiAudio;

    void Awake()
    {
        uiAudio.ignoreListenerPause = true;
    }
}

