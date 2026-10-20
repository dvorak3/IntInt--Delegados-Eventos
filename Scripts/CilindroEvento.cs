using System;
using UnityEngine;

public class CilindroEvento : MonoBehaviour
{
    public static event Action OnCuboColision;

    // Se activa si el Collider del Cilindro NO tiene "Is Trigger" marcado
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player") || collision.gameObject.name.Contains("Jugador"))
        {
            NotificarColision();
        }
    }

    // Se activa si el Collider del Cilindro SI tiene "Is Trigger" marcado
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") || other.gameObject.name.Contains("Jugador"))
        {
            NotificarColision();
        }
    }

    private void NotificarColision()
    {
        Debug.Log("¡Colisión/Trigger detectado en el Cilindro! Enviando mensaje...");
        OnCuboColision?.Invoke();
    }
}