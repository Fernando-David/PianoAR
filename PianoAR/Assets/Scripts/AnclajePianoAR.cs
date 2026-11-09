using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

[RequireComponent(typeof(ARTrackedImageManager))]
public class AnclajePianoAR : MonoBehaviour
{
    [Header("Referencias")]
    public Transform objetoCascada;
    public string nombreMarcador = "PianoAR81";

    [Header("Calibración Fina")]
    [Tooltip("Ajuste manual (X, Y, Z) en metros. Z ajusta la profundidad sobre las teclas.")]
    public Vector3 ajustePosicion = new Vector3(0, 0, 0.1f);

    [Header("Control de Anclaje Espacial")]
    private bool posicionFijada = false;

    [Header("Interfaz de Usuario")]
    public GameObject botonFijar;
    public GameObject botonReajustar;
    public GestorMenu gestorMenu;

    private ARTrackedImageManager gestorImagenes;

    void Start()
    {
        if (botonFijar != null) botonFijar.SetActive(true);
        if (botonReajustar != null) botonReajustar.SetActive(false);
    }

    void Awake()
    {
        gestorImagenes = GetComponent<ARTrackedImageManager>();
    }

#pragma warning disable CS0618 
    void OnEnable()
    {
        gestorImagenes.trackedImagesChanged += AlCambiarImagen;
    }

    void OnDisable()
    {
        gestorImagenes.trackedImagesChanged -= AlCambiarImagen;
    }

    void AlCambiarImagen(ARTrackedImagesChangedEventArgs args)
    {
        if (posicionFijada) return;

        foreach (var imagen in args.added)
        {
            ActualizarAnclaje(imagen);
        }
        foreach (var imagen in args.updated)
        {
            ActualizarAnclaje(imagen);
        }
    }
#pragma warning restore CS0618

    void ActualizarAnclaje(ARTrackedImage imagen)
    {
        if (imagen.referenceImage.name == nombreMarcador && imagen.trackingState == TrackingState.Tracking)
        {
            if (!objetoCascada.gameObject.activeSelf)
            {
                objetoCascada.gameObject.SetActive(true);
            }
            objetoCascada.position = imagen.transform.position + imagen.transform.TransformDirection(ajustePosicion);
            objetoCascada.rotation = imagen.transform.rotation;
        }
    }

    public void ConfirmarPosicion()
    {
        posicionFijada = true;

        if (objetoCascada != null)
        {
            objetoCascada.SetParent(null);

            if (objetoCascada.GetComponent<ARAnchor>() == null)
            {
                objetoCascada.gameObject.AddComponent<ARAnchor>();
            }
        }

        if (botonFijar != null) botonFijar.SetActive(false);
        if (botonReajustar != null) botonReajustar.SetActive(true);
        if (gestorMenu != null) gestorMenu.IniciarCuentaRegresiva();
    }

    public void ReajustarPosicion()
    {
        posicionFijada = false;

        if (objetoCascada != null)
        {
            ARAnchor ancla = objetoCascada.GetComponent<ARAnchor>();
            if (ancla != null)
            {
                Destroy(ancla);
            }
        }

        if (botonReajustar != null) botonReajustar.SetActive(false);
        if (botonFijar != null) botonFijar.SetActive(true);
        if (gestorMenu != null) gestorMenu.InterrumpirPractica();
    }
}