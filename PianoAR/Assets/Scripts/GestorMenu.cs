using UnityEngine;
using TMPro;
using System.Collections;

public class GestorMenu : MonoBehaviour
{
    [Header("Paneles Principales UI")]
    public GameObject panelMenuPrincipal;
    public GameObject panelSeleccionCanciones;
    public GameObject panelJuegoAR;
    public GameObject panelDeCalibracion;

    [Header("Sub-Elementos de Selección")]
    public GameObject contenedorOpcionesMano;
    public TextMeshProUGUI textoTituloCancion;

    [Header("Referencias Externas")]
    public MidiReaderTest lectorMidi;
    public AudioSource reproductorAudio;
    public AnclajePianoAR scriptAnclaje;

    [Header("UI AR y Cuenta Regresiva")]
    public TextMeshProUGUI textoCuentaRegresiva;

    private string archivoMidiPendiente = "";
    private string cancionSeleccionada = "";
    private bool pianoYaCalibrado = false;
    private Coroutine rutinaCuenta;

    void Start()
    {
        MostrarMenuPrincipal();
    }

    public void MostrarMenuPrincipal()
    {
        panelMenuPrincipal.SetActive(true);
        panelSeleccionCanciones.SetActive(false);
        panelJuegoAR.SetActive(false);
        if (panelDeCalibracion != null) panelDeCalibracion.SetActive(false);

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

        panelMenuPrincipal.SetActive(false);
        if (panelDeCalibracion != null) panelDeCalibracion.SetActive(false);
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

        if (panelDeCalibracion != null) panelDeCalibracion.SetActive(true);
        scriptAnclaje.ActivarModoCalibracion();

        // Si es la primera vez que entra, centramos la cascada frente a la cámara
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
        if (panelDeCalibracion != null) panelDeCalibracion.SetActive(false);
        panelJuegoAR.SetActive(true);

        reproductorAudio.Stop();
        reproductorAudio.time = 0f;
        reproductorAudio.pitch = 1f;
        lectorMidi.EstablecerVelocidad100();

        IniciarCuentaRegresiva();
    }

    public void IniciarCuentaRegresiva()
    {
        if (rutinaCuenta != null) StopCoroutine(rutinaCuenta);
        rutinaCuenta = StartCoroutine(RutinaCuenta());
    }

    private IEnumerator RutinaCuenta()
    {
        // 1. Lanzamos la cascada desde el inicio de la cuenta:
        // Como las notas traen un colchón espacial de 3 segundos en MidiReaderTest,
        // irán bajando durante el "3, 2, 1" y tocarán la línea justo en "¡A TOCAR!"
        lectorMidi.IniciarCascada(archivoMidiPendiente);

        textoCuentaRegresiva.gameObject.SetActive(true);

        float tiempoGracia = lectorMidi.tiempoGraciaSegundos; // 3 segundos base
        float progreso = 0f;

        // El reloj avanza al ritmo del pitch (por si el usuario cambia la velocidad durante la cuenta)
        while (progreso < tiempoGracia)
        {
            int segundoMostrado = Mathf.CeilToInt(tiempoGracia - progreso);
            textoCuentaRegresiva.text = segundoMostrado.ToString();

            progreso += Time.deltaTime * reproductorAudio.pitch;
            yield return null;
        }

        textoCuentaRegresiva.text = "¡A TOCAR!";

        // 2. Justo cuando la primera nota llega a Y = 0, arranca el audio
        reproductorAudio.Stop();
        reproductorAudio.time = 0f;
        reproductorAudio.Play();

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