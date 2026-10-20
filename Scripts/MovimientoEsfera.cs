using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[ExecuteAlways]
public class MovimientoEsfera : MonoBehaviour
{
    public enum TipoHumanoide { Tipo1, Tipo2 }

    private static event Action OnCuboTocaTipo2;
    private static event Action OnCuboTocaTipo1;

    [Header("Configuración")]
    public TipoHumanoide tipo;
    public float velocidad = 4.0f;
    [Tooltip("Prefab del personaje que representa este humanoide.")]
    public GameObject modeloHumanoidePrefab;
    [Min(0.1f)]
    public float escalaModelo = 0.75f;

    [Header("Objetivos")]
    public Transform objetivoEsferaTipo2;
    public Transform objetivoCilindro;
    public Transform escudoTipo1;
    [Tooltip("Objetivo único, conservado para mantener las referencias ya asignadas en la escena.")]
    public Transform escudoTipo2;
    [Tooltip("Escudos u objetos físicos del grupo 2 a los que irá el grupo 1.")]
    public Transform[] escudosTipo2;
    public Transform objetivoOrientacionTipo2;

    [Header("Separación")]
    [Min(0.1f)]
    public float separacionEnObjetivo = 0.8f;

    private static readonly Dictionary<Transform, List<MovimientoEsfera>> SeguidoresPorObjetivo =
        new Dictionary<Transform, List<MovimientoEsfera>>();

    private Transform objetivoActual = null;
    private bool estaDerrotado = false;
    private Rigidbody rb;
    private GameObject modeloHumanoide;

    private void Awake()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }

        // Mantenemos el Rigidbody kinematic durante el movimiento para evitar peleas con MoveTowards/MovePosition
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    private void PrepararModeloHumanoide()
    {
        if (modeloHumanoidePrefab == null)
        {
            Debug.LogWarning($"No hay un prefab asignado para {name}.", this);
            return;
        }

        Renderer rendererEsfera = GetComponent<Renderer>();
        if (rendererEsfera != null)
        {
            rendererEsfera.enabled = false;
        }

        Transform modeloExistente = transform.Find(modeloHumanoidePrefab.name);
        if (modeloExistente != null)
        {
            modeloHumanoide = modeloExistente.gameObject;
        }
        else if (Application.isPlaying)
        {
            modeloHumanoide = Instantiate(modeloHumanoidePrefab, transform);
        }
#if UNITY_EDITOR
        else
        {
            modeloHumanoide = PrefabUtility.InstantiatePrefab(
                modeloHumanoidePrefab,
                gameObject.scene) as GameObject;
            if (modeloHumanoide == null)
            {
                Debug.LogError($"No se pudo crear el prefab visual para {name}.", this);
                return;
            }
            modeloHumanoide.transform.SetParent(transform, false);
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
        modeloHumanoide.name = modeloHumanoidePrefab.name;
        modeloHumanoide.transform.localPosition = new Vector3(0f, -0.5f, 0f);
        modeloHumanoide.transform.localRotation = Quaternion.identity;
        modeloHumanoide.transform.localScale = Vector3.one * escalaModelo;
    }

    private void OnEnable()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorApplication.delayCall += CrearModeloEnEditor;
            return;
        }
#endif
        PrepararModeloHumanoide();
        CilindroEvento.OnCuboColision += ResponderACilindro;
        ObjetoReferenciaEvento.JugadorSeAproxima += ResponderAObjetoReferencia;
        OnCuboTocaTipo2 += IrAHaciaEscudo1;
        OnCuboTocaTipo1 += IrAHaciaEscudo2;
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        EditorApplication.delayCall -= CrearModeloEnEditor;
        if (!Application.isPlaying)
        {
            return;
        }
#endif
        CilindroEvento.OnCuboColision -= ResponderACilindro;
        ObjetoReferenciaEvento.JugadorSeAproxima -= ResponderAObjetoReferencia;
        OnCuboTocaTipo2 -= IrAHaciaEscudo1;
        OnCuboTocaTipo1 -= IrAHaciaEscudo2;
        EstablecerObjetivo(null);
    }

#if UNITY_EDITOR
    private void CrearModeloEnEditor()
    {
        if (this != null && isActiveAndEnabled && !Application.isPlaying && gameObject.scene.IsValid())
        {
            PrepararModeloHumanoide();
        }
    }
