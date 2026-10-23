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
        public bool esSostenido; // Nueva bandera
        public bool estaActiva;
        public bool estaTerminada;
        public GameObject cuboActivo;
    }

    private List<DatosNota> todasLasNotas = new List<DatosNota>();

    // Separamos los pools
    private Queue<GameObject> poolDeNotasBlancas = new Queue<GameObject>();
    private Queue<GameObject> poolDeNotasNegras = new Queue<GameObject>();

    void Start()
    {
        InicializarPools();
        GenerarCascadaVisual();
    }

    void InicializarPools()
    {
        for (int i = 0; i < tamanoPool; i++)
        {
            // Llenar pool de blancas
            GameObject objBlanca = Instantiate(prefabNotaBlanca, contenedorCascada);
            objBlanca.SetActive(false);
            poolDeNotasBlancas.Enqueue(objBlanca);

            // Llenar pool de negras
            GameObject objNegra = Instantiate(prefabNotaNegra, contenedorCascada);
            objNegra.SetActive(false);
            poolDeNotasNegras.Enqueue(objNegra);
        }
    }

    // Función auxiliar para detectar teclas negras matemáticamente
    bool EsSostenidoOBemol(int numeroMidi)
    {
        int notaEnOctava = numeroMidi % 12;
        return notaEnOctava == 1 || notaEnOctava == 3 || notaEnOctava == 6 || notaEnOctava == 8 || notaEnOctava == 10;
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
                esSostenido = EsSostenidoOBemol(nota.NoteNumber), // Detectamos el tipo de nota
                estaActiva = false,
                estaTerminada = false
            });
        }
    }

    void Update()
    {
        if (contenedorCascada != null)
        {
            contenedorCascada.Translate(Vector3.down * velocidadCaida * Time.deltaTime, Space.World);
        }

        float yMundoContenedor = contenedorCascada.position.y;

        foreach (var nota in todasLasNotas)
        {
            if (nota.estaTerminada) continue;

            float yMundoActual = yMundoContenedor + nota.posicionY;

            // A. Aparecer nota
            if (!nota.estaActiva && yMundoActual <= alturaAparicionY)
            {
                GameObject cuboReciclado;

                // Determinamos qué pool y qué prefab usar
                Queue<GameObject> poolCorrecto = nota.esSostenido ? poolDeNotasNegras : poolDeNotasBlancas;
                GameObject prefabCorrecto = nota.esSostenido ? prefabNotaNegra : prefabNotaBlanca;

                if (poolCorrecto.Count > 0)
                    cuboReciclado = poolCorrecto.Dequeue();
                else
                    cuboReciclado = Instantiate(prefabCorrecto, contenedorCascada);

                cuboReciclado.transform.localPosition = new Vector3(nota.posicionX, nota.posicionY, 0);

                // Si quieres que las notas negras sean más delgadas o de otro tamaño por defecto, puedes ajustarlo aquí
                cuboReciclado.transform.localScale = new Vector3(anchoTecla * 0.9f, nota.alturaNota, 0.01f);

                cuboReciclado.SetActive(true);

                nota.cuboActivo = cuboReciclado;
                nota.estaActiva = true;
            }

            // B. Reciclar nota al pasar el teclado
            if (nota.estaActiva && yMundoActual < alturaReciclajeY)
            {
                nota.cuboActivo.SetActive(false);

                // Regresamos el cubo a su respectivo pool
                if (nota.esSostenido)
                    poolDeNotasNegras.Enqueue(nota.cuboActivo);
                else
                    poolDeNotasBlancas.Enqueue(nota.cuboActivo);

                nota.cuboActivo = null;
                nota.estaTerminada = true;
            }
        }
    }
}