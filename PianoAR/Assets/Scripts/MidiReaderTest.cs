using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using UnityEngine.Networking;
using System.Collections;

public class MidiReaderTest : MonoBehaviour
{
    [Header("Configuración de Archivo")]
    public string nombreArchivoMidi = "EnderLilies.mid";

    [Header("Objetos Visuales")]
    public GameObject prefabNotaBlanca;
    public GameObject prefabNotaNegra;
    public Transform contenedorCascada;

    [Header("Ajustes de Cascada")]
    public float velocidadCaida = 2f;
    public float anchoTecla = 0.024f;

    [Header("Configuración del Pool")]
    public int tamanoPool = 40;
    public float alturaAparicionY = 15f;
    public float alturaReciclajeY = -2f;

    private class DatosNota
    {
        public float posicionX;
        public float posicionY;
        public float alturaNota;
        public bool esSostenido;
        public bool estaActiva;
        public bool estaTerminada;
        public GameObject cuboActivo;
    }

    private List<DatosNota> todasLasNotas = new List<DatosNota>();
    private Queue<GameObject> poolDeNotasBlancas = new Queue<GameObject>();
    private Queue<GameObject> poolDeNotasNegras = new Queue<GameObject>();

    private float velocidadOriginal;

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

        foreach (var nota in notasMidi)
        {
            float tiempoInicio = (float)nota.TimeAs<MetricTimeSpan>(mapaTempo).TotalMicroseconds / 1000000f;
            float duracion = (float)nota.LengthAs<MetricTimeSpan>(mapaTempo).TotalMicroseconds / 1000000f;

            float xCalculado = ObtenerPosicionXReal(nota.NoteNumber);
            float alturaCalculada = duracion * velocidadOriginal;
            float yCalculado = (tiempoInicio * velocidadOriginal) + (alturaCalculada / 2f);

            todasLasNotas.Add(new DatosNota
            {
                posicionX = xCalculado,
                posicionY = yCalculado,
                alturaNota = alturaCalculada,
                esSostenido = EsSostenidoOBemol(nota.NoteNumber),
                estaActiva = false,
                estaTerminada = false
            });
        }
    }

    void Update()
    {
        if (contenedorCascada != null)
        {
            contenedorCascada.Translate(Vector3.down * velocidadCaida * Time.deltaTime, Space.Self);
        }

          float yLocalContenedor = contenedorCascada.localPosition.y;
        
        foreach (var nota in todasLasNotas)
        {
            if (nota.estaTerminada) continue;

            float yLocalActual = yLocalContenedor + nota.posicionY;

            if (!nota.estaActiva && yLocalActual <= alturaAparicionY)
            {
                GameObject cuboReciclado;
                Queue<GameObject> poolCorrecto = nota.esSostenido ? poolDeNotasNegras : poolDeNotasBlancas;
                GameObject prefabCorrecto = nota.esSostenido ? prefabNotaNegra : prefabNotaBlanca;

                if (poolCorrecto.Count > 0)
                    cuboReciclado = poolCorrecto.Dequeue();
                else
                    cuboReciclado = Instantiate(prefabCorrecto, contenedorCascada);

                cuboReciclado.transform.localPosition = new Vector3(nota.posicionX, nota.posicionY, -0.02f);
                cuboReciclado.transform.localScale = new Vector3(anchoTecla * 0.9f, nota.alturaNota, 0.03f);

                cuboReciclado.SetActive(true);
                nota.cuboActivo = cuboReciclado;
                nota.estaActiva = true;
            }

            if (nota.estaActiva && yLocalActual < alturaReciclajeY)
            {
                nota.cuboActivo.SetActive(false);
                if (nota.esSostenido)
                    poolDeNotasNegras.Enqueue(nota.cuboActivo);
                else
                    poolDeNotasBlancas.Enqueue(nota.cuboActivo);

                nota.cuboActivo = null;
                nota.estaTerminada = true;
            }
        }
    }

    // --- CONTROL DE INTERFAZ (BOTONES) ---

    public void EstablecerVelocidad25()
    {
        velocidadCaida = velocidadOriginal * 0.25f;
    }

    public void EstablecerVelocidad50()
    {
        velocidadCaida = velocidadOriginal * 0.5f;
    }

    public void EstablecerVelocidad75()
    {
        velocidadCaida = velocidadOriginal * 0.75f;
    }

    public void EstablecerVelocidad100()
    {
        velocidadCaida = velocidadOriginal;
    }

    public void IniciarCascada(string nombreArchivo)
    {
        nombreArchivoMidi = nombreArchivo;
        todasLasNotas.Clear();

        if (contenedorCascada != null)
        {
            contenedorCascada.gameObject.SetActive(true);
            contenedorCascada.localPosition = Vector3.zero;
        }

        StartCoroutine(ExtraerYLeerMidi(nombreArchivo));
    }

    public void CambiarVelocidadCaida(float multiplicador)
    {
        velocidadCaida = velocidadOriginal * multiplicador;
    }

    public void DetenerCascada()
    {
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