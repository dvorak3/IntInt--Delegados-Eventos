using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[ExecuteAlways]
[RequireComponent(typeof(Rigidbody))]
public class ControlJugador : MonoBehaviour
{
    public float velocidad = 8.0f;

    private Rigidbody rb;
    private Vector2 direccionMovimiento;
    private int puntuacion;
    private int siguienteUmbralRecompensa = 100;
    private Text textoPuntuacion;
    private Text textoRecompensa;
    private Coroutine ocultarRecompensa;
    private Transform camara;
    private Vector3 desplazamientoCamara;

    private void Awake()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        rb = GetComponent<Rigidbody>();
        rb.constraints |= RigidbodyConstraints.FreezeRotation;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    private void OnEnable()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorApplication.delayCall += PrepararEscenaEnEditor;
        }
#endif
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        EditorApplication.delayCall -= PrepararEscenaEnEditor;
#endif
    }

    private void Start()
    {
        CrearElementosDeEscena();
        CrearInterfaz();
        if (Camera.main != null)
        {
            camara = Camera.main.transform;
            desplazamientoCamara = camara.position - transform.position;
        }
    }

    private void AsignarEscudoObjetivoTipo1()
    {
        ObjetoReferenciaEvento evento = FindAnyObjectByType<ObjetoReferenciaEvento>();
        if (evento == null || evento.escudoObjetivoTipo1 != null)
        {
            return;
        }

        GameObject escudoFijado = GameObject.Find("EscudoTipo1_1");
        EscudoRecolectable recolectable = escudoFijado != null
            ? escudoFijado.GetComponent<EscudoRecolectable>()
            : null;
        if (recolectable != null && recolectable.tipo == EscudoRecolectable.TipoEscudo.Tipo1)
        {
            evento.escudoObjetivoTipo1 = recolectable.transform;
            return;
        }

        Debug.LogWarning("No se encontró EscudoTipo1_1 para asignarlo como objetivo de los Legion.", this);
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        float horizontal = 0f;
        float vertical = 0f;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) vertical += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) vertical -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) horizontal += 1f;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) horizontal -= 1f;
        }

        direccionMovimiento = Vector2.ClampMagnitude(new Vector2(horizontal, vertical), 1f);
    }

    private void FixedUpdate()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        Vector3 velocidadActual = rb.linearVelocity;
        Vector3 velocidadHorizontal = new Vector3(direccionMovimiento.x, 0f, direccionMovimiento.y) * velocidad;
        rb.linearVelocity = new Vector3(velocidadHorizontal.x, velocidadActual.y, velocidadHorizontal.z);

        if (direccionMovimiento.sqrMagnitude > 0.001f)
        {
            Vector3 direccion = new Vector3(direccionMovimiento.x, 0f, direccionMovimiento.y);
            rb.MoveRotation(Quaternion.LookRotation(direccion));
        }
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (camara != null)
        {
            camara.position = transform.position + desplazamientoCamara;
        }
    }

    public void SumarPuntos(int puntos)
    {
        if (puntos <= 0)
        {
            Debug.LogWarning($"Se ignoró una cantidad de puntos no válida: {puntos}.", this);
            return;
        }

        puntuacion += puntos;
        Debug.Log($"Puntuación: {puntuacion}", this);
        ActualizarTextoPuntuacion();

        while (puntuacion >= siguienteUmbralRecompensa)
        {
            MostrarRecompensa(siguienteUmbralRecompensa);
            siguienteUmbralRecompensa += 100;
        }
    }

    private void CrearInterfaz()
    {
        GameObject canvasObject = GameObject.Find("CanvasPuntuacion");
        Canvas canvas;
        if (canvasObject == null)
        {
            canvasObject = new GameObject("CanvasPuntuacion");
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObject.AddComponent<GraphicRaycaster>();
        }
        else
        {
            canvas = canvasObject.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = canvasObject.AddComponent<Canvas>();
            }
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }
        canvas.enabled = true;

        if (canvasObject.transform.parent != null)
        {
            canvasObject.transform.SetParent(null, true);
#if UNITY_EDITOR
            if (!Application.isPlaying && canvasObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(canvasObject.scene);
            }
#endif
        }

        Transform scoreTransform = canvas.transform.Find("Puntuacion");
        textoPuntuacion = scoreTransform != null
            ? scoreTransform.GetComponent<Text>()
            : CrearTexto(canvas.transform, "Puntuacion", new Vector2(20f, -20f), 28);
        textoPuntuacion.gameObject.SetActive(true);
        Transform rewardTransform = canvas.transform.Find("Recompensa");
        textoRecompensa = rewardTransform != null
            ? rewardTransform.GetComponent<Text>()
            : CrearTexto(canvas.transform, "Recompensa", new Vector2(20f, -62f), 24);
        textoRecompensa.color = new Color(1f, 0.82f, 0.2f);
        textoRecompensa.gameObject.SetActive(false);
        ActualizarTextoPuntuacion();
    }

    private static Text CrearTexto(Transform padre, string nombre, Vector2 posicion, int tamano)
    {
        GameObject textoObject = new GameObject(nombre, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textoObject.transform.SetParent(padre, false);

        RectTransform rect = textoObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = posicion;
        rect.sizeDelta = new Vector2(520f, 42f);

        Text texto = textoObject.GetComponent<Text>();
        texto.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        texto.fontSize = tamano;
        texto.fontStyle = FontStyle.Bold;
        texto.color = Color.white;
        texto.alignment = TextAnchor.MiddleLeft;
        texto.horizontalOverflow = HorizontalWrapMode.Overflow;
        texto.verticalOverflow = VerticalWrapMode.Overflow;
        return texto;
    }

    private void ActualizarTextoPuntuacion()
    {
        if (textoPuntuacion != null)
        {
            textoPuntuacion.text = $"Puntuación: {puntuacion}";
        }
    }

    private void MostrarRecompensa(int umbral)
    {
        string mensaje = $"¡Recompensa desbloqueada al alcanzar {umbral} puntos!";
        Debug.Log(mensaje, this);

        if (textoRecompensa == null)
        {
            return;
        }

        textoRecompensa.text = mensaje;
        textoRecompensa.gameObject.SetActive(true);
        if (ocultarRecompensa != null)
        {
            StopCoroutine(ocultarRecompensa);
        }
        ocultarRecompensa = StartCoroutine(OcultarRecompensa());
    }

    private IEnumerator OcultarRecompensa()
    {
        yield return new WaitForSeconds(4f);
        if (textoRecompensa != null)
        {
            textoRecompensa.gameObject.SetActive(false);
        }
        ocultarRecompensa = null;
    }

    private void CrearElementosDeEscena()
    {
        CrearObjetosDeReferencia();
        if (FindAnyObjectByType<EscudoRecolectable>() == null)
        {
            CrearEscudosRecolectables();
        }
        ConfigurarEscudosRecolectables();
        AsignarEscudoObjetivoTipo1();
        AsignarEscudosTipo2();
    }

#if UNITY_EDITOR
    private void PrepararEscenaEnEditor()
    {
        if (this == null || !isActiveAndEnabled || Application.isPlaying || !gameObject.scene.IsValid())
        {
            return;
        }

        CrearElementosDeEscena();
        CrearInterfaz();
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }
#endif

    private void CrearObjetosDeReferencia()
    {
        ObjetoReferenciaEvento evento = FindAnyObjectByType<ObjetoReferenciaEvento>();
        GameObject referencia = evento != null
            ? evento.gameObject
            : GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Vector3 direccionFrontal = transform.forward;
        direccionFrontal.y = 0f;
        if (direccionFrontal.sqrMagnitude < 0.001f)
        {
            direccionFrontal = Vector3.forward;
        }
        direccionFrontal.Normalize();

        Vector3 posicionReferencia = transform.position + direccionFrontal * 8f;
        posicionReferencia.y = ObtenerAlturaTerreno(posicionReferencia) + 0.75f;
        referencia.name = "ObjetoReferencia";
        referencia.transform.position = posicionReferencia;
        referencia.transform.rotation = Quaternion.identity;
        referencia.transform.localScale = new Vector3(1.2f, 0.75f, 1.2f);
        referencia.GetComponent<Renderer>().enabled = true;
        Collider colliderCilindro = referencia.GetComponent<Collider>();
        if (colliderCilindro != null)
        {
            colliderCilindro.enabled = false;
        }
        referencia.GetComponent<Renderer>().sharedMaterial = CrearMaterial(new Color(1f, 0.25f, 0.02f));
        SphereCollider zona = referencia.GetComponent<SphereCollider>();
        if (zona == null)
        {
            zona = referencia.AddComponent<SphereCollider>();
        }
        zona.isTrigger = true;
        zona.radius = 3f;

        Vector3 posicionOrientacion = posicionReferencia +
            Quaternion.Euler(0f, 90f, 0f) * direccionFrontal * 6f;
        if (evento != null && evento.escudoObjetivoTipo1 != null &&
            evento.escudoObjetivoTipo1.name == "EscudoObjetivoTipo1")
        {
            GameObject marcadorObsoleto = evento.escudoObjetivoTipo1.gameObject;
            evento.escudoObjetivoTipo1 = null;
            if (Application.isPlaying)
            {
                Destroy(marcadorObsoleto);
            }
            else
            {
                DestroyImmediate(marcadorObsoleto);
            }
        }

        GameObject objetivoOrientacion = evento != null && evento.objetivoOrientacionTipo2 != null
            ? evento.objetivoOrientacionTipo2.gameObject
            : CrearMarcador("ObjetivoOrientacionTipo2", posicionOrientacion,
                Vector3.one, new Color(0.2f, 0.9f, 0.4f));
        objetivoOrientacion.transform.position = posicionOrientacion;

        if (evento == null)
        {
            evento = referencia.AddComponent<ObjetoReferenciaEvento>();
        }
        evento.objetivoOrientacionTipo2 = objetivoOrientacion.transform;
    }

    private void CrearEscudosRecolectables()
    {
        for (int i = 0; i < 20; i++)
        {
            float angulo = i * Mathf.PI * 2f / 20f;
            float radio = 4f + (i % 5) * 1.7f;
            Vector3 desplazamiento = new Vector3(Mathf.Cos(angulo), 0f, Mathf.Sin(angulo)) * radio;
            Vector3 posicion = transform.position + desplazamiento;
            posicion.y = ObtenerAlturaTerreno(posicion) + 0.5f;

            bool esTipo1 = i < 10;
            GameObject escudo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            escudo.name = esTipo1 ? $"EscudoTipo1_{i + 1}" : $"EscudoTipo2_{i - 9}";
            escudo.transform.SetPositionAndRotation(posicion, Quaternion.Euler(0f, -angulo * Mathf.Rad2Deg, 0f));
            escudo.transform.localScale = new Vector3(1.3f, 1.7f, 0.3f);
            escudo.GetComponent<Renderer>().sharedMaterial = CrearMaterial(
                esTipo1 ? new Color(0.2f, 0.65f, 1f) : new Color(0.95f, 0.72f, 0.18f));

            BoxCollider collider = escudo.GetComponent<BoxCollider>();
            collider.isTrigger = true;
            Rigidbody escudoRb = escudo.AddComponent<Rigidbody>();
            escudoRb.isKinematic = true;
            escudoRb.useGravity = false;

            EscudoRecolectable recolectable = escudo.AddComponent<EscudoRecolectable>();
            recolectable.tipo = esTipo1
                ? EscudoRecolectable.TipoEscudo.Tipo1
                : EscudoRecolectable.TipoEscudo.Tipo2;
        }
    }

    private void ConfigurarEscudosRecolectables()
    {
        EscudoRecolectable[] escudos = FindObjectsByType<EscudoRecolectable>();
        foreach (EscudoRecolectable escudo in escudos)
        {
            string[] partesNombre = escudo.name.Split('_');
            if (partesNombre.Length != 2 || !int.TryParse(partesNombre[1], out int numero))
            {
                continue;
            }

            bool esTipo1 = escudo.tipo == EscudoRecolectable.TipoEscudo.Tipo1;
            int indice = esTipo1 ? numero - 1 : numero + 9;
            if (indice < 0 || indice >= 20)
            {
                continue;
            }

            float angulo = indice * Mathf.PI * 2f / 20f;
            float radio = 6f + (indice % 3) * 1.4f;
            Vector3 desplazamiento = new Vector3(Mathf.Cos(angulo), 0f, Mathf.Sin(angulo)) * radio;
            Vector3 posicion = transform.position + desplazamiento;
            posicion.y = ObtenerAlturaTerreno(posicion) + 0.85f;

            escudo.transform.SetPositionAndRotation(
                posicion,
                Quaternion.Euler(0f, -angulo * Mathf.Rad2Deg, 0f));
            escudo.transform.localScale = new Vector3(1.3f, 1.7f, 0.3f);
            Renderer renderer = escudo.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.enabled = true;
                renderer.sharedMaterial = CrearMaterial(
                    esTipo1 ? new Color(0.2f, 0.65f, 1f) : new Color(0.95f, 0.72f, 0.18f));
            }
        }
    }

    private static float ObtenerAlturaTerreno(Vector3 posicion)
    {
        Terrain terreno = Terrain.activeTerrain;
        if (terreno == null)
        {
            terreno = FindAnyObjectByType<Terrain>();
        }

        if (terreno != null)
        {
            return terreno.SampleHeight(posicion) + terreno.transform.position.y;
        }

        Ray rayo = new Ray(new Vector3(posicion.x, posicion.y + 500f, posicion.z), Vector3.down);
        if (Physics.Raycast(rayo, out RaycastHit impacto, 1000f))
        {
            return impacto.point.y;
        }

        return posicion.y;
    }

    private void AsignarEscudosTipo2()
    {
        EscudoRecolectable[] escudos = FindObjectsByType<EscudoRecolectable>();
        MovimientoEsfera[] humanoides = FindObjectsByType<MovimientoEsfera>();
        foreach (MovimientoEsfera humanoide in humanoides)
        {
            if (humanoide.tipo != MovimientoEsfera.TipoHumanoide.Tipo1)
            {
                continue;
            }

            var destinos = new System.Collections.Generic.List<Transform>();
            if (humanoide.escudoTipo2 != null)
            {
                destinos.Add(humanoide.escudoTipo2);
            }
            if (humanoide.escudosTipo2 != null)
            {
                foreach (Transform destino in humanoide.escudosTipo2)
                {
                    if (destino != null && !destinos.Contains(destino))
                    {
                        destinos.Add(destino);
                    }
                }
            }

            foreach (EscudoRecolectable escudo in escudos)
            {
                if (escudo.tipo == EscudoRecolectable.TipoEscudo.Tipo2 &&
                    !destinos.Contains(escudo.transform))
                {
                    destinos.Add(escudo.transform);
                }
            }

            humanoide.escudosTipo2 = destinos.ToArray();
        }
    }

    private static GameObject CrearMarcador(string nombre, Vector3 posicion, Vector3 escala, Color color)
    {
        GameObject marcador = GameObject.CreatePrimitive(PrimitiveType.Cube);
        marcador.name = nombre;
        marcador.transform.position = posicion;
        marcador.transform.localScale = escala;
        marcador.GetComponent<Collider>().enabled = false;
        marcador.GetComponent<Renderer>().sharedMaterial = CrearMaterial(color);
        return marcador;
    }

    private static Material CrearMaterial(Color color)
    {
#if UNITY_EDITOR
        bool esNaranja = color.r > 0.8f && color.g < 0.4f;
        string rutaMaterial = esNaranja
            ? null
            : color.r > 0.8f && color.g > 0.65f
                ? "Assets/Colours/YELLOW.mat"
                : color.g > color.r && color.g > color.b
                    ? "Assets/Colours/GREEN.mat"
                    : color.b > color.r && color.b >= color.g
                        ? "Assets/Colours/BLUE (escudo).mat"
                        : "Assets/Colours/RED.mat";
        Material materialProyecto = rutaMaterial != null
            ? AssetDatabase.LoadAssetAtPath<Material>(rutaMaterial)
            : null;
        if (materialProyecto != null)
        {
            return materialProyecto;
        }
#endif

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        if (shader == null)
        {
            Debug.LogError("No se encontró un shader compatible para los escudos de la escena.");
            return null;
        }

        Material material = new Material(shader);
        material.color = color;
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }
        return material;
    }
}
