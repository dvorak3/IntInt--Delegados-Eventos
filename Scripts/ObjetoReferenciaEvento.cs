using System;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ObjetoReferenciaEvento : MonoBehaviour
{
    public static event Action<Transform, Transform> JugadorSeAproxima;

    public Transform escudoObjetivoTipo1;
    public Transform objetivoOrientacionTipo2;

    private bool activado;

    private void OnTriggerEnter(Collider other)
    {
        Transform actual = other.transform;
        while (actual != null)
        {
            if (actual.CompareTag("Player") ||
                actual.name.IndexOf("Jugador", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (activado)
                {
                    return;
                }

                activado = true;
                Debug.Log("El jugador se acercó al objeto de referencia.");
                JugadorSeAproxima?.Invoke(escudoObjetivoTipo1, objetivoOrientacionTipo2);
                return;
            }

            actual = actual.parent;
        }
    }
}
