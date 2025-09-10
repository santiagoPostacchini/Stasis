using System.Linq;
using UnityEngine;

public class SpiderStepGovernor : MonoBehaviour
{
    [SerializeField] private SpiderFoot[] feet;
    [SerializeField] private int maxStepping = 3; // trípode

    void Update()
    {
        int stepping = feet.Count(f => f != null && f.IsStepping);
        bool gateClosed = stepping >= maxStepping;

        foreach (var f in feet)
        {
            if (f == null) continue;
            f.StepGateEnabled = !gateClosed || f.IsStepping;
        }
    }
}