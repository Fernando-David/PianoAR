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
        panelMenuPrincipal.SetActive(false);
        panelSeleccionCanciones.SetActive(true);
        panelJuegoAR.SetActive(false);

        contenedorOpcionesMano.SetActive(false);
        textoTituloCancion.text = "Selecciona una pista para escucharla";
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
        // Detiene la cuenta regresiva si estaba a medias
        if (rutinaCuenta != null) StopCoroutine(rutinaCuenta);
        if (textoCuentaRegresiva != null) textoCuentaRegresiva.gameObject.SetActive(false);

        // Apaga la música y resetea las notas
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