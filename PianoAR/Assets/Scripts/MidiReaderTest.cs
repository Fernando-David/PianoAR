using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using UnityEngine.Networking;
using System.Collections;
using TMPro;

public class MidiReaderTest : MonoBehaviour
{
    [Header("Configuración de Archivo")]
    public string nombreArchivoMidi = "EnderLilies.mid";

    [Header("Objetos Visuales")]
    public GameObject prefabNotaBlanca;
    public GameObject prefabNotaNegra;
    public Transform contenedorCascada;

    [Header("Ajustes de Cascada y Tiempo")]
    public float velocidadCaida = 2f;
    public float anchoTecla = 0.024f;
    public float tiempoGraciaSegundos = 3f; // 3 segundos de anticipación al inicio

    [Header("Configuración del Pool")]
    public int tamanoPool = 40;
    public float alturaAparicionY = 15f;

    private class DatosNota
    {
        public float posicionX;
        public float yInferiorOriginal; // Posición donde inicia la base de la nota
        public float alturaOriginal;    // Largo total de la nota
        public bool esSostenido;
        public bool estaActiva;
        public bool estaTerminada;
        public GameObject cuboActivo;
        public string letraNota;
    }

    private List<DatosNota> todasLasNotas = new List<DatosNota>();
    private Queue<GameObject> poolDeNotasBlancas = new Queue<GameObject>();
    private Queue<GameObject> poolDeNotasNegras = new Queue<GameObject>();

    private float velocidadOriginal;
    private float distanciaRecorrida = 0f;
    private bool cascadaEnMovimiento = false;

    void Start()
    {
        velocidadOriginal = velocidadCaida;
        InicializarPools();
    }

    void InicializarPools()
    {
        for (int i = 0; i < tamanoPool; i++)
        {
            GameObject objBlanca = Instantiate(prefabNotaBlanca, contenedorCascada);
            objBlanca.SetActive(false);
            poolDeNotasBlancas.Enqueue(objBlanca);

            GameObject objNegra = Instantiate(prefabNotaNegra, contenedorCascada);
            objNegra.SetActive(false);
            poolDeNotasNegras.Enqueue(objNegra);
        }
    }

    bool EsSostenidoOBemol(int numeroMidi)
    {
        int notaEnOctava = numeroMidi % 12;
        return notaEnOctava == 1 || notaEnOctava == 3 || notaEnOctava == 6 || notaEnOctava == 8 || notaEnOctava == 10;
    }

    float ObtenerPosicionXReal(int numeroMidi)
    {
        int distanciaMidi = numeroMidi - 60;
        int octavas = Mathf.FloorToInt((float)distanciaMidi / 12f);
        int notaEnOctava = distanciaMidi - (octavas * 12);

        float[] posiciones = new float[12] { 0f, 0.5f, 1f, 1.5f, 2f, 3f, 3.5f, 4f, 4.5f, 5f, 5.5f, 6f };
        float anchoOctavaFisica = 7f * anchoTecla;

        return (octavas * anchoOctavaFisica) + (posiciones[notaEnOctava] * anchoTecla);
    }

    public void ProcesarMidiFisico(string rutaArchivo)
    {
        if (!File.Exists(rutaArchivo))
        {
            Debug.LogError("No se encontró el archivo MIDI en la ruta: " + rutaArchivo);
            return;
        }

        MidiFile archivoMidi = MidiFile.Read(rutaArchivo);
        TempoMap mapaTempo = archivoMidi.GetTempoMap();
        var notasMidi = archivoMidi.GetNotes().ToList();

        // Desfase espacial equivalente a los 3 segundos de gracia iniciales
        float offsetGraciaEspacial = tiempoGraciaSegundos * velocidadOriginal;

        foreach (var nota in notasMidi)
        {
            float tiempoInicio = (float)nota.TimeAs<MetricTimeSpan>(mapaTempo).TotalMicroseconds / 1000000f;
            float duracion = (float)nota.LengthAs<MetricTimeSpan>(mapaTempo).TotalMicroseconds / 1000000f;

            float xCalculado = ObtenerPosicionXReal(nota.NoteNumber);
            float alturaCalculada = duracion * velocidadOriginal;

            // Guardamos dónde empieza el borde inferior de la nota sumando los 3 segundos de gracia
            float yBaseCalculado = (tiempoInicio * velocidadOriginal) + offsetGraciaEspacial;

            string[] nombresNotas = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
            string letraTraducida = nombresNotas[nota.NoteNumber % 12];

            todasLasNotas.Add(new DatosNota
            {
                posicionX = xCalculado,
                yInferiorOriginal = yBaseCalculado,
                alturaOriginal = alturaCalculada,
                esSostenido = EsSostenidoOBemol(nota.NoteNumber),
                estaActiva = false,
                estaTerminada = false,
                letraNota = letraTraducida
            });
        }

        cascadaEnMovimiento = true;
    }

