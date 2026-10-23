using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

public class MidiReaderTest : MonoBehaviour
{
    [Header("Configuración de Archivo")]
    public string nombreArchivoMidi = "EnderLilies.mid";

    [Header("Objetos Visuales")]
    public GameObject prefabNota;
    public Transform contenedorCascada;

    [Header("Ajustes de Cascada")]
    public float velocidadCaida = 2f;
    public float anchoTecla = 0.024f;

    [Header("Configuración del Pool")]
    public int tamanoPool = 40;
    public float alturaAparicionY = 15f;
    public float alturaReciclajeY = -2f;

    // Estructura para guardar los cálculos de cada nota
    private class DatosNota
    {
        public float posicionX;
        public float posicionY;
        public float alturaNota;
        public bool estaActiva;
        public bool estaTerminada;
        public GameObject cuboActivo;
    }

    private List<DatosNota> todasLasNotas = new List<DatosNota>();
    private Queue<GameObject> poolDeNotas = new Queue<GameObject>();

    void Start()
    {
        InicializarPool();
        GenerarCascadaVisual();
    }

    void InicializarPool()
    {
        // Creamos los cubos apagados al inicio
        for (int i = 0; i < tamanoPool; i++)
        {
            GameObject obj = Instantiate(prefabNota, contenedorCascada);
            obj.SetActive(false);
            poolDeNotas.Enqueue(obj);
        }
    }

    void GenerarCascadaVisual()
    {
        string rutaArchivo = Path.Combine(Application.streamingAssetsPath, nombreArchivoMidi);

        if (!File.Exists(rutaArchivo))
        {
            Debug.LogError("No se encontró el archivo MIDI.");
            return;
        }

        MidiFile archivoMidi = MidiFile.Read(rutaArchivo);
        TempoMap mapaTempo = archivoMidi.GetTempoMap();
        var notasMidi = archivoMidi.GetNotes().ToList();

        foreach (var nota in notasMidi)
        {
            float tiempoInicio = (float)nota.TimeAs<MetricTimeSpan>(mapaTempo).TotalMicroseconds / 1000000f;
            float duracion = (float)nota.LengthAs<MetricTimeSpan>(mapaTempo).TotalMicroseconds / 1000000f;

            float xCalculado = (nota.NoteNumber - 60) * anchoTecla;
            float alturaCalculada = duracion * velocidadCaida;
            float yCalculado = (tiempoInicio * velocidadCaida) + (alturaCalculada / 2f);

            todasLasNotas.Add(new DatosNota
            {
                posicionX = xCalculado,
                posicionY = yCalculado,
                alturaNota = alturaCalculada,
                estaActiva = false,
                estaTerminada = false
            });
        }

        Debug.Log($"¡Cascada calculada! {todasLasNotas.Count} notas listas para ser procesadas por el Pool.");
    }

    void Update()
    {
        // Trasladar el contenedor completo
        if (contenedorCascada != null)
        {
            contenedorCascada.Translate(Vector3.down * velocidadCaida * Time.deltaTime, Space.World);
        }

        float yMundoContenedor = contenedorCascada.position.y;

        foreach (var nota in todasLasNotas)
        {
            // Ignorar notas recicladas
            if (nota.estaTerminada) continue;

            float yMundoActual = yMundoContenedor + nota.posicionY;

            // A. Aparecer nota
            if (!nota.estaActiva && yMundoActual <= alturaAparicionY)
            {
                GameObject cuboReciclado;

                // Extraer del pool o instanciar de emergencia
                if (poolDeNotas.Count > 0)
                    cuboReciclado = poolDeNotas.Dequeue();
                else
                    cuboReciclado = Instantiate(prefabNota, contenedorCascada);

                cuboReciclado.transform.localPosition = new Vector3(nota.posicionX, nota.posicionY, 0);
                cuboReciclado.transform.localScale = new Vector3(anchoTecla * 0.9f, nota.alturaNota, 0.01f);

                cuboReciclado.SetActive(true);

                nota.cuboActivo = cuboReciclado;
                nota.estaActiva = true;
            }

            // B. Reciclar nota al pasar el teclado
            if (nota.estaActiva && yMundoActual < alturaReciclajeY)
            {
                nota.cuboActivo.SetActive(false);
                poolDeNotas.Enqueue(nota.cuboActivo);
                nota.cuboActivo = null;
                nota.estaTerminada = true;
            }
        }
    }
}