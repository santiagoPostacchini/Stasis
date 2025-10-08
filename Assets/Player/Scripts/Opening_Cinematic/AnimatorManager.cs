using UnityEngine;
using System.Collections;

public class AnimatorManager : MonoBehaviour
{
    [Header("Referencias")]
    public Animator targetAnimator;
    public RagdollHanger ragdollHanger;

    [Header("Opciones")]
    public float startDelay = 1f;

    [HideInInspector]
    public bool animatorActivated = false; // ← NUEVA VARIABLE

    private bool animatorStarted = false;

    void Start()
    {
        if (targetAnimator == null)
        {
            Debug.LogWarning("AnimatorManager: falta asignar targetAnimator");
            return;
        }

        if (ragdollHanger == null)
        {
            Debug.LogWarning("AnimatorManager: falta asignar ragdollHanger");
            return;
        }

        targetAnimator.enabled = false;
    }

    void Update()
    {
        if (ragdollHanger.fadeBlack && !animatorStarted)
        {
            StartCoroutine(StartAnimatorAfterDelay());
            animatorStarted = true;
        }
    }

    private IEnumerator StartAnimatorAfterDelay()
    {
        yield return new WaitForSeconds(startDelay);

        if (targetAnimator != null)
        {
            targetAnimator.enabled = true;
            animatorActivated = true; // ← MARCAMOS COMO ACTIVADO
        }
    }
}