#endif

    // --- RESPUESTA AL CILINDRO ---
    private void ResponderACilindro()
    {
        if (estaDerrotado) return;

        if (tipo == TipoHumanoide.Tipo1 && objetivoEsferaTipo2 != null)
        {
            EstablecerObjetivo(objetivoEsferaTipo2);
        }
        else if (tipo == TipoHumanoide.Tipo2 && objetivoCilindro != null)
        {
            EstablecerObjetivo(objetivoCilindro);
        }
    }

    // --- RESPUESTAS A HUMANOS (ESCUDOS) ---
    private void IrAHaciaEscudo1()
    {
        if (tipo != TipoHumanoide.Tipo1 || estaDerrotado)
        {
            return;
        }

        Transform destino = escudoTipo1;
        if (destino == null)
        {
            ObjetoReferenciaEvento evento = FindAnyObjectByType<ObjetoReferenciaEvento>();
            if (evento != null)
            {
                destino = evento.escudoObjetivoTipo1;
            }
        }
        if (destino == null)
        {
            GameObject objetivo = GameObject.Find("EscudoTipo1_1");
            if (objetivo != null)
            {
                destino = objetivo.transform;
            }
        }

        if (destino != null)
        {
            EstablecerObjetivo(destino);
        }
        else
        {
            Debug.LogWarning(
                $"No hay un escudo objetivo tipo 1 asignado para {name}.",
                this);
        }
    }

    private void IrAHaciaEscudo2()
    {
        if (tipo != TipoHumanoide.Tipo1 || estaDerrotado)
        {
            return;
        }

        Transform objetivoMasCercano = null;
        float distanciaMasCercana = float.PositiveInfinity;

        if (escudoTipo2 != null)
        {
            objetivoMasCercano = escudoTipo2;
            distanciaMasCercana = (escudoTipo2.position - transform.position).sqrMagnitude;
        }

        if (escudosTipo2 != null)
        {
            foreach (Transform escudo in escudosTipo2)
            {
                if (escudo == null)
                {
                    continue;
                }

                float distancia = (escudo.position - transform.position).sqrMagnitude;
                if (distancia < distanciaMasCercana)
                {
                    distanciaMasCercana = distancia;
                    objetivoMasCercano = escudo;
                }
            }
        }

        EstablecerObjetivo(objetivoMasCercano);
        if (objetivoActual == null)
        {
            Debug.LogWarning(
                $"No hay un objetivo de tipo 2 asignado para {name}. Asigna Escudo Tipo 2 o Escudos Tipo 2.",
                this);
        }
    }

    private void ResponderAObjetoReferencia(Transform destinoTipo1, Transform destinoTipo2)
    {
        if (estaDerrotado)
        {
            return;
        }

        if (tipo == TipoHumanoide.Tipo1)
        {
            if (destinoTipo1 == null)
            {
                Debug.LogWarning($"No hay un escudo objetivo asignado para {name}; no se puede teletransportar.", this);
                return;
            }

            EstablecerObjetivo(destinoTipo1);
            Vector3 posicionTeletransporte = ObtenerPosicionConSeparacion();
            transform.SetPositionAndRotation(posicionTeletransporte, transform.rotation);
            if (rb != null)
            {
                rb.position = posicionTeletransporte;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            Physics.SyncTransforms();
            Debug.Log($"{name} ({tipo}) se teletransportó junto al objetivo {destinoTipo1.name}.", this);
        }
        else if (tipo == TipoHumanoide.Tipo2 && destinoTipo2 != null)
        {
            objetivoOrientacionTipo2 = destinoTipo2;
            Debug.Log($"{name} ({tipo}) ahora se orienta hacia {destinoTipo2.name}.", this);
        }
        else if (tipo == TipoHumanoide.Tipo2)
        {
            Debug.LogWarning($"No hay un objetivo de orientación asignado para {name}.", this);
        }
    }

    private void FixedUpdate()
    {
        if (estaDerrotado)
        {
            return;
        }

        if (objetivoActual != null)
        {
            // Calcular la posición objetivo manteniendo la altura Y del personaje para no enterrarse
            Vector3 objetivoConSeparacion = ObtenerPosicionConSeparacion();
            Vector3 posicionObjetivo = new Vector3(objetivoConSeparacion.x, transform.position.y, objetivoConSeparacion.z);

            Vector3 nuevaPosicion = Vector3.MoveTowards(transform.position, posicionObjetivo, velocidad * Time.fixedDeltaTime);
            rb.MovePosition(nuevaPosicion);

            if (tipo == TipoHumanoide.Tipo1 && objetivoActual == objetivoEsferaTipo2 &&
                Vector3.Distance(transform.position, objetivoEsferaTipo2.position) < 1.2f)
            {
                MovimientoEsfera scriptVerde = objetivoEsferaTipo2.GetComponent<MovimientoEsfera>();
                if (scriptVerde != null)
                {
                    scriptVerde.RecibirImpacto();
                }
                EstablecerObjetivo(null);
            }
        }

        Transform objetivoDeOrientacion = objetivoOrientacionTipo2 != null
            ? objetivoOrientacionTipo2
            : objetivoActual;
        if (objetivoDeOrientacion != null)
        {
            Vector3 direccion = objetivoDeOrientacion.position - transform.position;
            direccion.y = 0f;
            if (direccion.sqrMagnitude > 0.001f)
            {
                Quaternion rotacionObjetivo = Quaternion.LookRotation(direccion);
                rb.MoveRotation(Quaternion.Slerp(
                    transform.rotation,
                    rotacionObjetivo,
                    Time.fixedDeltaTime * 10f));
            }
        }
    }

    // --- COLISIONES CON EL JUGADOR Y LOS ESCUDOS ---
    private void OnTriggerEnter(Collider other)
    {
        ProcesarContacto(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        ProcesarContacto(collision.gameObject);
    }

    private void ProcesarContacto(GameObject otro)
    {
        if (EsJugador(otro))
        {
            if (tipo == TipoHumanoide.Tipo2)
            {
                OnCuboTocaTipo2?.Invoke();
            }
            else
            {
                OnCuboTocaTipo1?.Invoke();
            }

            return;
        }

        if (!estaDerrotado && EsObjetivoGrupo2(otro))
        {
            EstablecerObjetivo(null);
            CambiarColor(Color.yellow);
        }
    }

    private void EstablecerObjetivo(Transform nuevoObjetivo)
    {
        if (objetivoActual == nuevoObjetivo)
        {
            return;
        }

        if (objetivoActual != null &&
            SeguidoresPorObjetivo.TryGetValue(objetivoActual, out List<MovimientoEsfera> seguidoresActuales))
        {
            seguidoresActuales.Remove(this);
            if (seguidoresActuales.Count == 0)
            {
                SeguidoresPorObjetivo.Remove(objetivoActual);
            }
        }

        objetivoActual = nuevoObjetivo;
        if (objetivoActual == null)
        {
            return;
        }

        if (!SeguidoresPorObjetivo.TryGetValue(objetivoActual, out List<MovimientoEsfera> seguidores))
        {
            seguidores = new List<MovimientoEsfera>();
            SeguidoresPorObjetivo.Add(objetivoActual, seguidores);
        }

        if (!seguidores.Contains(this))
        {
            seguidores.Add(this);
        }
    }

    private Vector3 ObtenerPosicionConSeparacion()
    {
        if (!SeguidoresPorObjetivo.TryGetValue(objetivoActual, out List<MovimientoEsfera> seguidores))
        {
            return objetivoActual.position;
        }

        int cantidad = seguidores.Count;
        int indice = seguidores.IndexOf(this);
        if (cantidad <= 0 || indice < 0)
        {
            return objetivoActual.position;
        }

        float radio = Mathf.Max(1.5f, separacionEnObjetivo * cantidad / (2f * Mathf.PI));
        float angulo = 2f * Mathf.PI * indice / cantidad;
        Vector3 desplazamiento = new Vector3(Mathf.Cos(angulo), 0f, Mathf.Sin(angulo)) * radio;
        return objetivoActual.position + desplazamiento;
    }

    private static bool EsJugador(GameObject objeto)
    {
        for (Transform actual = objeto.transform; actual != null; actual = actual.parent)
        {
            if (actual.CompareTag("Player") ||
                actual.name.IndexOf("Jugador", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private bool EsObjetivoGrupo2(GameObject objeto)
    {
        for (Transform actual = objeto.transform; actual != null; actual = actual.parent)
        {
            if (actual.gameObject.tag == "EscudoTipo2" ||
                actual.name.IndexOf("EscudoTipo2", StringComparison.OrdinalIgnoreCase) >= 0 ||
                actual.name.IndexOf("ShieldType2", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            if (actual == escudoTipo2)
            {
                return true;
            }

            if (escudosTipo2 == null)
            {
                continue;
            }

            foreach (Transform escudo in escudosTipo2)
            {
                if (escudo != null && actual == escudo) return true;
            }
        }

        return false;
    }

    private void CambiarColor(Color color)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        MaterialPropertyBlock block = new MaterialPropertyBlock();

        foreach (Renderer renderer in renderers)
        {
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null)
                {
                    continue;
                }

                renderer.GetPropertyBlock(block, i);
                if (material.HasProperty("_BaseColor"))
                {
                    block.SetColor("_BaseColor", color);
                }
                if (material.HasProperty("_Color"))
                {
                    block.SetColor("_Color", color);
                }
                renderer.SetPropertyBlock(block, i);
            }
        }
    }

    // --- EFECTO CAÍDA FÍSICA + FADE A GRIS OSCURO ---
    public void RecibirImpacto()
    {
        if (estaDerrotado) return;
        estaDerrotado = true;
        EstablecerObjetivo(null);

        // Liberar física solo al ser derrotado
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.constraints = RigidbodyConstraints.None;
        rb.linearDamping = 3.0f;
        rb.angularDamping = 5.0f;

        StartCoroutine(TransicionAGrisOscuro());
    }

    private IEnumerator TransicionAGrisOscuro()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        Color colorDestino = new Color(0.08f, 0.08f, 0.08f, 1f);
        float duracion = 1.2f;
        float tiempo = 0f;

        Color[] coloresIniciales = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i].material.HasProperty("_BaseColor"))
                coloresIniciales[i] = renderers[i].material.GetColor("_BaseColor");
            else if (renderers[i].material.HasProperty("_Color"))
                coloresIniciales[i] = renderers[i].material.color;
        }

        while (tiempo < duracion)
        {
            tiempo += Time.deltaTime;
            float t = tiempo / duracion;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;

                Color nuevoColor = Color.Lerp(coloresIniciales[i], colorDestino, t);

                if (renderers[i].material.HasProperty("_BaseColor"))
                    renderers[i].material.SetColor("_BaseColor", nuevoColor);
                else if (renderers[i].material.HasProperty("_Color"))
                    renderers[i].material.color = nuevoColor;
            }

            yield return null;
        }
    }
}