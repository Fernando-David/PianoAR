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
    public GameObject panelCalibracion;
    public GestorMenu gestorMenu;

    private ARTrackedImageManager gestorImagenes;

    [Header("Ajuste Fino en Tiempo Real")]
    public float pasoAjuste = 0.01f;

    public void MoverArriba() { AplicarAjusteLocal(new Vector3(0, pasoAjuste, 0)); }
    public void MoverAbajo() { AplicarAjusteLocal(new Vector3(0, -pasoAjuste, 0)); }
    public void MoverDerecha() { AplicarAjusteLocal(new Vector3(pasoAjuste, 0, 0)); }
    public void MoverIzquierda() { AplicarAjusteLocal(new Vector3(-pasoAjuste, 0, 0)); }
    public void MoverAdelante() { AplicarAjusteLocal(new Vector3(0, 0, pasoAjuste)); }
    public void MoverAtras() { AplicarAjusteLocal(new Vector3(0, 0, -pasoAjuste)); }

    void Start()
    {
        if (botonFijar != null) botonFijar.SetActive(true);
        if (botonReajustar != null) botonReajustar.SetActive(false);
        if (panelCalibracion != null) panelCalibracion.SetActive(true);
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

    private void AplicarAjusteLocal(Vector3 desplazamiento)
    {
        // 1. Guardamos el ajuste para que futuras detecciones del marcador lo respeten
        ajustePosicion += desplazamiento;

        // 2. Si el panel ya está "Fijado" y el usuario decide moverlo, 
        // desplazamos el objeto físicamente en ese instante respetando su propia rotación.
        if (posicionFijada && objetoCascada != null)
        {
            objetoCascada.Translate(desplazamiento, Space.Self);
        }
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

        if (botonFijar != null) botonFijar.SetActive(false);
        if (botonReajustar != null) botonReajustar.SetActive(true);

        if (panelCalibracion != null) panelCalibracion.SetActive(false);

        if (gestorMenu != null) gestorMenu.IniciarCuentaRegresiva();
    }

    public void ReajustarPosicion()
    {
        posicionFijada = false;

        if (botonReajustar != null) botonReajustar.SetActive(false);
        if (botonFijar != null) botonFijar.SetActive(true);

        if (panelCalibracion != null) panelCalibracion.SetActive(true);

        if (gestorMenu != null) gestorMenu.InterrumpirPractica();
    }
}