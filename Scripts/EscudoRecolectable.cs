using UnityEngine;

public class EscudoRecolectable : MonoBehaviour
{
    public enum TipoEscudo
    {
        Tipo1,
        Tipo2
    }

    public TipoEscudo tipo;

    private bool recolectado;

    private void OnTriggerEnter(Collider other)
    {
        IntentarRecolectar(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        IntentarRecolectar(collision.gameObject);
    }

    private void IntentarRecolectar(GameObject objeto)
    {
        if (recolectado)
        {
            return;
        }

        ControlJugador jugador = objeto.GetComponentInParent<ControlJugador>();
        if (jugador == null)
        {
            return;
        }

        recolectado = true;
        int puntos = tipo == TipoEscudo.Tipo1 ? 5 : 10;
        jugador.SumarPuntos(puntos);
        Destroy(gameObject);
    }
}
