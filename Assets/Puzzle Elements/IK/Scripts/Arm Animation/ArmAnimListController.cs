using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

public class ArmAnimListController : MonoBehaviour
{
    [Tooltip("Lista de controladores de animación de los brazos.")]
    public List<ArmAnimController> armList;

    [Tooltip("Posición mínima que puede alcanzar el brazo (valor normalizado entre 0 y 1).")]
    [Range(0, 1)] public float posMin = 0f;

    [Tooltip("Posición máxima que puede alcanzar el brazo (valor normalizado entre 0 y 1).")]
    [Range(0, 1)] public float posMax = 1f;

    [Tooltip("Intensidad mínima del efecto de 'shake' (valor normalizado entre 0 y 1).")]
    [Range(0, 1)] public float shakeMin = 0f;

    [Tooltip("Intensidad máxima del efecto de 'shake' (valor normalizado entre 0 y 1).")]
    [Range(0, 1)] public float shakeMax = 1f;

    [Tooltip("Separación de fase base entre cada brazo.")]
    public float offset = 0f;

    [Tooltip("Amplitud adicional de la fase por índice de brazo.")]
    public float amp = 0f;

    // Tiempo LOCAL por brazo (solo avanza cuando NO está en stasis)
    private List<float> localTimes = new List<float>();

    private void Start()
    {
        offset = (armList != null && armList.Count > 0) ? 1f / armList.Count : 0f;
        SyncLocalTimes(reset: true);
    }

    private void OnValidate()
    {
        if (!Application.isPlaying) return;
        SyncLocalTimes();
    }

    private void Update()
    {
        CalculatePosAndShake();
    }

    private void SyncLocalTimes(bool reset = false)
    {
        int n = armList != null ? armList.Count : 0;
        if (reset || localTimes.Count != n)
        {
            localTimes.Clear();
            for (int i = 0; i < n; i++)
            {
                // Arranca alineado con Time.time para mantener la fase inicial
                localTimes.Add(Time.time);
            }
        }
    }

    private void CalculatePosAndShake()
    {
        if (armList == null) return;
        if (localTimes.Count != armList.Count) SyncLocalTimes();

        for (int i = 0; i < armList.Count; i++)
        {
            var arm = armList[i];
            if (!arm) continue;

            // Si está staseado: NO avanza tiempo, NO recalcula pos,
            // y fuerza el shake a 0 para eliminar vibración.
            if (arm.IsFreezed)
            {
                arm.shake = 0f; // <- fix anti-vibración
                continue;
            }

            // Avanza solo cuando no está staseado
            localTimes[i] += Time.deltaTime;

            // Misma fórmula que tenías, pero con tiempo local
            float t = localTimes[i] + offset;   // si querés por índice, podrías usar offset * i
            t = math.cos(t + amp * i);
            t = t * 0.5f + 0.5f;

            float shake = math.remap(0f, 1f, shakeMin, shakeMax, t);
            float pos = math.remap(0f, 1f, posMin, posMax, t);

            UpdateArm(i, shake, pos);
        }
    }

    private void UpdateArm(int index, float shake, float pos)
    {
        armList[index].position = pos;
        armList[index].shake = shake;
    }
}
