using UnityEngine;

public class NotaCascada : MonoBehaviour
{
    private float tiempoInicio;
    private float tiempoFin;
    private float velocidad;
    private float posX;
    private float anchoX;
    private float grosorZ;

    // Llamar a esta función al instanciar la nota en tu LectorMIDI
    public void ConfigurarNota(float inicioEnSegundos, float duracionEnSegundos, float velocidadCaida, float x, float ancho, float z)
    {
        tiempoInicio = inicioEnSegundos;
        tiempoFin = inicioEnSegundos + duracionEnSegundos;
        velocidad = velocidadCaida;
        posX = x;
        anchoX = ancho;
        grosorZ = z;

        // Posición y tamaño inicial antes de que empiece a consumirse
        ActualizarNota(-3f);
    }

    // Llamar desde el Update de tu LectorMIDI pasándole el tiempo actual de la canción
    public void ActualizarNota(float tiempoActualCancion)
    {
        // 1. Si la nota ya terminó de tocarse, se apaga
        if (tiempoActualCancion >= tiempoFin)
        {
            gameObject.SetActive(false);
            return;
        }

        // 2. Si la nota aún no llega a la línea de golpe (Y = 0)
        if (tiempoActualCancion < tiempoInicio)
        {
            float distanciaALinea = (tiempoInicio - tiempoActualCancion) * velocidad;
            float largoTotal = (tiempoFin - tiempoInicio) * velocidad;

            transform.localPosition = new Vector3(posX, distanciaALinea, 0f);
            transform.localScale = new Vector3(anchoX, largoTotal, grosorZ);
        }
        // 3. Si la nota ESTÁ cruzando la línea de golpe (se detiene en Y=0 y se encoge)
        else
        {
            float tiempoRestante = tiempoFin - tiempoActualCancion;
            float largoRestante = Mathf.Max(0f, tiempoRestante * velocidad);

            // Se queda clavada en la línea de golpe (Y = 0) mientras su escala disminuye
            transform.localPosition = new Vector3(posX, 0f, 0f);
            transform.localScale = new Vector3(anchoX, largoRestante, grosorZ);
        }
    }
}