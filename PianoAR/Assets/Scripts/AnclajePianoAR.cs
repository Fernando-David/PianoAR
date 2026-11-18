using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.EnhancedTouch;
using TMPro;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

public class AnclajePianoAR : MonoBehaviour
{
    [Header("Referencias Principales")]
    public Transform objetoCascada;
    public Transform camaraAR;
    public GameObject imagenPianoGuia;

    [Header("UI del Botón de Modo (Opcional)")]
    public TMP_Text textoBotonModo;

    [Header("Sensibilidad Táctil")]
    public float sensibilidadMovimiento = 0.002f;
    public float sensibilidadProfundidad = 0.003f;
    public float sensibilidadEscala = 0.001f;
    public float sensibilidadRotacion = 0.4f;

    [Header("Límites de Escala Horizontal")]
    public float escalaMinimaX = 0.5f;
    public float escalaMaximaX = 2.5f;

    [SerializeField] private bool modoCalibracionActivo = false;
    [SerializeField] private bool modoEscaladoX = false; // false = Profundidad/Rotar, true = Solo Escala X

    void OnEnable()
    {
        EnhancedTouchSupport.Enable();
    }

    void OnDisable()
    {
        EnhancedTouchSupport.Disable();
    }

    void Start()
    {
        ActualizarTextoBoton();
    }

    void Update()
    {
        if (!modoCalibracionActivo || objetoCascada == null || camaraAR == null) return;

        var toquesActivos = Touch.activeTouches;

        // ==========================================================
        // 1 DEDO: MOVER EN EL PLANO DE LA CÁMARA
        // ==========================================================
        if (toquesActivos.Count == 1)
        {
            Touch toque = toquesActivos[0];

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(toque.touchId))
                return;

            if (toque.phase == TouchPhase.Moved)
            {
                MoverCascadaEnPantalla(toque.delta.x, toque.delta.y);
            }
        }
        // ==========================================================
        // 2 DEDOS: DEPENDE DEL MODO SELECCIONADO CON EL BOTÓN
        // ==========================================================
        else if (toquesActivos.Count == 2)
        {
            Touch toque1 = toquesActivos[0];
            Touch toque2 = toquesActivos[1];

            if (toque1.phase == TouchPhase.Moved || toque2.phase == TouchPhase.Moved)
            {
                Vector2 posActual1 = toque1.screenPosition;
                Vector2 posActual2 = toque2.screenPosition;
                Vector2 posPrevia1 = posActual1 - toque1.delta;
                Vector2 posPrevia2 = posActual2 - toque2.delta;

                float distanciaPrevia = (posPrevia1 - posPrevia2).magnitude;
                float distanciaActual = (posActual1 - posActual2).magnitude;
                float diferenciaDistancia = distanciaActual - distanciaPrevia;

                if (modoEscaladoX)
                {
                    // --- MODO ESCALA X (Rotación bloqueada para evitar accidentes) ---
                    Vector3 escalaActual = objetoCascada.localScale;
                    escalaActual.x = Mathf.Clamp(escalaActual.x + (diferenciaDistancia * sensibilidadEscala), escalaMinimaX, escalaMaximaX);
                    objetoCascada.localScale = escalaActual;
                }
                else
                {
                    // --- MODO PROFUNDIDAD (Acerca/Aleja + Rotación habilitada) ---
                    objetoCascada.position += camaraAR.forward * (diferenciaDistancia * sensibilidadProfundidad);

                    float anguloPrevio = Mathf.Atan2(posPrevia2.y - posPrevia1.y, posPrevia2.x - posPrevia1.x) * Mathf.Rad2Deg;
                    float anguloActual = Mathf.Atan2(posActual2.y - posActual1.y, posActual2.x - posActual1.x) * Mathf.Rad2Deg;
                    float diferenciaAngulo = Mathf.DeltaAngle(anguloPrevio, anguloActual);

                    objetoCascada.Rotate(objetoCascada.forward, -diferenciaAngulo * sensibilidadRotacion, Space.World);
                }
            }
        }
    }

    // --- FUNCIÓN PARA TU NUEVO BOTÓN EN EL CANVAS ---
    public void AlternarModoPellizco()
    {
        modoEscaladoX = !modoEscaladoX;
        ActualizarTextoBoton();
    }

    private void ActualizarTextoBoton()
    {
        if (textoBotonModo != null)
        {
            textoBotonModo.text = modoEscaladoX ? "Modo: Ancho X" : "Modo: Distancia";
        }
    }

    private void MoverCascadaEnPantalla(float deltaX, float deltaY)
    {
        Vector3 direccion = (camaraAR.right * deltaX + camaraAR.up * deltaY) * sensibilidadMovimiento;
        objetoCascada.position += direccion;
    }

    public void CentrarFrenteACamara()
    {
        if (objetoCascada != null && camaraAR != null)
        {
            objetoCascada.position = camaraAR.position + (camaraAR.forward * 0.4f);
            objetoCascada.rotation = camaraAR.rotation;
        }
    }

    public void ActivarModoCalibracion()
    {
        modoCalibracionActivo = true;
        if (objetoCascada != null) objetoCascada.gameObject.SetActive(true);
        if (imagenPianoGuia != null) imagenPianoGuia.SetActive(true);
    }

    public void DesactivarModoCalibracion()
    {
        modoCalibracionActivo = false;
        if (imagenPianoGuia != null) imagenPianoGuia.SetActive(false);
    }
}