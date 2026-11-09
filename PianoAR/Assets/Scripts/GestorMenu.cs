using UnityEngine;
using TMPro;
using System.Collections; // Necesario para modificar los textos de la UI

public class GestorMenu : MonoBehaviour
{
    [Header("Paneles Principales UI")]
    public GameObject panelMenuPrincipal;
    public GameObject panelSeleccionCanciones;
    public GameObject panelJuegoAR;

    [Header("Sub-Elementos de Selección")]
    public GameObject contenedorOpcionesMano; // El sub-panel derecho que tiene los botones de mano
    public TextMeshProUGUI textoTituloCancion; // El texto arriba de los botones de mano

    [Header("Referencias Externas")]
    public MidiReaderTest lectorMidi;
    public AudioSource reproductorAudio;

    [Header("UI AR y Cuenta Regresiva")]
    public TextMeshProUGUI textoCuentaRegresiva;
    private string archivoMidiPendiente = "";
    private Coroutine rutinaCuenta;

    // Variables internas
    private string cancionSeleccionada = "";

    void Start()
    {
        MostrarMenuPrincipal();
    }

    // --- NAVEGACIÓN PRINCIPAL ---

    public void MostrarMenuPrincipal()
    {
        panelMenuPrincipal.SetActive(true);
        panelSeleccionCanciones.SetActive(false);
        panelJuegoAR.SetActive(false);

        reproductorAudio.Stop(); // Apagar cualquier música que esté sonando
        cancionSeleccionada = ""; // Limpiar selección
    }

    public void MostrarSeleccionCanciones()
    {
        panelMenuPrincipal.SetActive(false);
        panelSeleccionCanciones.SetActive(true);
        panelJuegoAR.SetActive(false);

        // Reseteamos la vista: ocultamos las opciones de mano hasta que elijan una canción
        contenedorOpcionesMano.SetActive(false);
        textoTituloCancion.text = "Selecciona una pista para escucharla";
    }

    // --- LÓGICA DEL PANEL DE SELECCIÓN ---

    // Asigna esta función a los 3 botones de tus canciones
    // En el inspector, en el campo "String", pondrás el ID de la canción (ej. "EnderLilies", "FF7")
    public void SeleccionarCancion(string idCancion)
    {
        cancionSeleccionada = idCancion;

        // 1. Mostrar las opciones de mano y actualizar el título
        contenedorOpcionesMano.SetActive(true);
        textoTituloCancion.text = "Practicando: " + idCancion;

        // 2. Reproducir la vista previa del audio
        // Asume que tienes tus MP3/WAV dentro de una carpeta llamada "Resources/Audios" en Unity
        AudioClip clipPreview = Resources.Load<AudioClip>("Audios/" + idCancion);

        if (clipPreview != null)
        {
            reproductorAudio.Stop(); // Detiene la anterior si estaban escuchando otra
            reproductorAudio.clip = clipPreview;
            reproductorAudio.pitch = 1f; // Asegurar velocidad normal
            reproductorAudio.Play();
        }
        else
        {
            Debug.LogWarning("No se encontró el audio de preview para: " + idCancion + ". Revisa la carpeta Resources/Audios.");
        }
    }

    // Asigna esta función a los botones "Mano Izquierda" y "Mano Derecha"
    // En el inspector, el parámetro string será "Izq" o "Der"
    public void IniciarPractica(string sufijoMano)
    {
        if (string.IsNullOrEmpty(cancionSeleccionada)) return;

        // Guardamos el nombre pero NO iniciamos el juego aún
        archivoMidiPendiente = cancionSeleccionada + "_" + sufijoMano + ".mid";

        panelSeleccionCanciones.SetActive(false);
        panelJuegoAR.SetActive(true);

        // Preparamos y silenciamos el audio
        reproductorAudio.Stop();
        reproductorAudio.time = 0f;

        if (textoCuentaRegresiva != null) textoCuentaRegresiva.gameObject.SetActive(false);
    }

    public void IniciarCuentaRegresiva()
    {
        if (rutinaCuenta != null) StopCoroutine(rutinaCuenta);
        rutinaCuenta = StartCoroutine(RutinaCuenta());
    }

    private IEnumerator RutinaCuenta()
    {
        textoCuentaRegresiva.gameObject.SetActive(true);

        textoCuentaRegresiva.text = "3";
        yield return new WaitForSeconds(1f);

        textoCuentaRegresiva.text = "2";
        yield return new WaitForSeconds(1f);

        textoCuentaRegresiva.text = "1";
        yield return new WaitForSeconds(1f);

        textoCuentaRegresiva.text = "¡A TOCAR!";

        // El anclaje está listo, iniciamos música y cascada
        reproductorAudio.Stop();
        reproductorAudio.time = 0f;
        reproductorAudio.Play();
        lectorMidi.IniciarCascada(archivoMidiPendiente);

        yield return new WaitForSeconds(1f);
        textoCuentaRegresiva.gameObject.SetActive(false);
    }

    public void InterrumpirPractica()
    {
        // Detiene la cuenta regresiva si estaba a medias
        if (rutinaCuenta != null) StopCoroutine(rutinaCuenta);
        if (textoCuentaRegresiva != null) textoCuentaRegresiva.gameObject.SetActive(false);

        // Apaga la música y resetea las notas
        reproductorAudio.Stop();
        lectorMidi.DetenerCascada();
    }

    // --- CONTROL DE VELOCIDAD (Para el PanelJuegoAR) ---

    public void CambiarVelocidadJuego(float multiplicador)
    {
        lectorMidi.CambiarVelocidadCaida(multiplicador);
        reproductorAudio.pitch = multiplicador;
    }

    // Función para el botón "Salir de la App" en el Menú Principal
    public void SalirAplicacion()
    {
        Application.Quit();
    }
    public void SalirDeAR()
    {
        InterrumpirPractica();
        MostrarMenuPrincipal();
    }
}