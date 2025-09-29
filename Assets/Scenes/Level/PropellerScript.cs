using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PropellerScript : MonoBehaviour
{
    [Header("Eje de rotación (1 = activo, 0 = inactivo)")]
    public Vector3 rotationAxis = Vector3.up; // Por defecto gira en Y

    [Header("Velocidad de rotación (grados por segundo)")]
    public float rotationSpeed = 90f;

    private void Update()
    {
        // Rotamos el objeto en el eje configurado
        transform.Rotate(rotationAxis.normalized * rotationSpeed * Time.deltaTime);
    }
}