    void Update()
    {
        if (!cascadaEnMovimiento || contenedorCascada == null) return;

        // Acumulamos cuánto han bajado las notas en lugar de mover todo el contenedor padre
        distanciaRecorrida += velocidadCaida * Time.deltaTime;

        foreach (var nota in todasLasNotas)
        {
            if (nota.estaTerminada) continue;

            // Posición actual de la base (borde inferior) y la punta superior respecto a la línea de golpe (Y = 0)
            float yInferiorActual = nota.yInferiorOriginal - distanciaRecorrida;
            float ySuperiorActual = yInferiorActual + nota.alturaOriginal;

            // 1. ACTIVACIÓN DESDE EL POOL: Cuando el borde inferior entra al rango de visión
            if (!nota.estaActiva && yInferiorActual <= alturaAparicionY)
            {
                GameObject cuboReciclado;
                Queue<GameObject> poolCorrecto = nota.esSostenido ? poolDeNotasNegras : poolDeNotasBlancas;
                GameObject prefabCorrecto = nota.esSostenido ? prefabNotaNegra : prefabNotaBlanca;

                if (poolCorrecto.Count > 0)
                    cuboReciclado = poolCorrecto.Dequeue();
                else
                    cuboReciclado = Instantiate(prefabCorrecto, contenedorCascada);

                TextMeshPro texto = cuboReciclado.GetComponentInChildren<TextMeshPro>();
                if (texto != null)
                {
                    texto.text = nota.letraNota;
                    // El texto se queda fijo a 2cm de la base de la nota
                    texto.transform.localPosition = new Vector3(0f, 0.02f, -0.03f);
                }

                cuboReciclado.SetActive(true);
                nota.cuboActivo = cuboReciclado;
                nota.estaActiva = true;
            }

            // 2. ACTUALIZACIÓN Y ESCALADO EN LA LÍNEA DE GOLPE (Y = 0)
            if (nota.estaActiva && nota.cuboActivo != null)
            {
                // Si la parte superior ya llegó a 0 (la nota se consumió por completo), la reciclamos
                if (ySuperiorActual <= 0f)
                {
                    nota.cuboActivo.SetActive(false);
                    if (nota.esSostenido)
                        poolDeNotasNegras.Enqueue(nota.cuboActivo);
                    else
                        poolDeNotasBlancas.Enqueue(nota.cuboActivo);

                    nota.cuboActivo = null;
                    nota.estaActiva = false;
                    nota.estaTerminada = true;
                    continue;
                }

                float posYBase;
                float alturaVisible;

                if (yInferiorActual > 0f)
                {
                    // Aún no toca la línea de golpe: baja normal con su tamaño completo
                    posYBase = yInferiorActual;
                    alturaVisible = nota.alturaOriginal;
                }
                else
                {
                    // Ya tocó la línea de golpe (yInferiorActual <= 0):
                    // Se clava su base en Y = 0 y su altura se reduce a lo que falta por consumirse
                    posYBase = 0f;
                    alturaVisible = ySuperiorActual;
                }

                // Colocamos el objeto padre exactamente en el borde inferior de la nota
                nota.cuboActivo.transform.localPosition = new Vector3(nota.posicionX, posYBase, -0.02f);

                // Ajustamos el cubo visual hijo para que crezca desde Y=0 hacia arriba (pivote en la base)
                Transform cuboVisual = nota.cuboActivo.transform.GetChild(0);
                cuboVisual.localScale = new Vector3(anchoTecla * 0.9f, alturaVisible, 0.03f);
                cuboVisual.localPosition = new Vector3(0f, alturaVisible / 2f, 0f);
            }
        }
    }

    // --- CONTROL DE INTERFAZ (BOTONES DE VELOCIDAD) ---

    public void EstablecerVelocidad25() { velocidadCaida = velocidadOriginal * 0.25f; }
    public void EstablecerVelocidad50() { velocidadCaida = velocidadOriginal * 0.5f; }
    public void EstablecerVelocidad75() { velocidadCaida = velocidadOriginal * 0.75f; }
    public void EstablecerVelocidad100() { velocidadCaida = velocidadOriginal; }

    public void CambiarVelocidadCaida(float multiplicador)
    {
        velocidadCaida = velocidadOriginal * multiplicador;
    }

    public void IniciarCascada(string nombreArchivo)
    {
        DetenerCascada();

        nombreArchivoMidi = nombreArchivo;
        distanciaRecorrida = 0f;

        if (contenedorCascada != null)
        {
            contenedorCascada.gameObject.SetActive(true);
            contenedorCascada.localPosition = Vector3.zero;
        }

        StartCoroutine(ExtraerYLeerMidi(nombreArchivo));
    }

    public void DetenerCascada()
    {
        cascadaEnMovimiento = false;

        foreach (var nota in todasLasNotas)
        {
            if (nota.estaActiva && nota.cuboActivo != null)
            {
                nota.cuboActivo.SetActive(false);

                if (nota.esSostenido)
                    poolDeNotasNegras.Enqueue(nota.cuboActivo);
                else
                    poolDeNotasBlancas.Enqueue(nota.cuboActivo);

                nota.cuboActivo = null;
                nota.estaActiva = false;
            }
        }

        todasLasNotas.Clear();

        if (contenedorCascada != null)
        {
            contenedorCascada.gameObject.SetActive(false);
        }
    }

    // --- EXTRACCIÓN PARA ANDROID ---
    public IEnumerator ExtraerYLeerMidi(string nombreArchivo)
    {
        string rutaStreaming = Path.Combine(Application.streamingAssetsPath, nombreArchivo);
        string rutaPersistente = Path.Combine(Application.persistentDataPath, nombreArchivo);

        if (rutaStreaming.Contains("://") || rutaStreaming.Contains(":///"))
        {
            using (UnityWebRequest www = UnityWebRequest.Get(rutaStreaming))
            {
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    File.WriteAllBytes(rutaPersistente, www.downloadHandler.data);
                    ProcesarMidiFisico(rutaPersistente);
                }
                else
                {
                    Debug.LogError("Error extrayendo MIDI: " + www.error);
                }
            }
        }
        else
        {
            ProcesarMidiFisico(rutaStreaming);
        }
    }
}