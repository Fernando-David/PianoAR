using UnityEngine;
using TMPro;
using System.Collections; 
public class GestorMenu : MonoBehaviour
{
    [Header("Paneles Principales UI")]
    public GameObject panelMenuPrincipal;
    public GameObject panelSeleccionCanciones;
    public GameObject panelJuegoAR;

    [Header("Sub-Elementos de Selección")]
    public GameObject contenedorOpcionesMano; 
    public TextMeshProUGUI textoTituloCancion; 

    [Header("Referencias Externas")]
    public MidiReaderTest lectorMidi;
    public AudioSource reproductorAudio;

    [Header("UI AR y Cuenta Regresiva")]
    public TextMeshProUGUI textoCuentaRegresiva;
    private string archivoMidiPendiente = "";
    private Coroutine rutinaCuenta;

    [Header("Nuevos Paneles y AR")]
    public GameObject panelDeCalibracion; // Arrastra tu nuevo panel aquí
    public AnclajePianoAR scriptAnclaje;
    private bool pianoYaCalibrado = false;

    // Variables internas
    private string cancionSeleccionada = "";

    void Start()
    {
        MostrarMenuPrincipal();
    }


    public void MostrarMenuPrincipal()
    {
        panelMenuPrincipal.SetActive(true);
        panelSeleccionCanciones.SetActive(false);
        panelJuegoAR.SetActive(false);

        reproductorAudio.Stop();
        cancionSeleccionada = "";
    }

    public void MostrarSeleccionCanciones()
    {
        // Si no está calibrado, forzamos la pantalla de calibración primero
        if (!pianoYaCalibrado)
        {
            AbrirMenuCalibracion();
            return;
        }

        // Si ya está calibrado, vamos al menú de canciones normal
        panelMenuPrincipal.SetActive(false);
        panelDeCalibracion.SetActive(false);
        panelSeleccionCanciones.SetActive(true);
        panelJuegoAR.SetActive(false);

        contenedorOpcionesMano.SetActive(false);
        textoTituloCancion.text = "Selecciona una pista para escucharla";
    }
    public void AbrirMenuCalibracion()
    {
        panelMenuPrincipal.SetActive(false);
        panelSeleccionCanciones.SetActive(false);
        panelJuegoAR.SetActive(false);

        panelDeCalibracion.SetActive(true);
        scriptAnclaje.ActivarModoCalibracion();

        // Si es la primera vez que entra, le centramos la cascada automáticamente
        if (!pianoYaCalibrado)
        {
            scriptAnclaje.CentrarFrenteACamara();
        }
    }

    public void FinalizarCalibracion()
    {
        pianoYaCalibrado = true;
        scriptAnclaje.DesactivarModoCalibracion();

        // Al terminar, pasamos directamente a la selección de canciones
        MostrarSeleccionCanciones();
    }
    public void SeleccionarCancion(string idCancion)
    {
        cancionSeleccionada = idCancion;

        contenedorOpcionesMano.SetActive(true);
        textoTituloCancion.text = "Practicando: " + idCancion;

        AudioClip clipPreview = Resources.Load<AudioClip>("Audios/" + idCancion);

        if (clipPreview != null)
        {
            reproductorAudio.Stop();
            reproductorAudio.clip = clipPreview;
            reproductorAudio.pitch = 1f;
            reproductorAudio.Play();
        }
        else
        {
            Debug.LogWarning("No se encontró el audio de preview para: " + idCancion + ". Revisa la carpeta Resources/Audios.");
        }
    }

    public void IniciarPractica(string sufijoMano)
    {
        if (string.IsNullOrEmpty(cancionSeleccionada)) return;

        archivoMidiPendiente = cancionSeleccionada + "_" + sufijoMano + ".mid";

        panelSeleccionCanciones.SetActive(false);
        panelJuegoAR.SetActive(true);

        reproductorAudio.Stop();
        reproductorAudio.time = 0f;

        if (textoCuentaRegresiva != null) textoCuentaRegresiva.gameObject.SetActive(false);

        // --- NUEVA LÓGICA DE DECISIÓN ---
        if (!pianoYaCalibrado)
        {
            // Es la primera vez: Activamos el panel de calibración táctil
            scriptAnclaje.ActivarModoCalibracion();
            pianoYaCalibrado = true; // Lo marcamos como calibrado para las próximas canciones
        }
        else
        {
            // Ya estaba calibrado: Saltamos directo a la cuenta regresiva y a tocar
            IniciarCuentaRegresiva();
        }
    }

    public void ForzarCalibracionManual()
    {
        // Se llama desde el menú de Selección de Canciones para re-calibrar
        panelSeleccionCanciones.SetActive(false);
        panelJuegoAR.SetActive(true);

        scriptAnclaje.ActivarModoCalibracion();
        pianoYaCalibrado = true;
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

        reproductorAudio.Stop();
        reproductorAudio.time = 0f;
        reproductorAudio.Play();
        lectorMidi.IniciarCascada(archivoMidiPendiente);

        yield return new WaitForSeconds(1f);
        textoCuentaRegresiva.gameObject.SetActive(false);
    }

    public void InterrumpirPractica()
    {

        if (rutinaCuenta != null) StopCoroutine(rutinaCuenta);
        if (textoCuentaRegresiva != null) textoCuentaRegresiva.gameObject.SetActive(false);


        reproductorAudio.Stop();
        lectorMidi.DetenerCascada();
    }

    public void CambiarVelocidadJuego(float multiplicador)
    {
        lectorMidi.CambiarVelocidadCaida(multiplicador);
        reproductorAudio.pitch = multiplicador;
    }

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