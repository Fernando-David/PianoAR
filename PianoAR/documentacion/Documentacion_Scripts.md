# Documentación Técnica de Scripts - Proyecto PianoAR

## 1. Visión General y Arquitectura del Sistema

El proyecto **PianoAR** es una aplicación de Realidad Aumentada (AR) desarrollada en Unity que permite proyectar una cascada de notas musicales (estilo *Synthesia*) directamente sobre las teclas de un piano físico real utilizando seguimiento de marcadores de imagen (*Image Tracking*).

La arquitectura actual se compone de **3 scripts principales** ubicados en `Assets/Scripts/`, los cuales trabajan de forma coordinada:

1. **`GestorMenu.cs`**: Controla el flujo de estados de la interfaz de usuario (Menú Principal $\rightarrow$ Selección de Canción y Mano $\rightarrow$ Modo Práctica AR), la previsualización y reproducción del audio MP3/WAV, la cuenta regresiva antes de iniciar y el ajuste sincronizado de velocidad (tanto visual como auditiva).
2. **`AnclajePianoAR.cs`**: Se encarga de detectar el marcador físico colocado sobre el piano mediante **ARFoundation** (`ARTrackedImageManager`), posicionar y alinear el contenedor 3D de las notas con el teclado real, ofrecer controles en pantalla para calibrar la posición milimétricamente en los 3 ejes (X, Y, Z) y "fijar" (congelar) el anclaje en el espacio para que no tiemble durante la práctica.
3. **`MidiReaderTest.cs`**: Es el motor de lectura y renderizado musical. Extrae los archivos `.mid` desde `StreamingAssets` (compatible con Android y PC), los interpreta usando la librería **DryWetMidi**, traduce cada nota musical a coordenadas físicas reales en metros (calculando octavas, teclas blancas y sostenidos/negros), e implementa un sistema de **Object Pooling** para hacer caer, escalar y reciclar los bloques 3D de las notas de manera óptima sin afectar el rendimiento.

### Flujo de Interacción entre Scripts

```
[Usuario en UI] 
   │
   ▼
GestorMenu.SeleccionarCancion("id") ──► Carga AudioClip desde Resources/Audios/
   │
   ▼
GestorMenu.IniciarPractica("mano")  ──► Guarda nombre "id_mano.mid" y abre Panel AR
   │
   ▼
AnclajePianoAR.AlCambiarImagen()    ──► Detecta marcador "PianoAR81" y ubica objetoCascada
   │
   ▼
[Usuario calibra con flechas y presiona "Fijar"]
   │
   ▼
AnclajePianoAR.ConfirmarPosicion()  ──► Congela posición AR y llama a:
   │
   ▼
GestorMenu.IniciarCuentaRegresiva() ──► Cuenta "3, 2, 1, ¡A TOCAR!", reproduce Audio y llama a:
   │
   ▼
MidiReaderTest.IniciarCascada()     ──► Extrae el .mid, calcula posiciones físicas y mueve las notas en Update()
```

---

## 2. Script: `AnclajePianoAR.cs`

**Ruta:** `Assets/Scripts/AnclajePianoAR.cs`

### 2.1 Propósito de la Clase
Esta clase hereda de `MonoBehaviour` y requiere obligatoriamente que el mismo `GameObject` tenga adjunto un componente `ARTrackedImageManager` (garantizado por el atributo `[RequireComponent(typeof(ARTrackedImageManager))]`). Su objetivo es vincular el mundo físico con el virtual: busca el marcador impreso en el piano, posiciona la cascada de notas sobre él, permite al usuario ajustar la posición manualmente con botones en pantalla y bloquea el seguimiento cuando el usuario está listo para tocar.

### 2.2 Dependencias y Librerías
```csharp
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
```
* **`UnityEngine.XR.ARFoundation`**: Necesario para acceder a `ARTrackedImageManager`, `ARTrackedImage` y los eventos de detección de imágenes en AR.
* **`UnityEngine.XR.ARSubsystems`**: Necesario para verificar el estado de seguimiento (`TrackingState.Tracking`).

---

### 2.3 Variables de `AnclajePianoAR`

#### A. Referencias (`[Header("Referencias")]`)
| Variable | Tipo | Visibilidad | Valor por defecto | Qué controla / A qué se refiere |
| :--- | :--- | :--- | :--- | :--- |
| `objetoCascada` | `Transform` | `public` | *Asignado en Inspector* | Es el objeto 3D raíz o contenedor donde aparece la cascada de notas. Este objeto se mueve y rota para alinearse con el marcador AR. |
| `nombreMarcador` | `string` | `public` | `"PianoAR81"` | Nombre exacto que tiene la imagen de referencia dentro de la librería `XRReferenceImageLibrary` de Unity. |

* **¿Qué se puede modificar?**: Puedes cambiar `nombreMarcador` en el Inspector **únicamente** si cambias el nombre de la imagen en tu `Reference Image Library`.
* **¿Qué NO se debe modificar?**: No dejes `objetoCascada` vacío (`null`), ya que producirá un `NullReferenceException` en cuanto la cámara detecte el marcador. Tampoco pongas un nombre en `nombreMarcador` que no coincida exactamente (mayúsculas y minúsculas) con el de la librería AR.

#### B. Calibración Fina (`[Header("Calibración Fina")]`)
| Variable | Tipo | Visibilidad | Valor por defecto | Qué controla / A qué se refiere |
| :--- | :--- | :--- | :--- | :--- |
| `ajustePosicion` | `Vector3` | `public` | `(0, 0, 0.1f)` | Desplazamiento (offset) en metros respecto al centro del marcador físico. `X` mueve a izquierda/derecha, `Y` arriba/abajo, `Z` ajusta la profundidad sobre las teclas. |

* **¿Qué se puede modificar?**: Es totalmente seguro modificar sus valores iniciales en el Inspector si notas que el marcador físico siempre queda desplazado una distancia fija respecto al Do central (C4) de tu piano. Además, este valor se modifica dinámicamente en tiempo de ejecución cuando el usuario pulsa los botones de calibración.

#### C. Control de Anclaje Espacial (`[Header("Control de Anclaje Espacial")]`)
| Variable | Tipo | Visibilidad | Valor por defecto | Qué controla / A qué se refiere |
| :--- | :--- | :--- | :--- | :--- |
| `posicionFijada` | `bool` | `private` | `false` | Bandera interna de estado. Cuando es `false`, la cascada sigue constantemente al marcador AR. Cuando es `true`, ignora las actualizaciones de la cámara AR para evitar vibraciones mientras se toca el piano. |

* **¿Qué NO se debe modificar?**: Es privada y se controla exclusivamente mediante los métodos `ConfirmarPosicion()` y `ReajustarPosicion()`. No debe forzarse a `true` al inicio porque impediría que el marcador sea detectado por primera vez.

#### D. Interfaz de Usuario (`[Header("Interfaz de Usuario")]`)
| Variable | Tipo | Visibilidad | Valor por defecto | Qué controla / A qué se refiere |
| :--- | :--- | :--- | :--- | :--- |
| `botonFijar` | `GameObject` | `public` | *Asignado en Inspector* | Botón de la UI que el usuario presiona cuando la cascada ya está bien alineada con su piano. |
| `botonReajustar` | `GameObject` | `public` | *Asignado en Inspector* | Botón de la UI que aparece tras fijar la posición, permitiendo desbloquear el anclaje para volver a calibrar. |
| `panelCalibracion` | `GameObject` | `public` | *Asignado en Inspector* | Panel contenedor que agrupa las 6 flechas de dirección para mover la cascada manualmente. Se oculta al fijar la posición para limpiar la vista. |
| `gestorMenu` | `GestorMenu` | `public` | *Asignado en Inspector* | Referencia al script `GestorMenu` de la escena para poder iniciar o interrumpir la cuenta regresiva y la canción. |
| `gestorImagenes` | `ARTrackedImageManager` | `private` | Se obtiene en `Awake()` | Referencia interna al gestor de imágenes AR de Unity. |

* **¿Qué se puede modificar?**: Puedes cambiar el diseño visual de `botonFijar`, `botonReajustar` y `panelCalibracion` en el Canvas de Unity libremente.
* **¿Qué NO se debe modificar?**: No desconectes estas referencias en el Inspector. En especial, `gestorMenu` es indispensable para que la canción comience al fijar la posición.

#### E. Ajuste Fino en Tiempo Real (`[Header("Ajuste Fino en Tiempo Real")]`)
| Variable | Tipo | Visibilidad | Valor por defecto | Qué controla / A qué se refiere |
| :--- | :--- | :--- | :--- | :--- |
| `pasoAjuste` | `float` | `public` | `0.01f` | Distancia en metros que se desplaza la cascada cada vez que se presiona un botón de dirección (`0.01f` equivale a 1 centímetro). |

* **¿Qué se puede modificar?**: Si sientes que 1 cm por clic es demasiado brusco, puedes bajarlo en el Inspector a `0.005f` (5 milímetros) o `0.002f` (2 milímetros) sin romper absolutamente nada.

---

### 2.4 Métodos y Funciones de `AnclajePianoAR`

#### 1. Métodos de Ciclo de Vida (`Awake`, `Start`, `OnEnable`, `OnDisable`)

```csharp
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
```
* **`Awake()`**: Se ejecuta antes que `Start()`. Obtiene e inicializa la referencia al componente `ARTrackedImageManager` que está en el mismo `GameObject`.
* **`Start()`**: Configura el estado visual inicial de la interfaz AR: muestra el botón de "Fijar" y el panel de flechas de calibración, y oculta el botón de "Reajustar".
* **`OnEnable()` y `OnDisable()`**: Suscriben y desuscriben el método `AlCambiarImagen` al evento `trackedImagesChanged` de ARFoundation. Esto asegura que Unity avise a este script cada vez que la cámara detecte o actualice un marcador. *(Nota: `#pragma warning disable CS0618` se usa para ocultar la advertencia de obsolescencia de versiones recientes de ARFoundation sin alterar su funcionamiento).*

#### 2. Funciones de Movimiento Manual (Botones de Calibración)

```csharp
public void MoverArriba() { AplicarAjusteLocal(new Vector3(0, pasoAjuste, 0)); }
public void MoverAbajo() { AplicarAjusteLocal(new Vector3(0, -pasoAjuste, 0)); }
public void MoverDerecha() { AplicarAjusteLocal(new Vector3(pasoAjuste, 0, 0)); }
public void MoverIzquierda() { AplicarAjusteLocal(new Vector3(-pasoAjuste, 0, 0)); }
public void MoverAdelante() { AplicarAjusteLocal(new Vector3(0, 0, pasoAjuste)); }
public void MoverAtras() { AplicarAjusteLocal(new Vector3(0, 0, -pasoAjuste)); }
```
* **Qué hacen**: Son 6 funciones públicas diseñadas para conectarse directamente al evento `OnClick()` de los botones de la UI en el `panelCalibracion`. Cada una construye un vector de dirección en el eje correspondiente (`X` para izquierda/derecha, `Y` para arriba/abajo, `Z` para adelante/atrás) usando la magnitud de `pasoAjuste` y llama a `AplicarAjusteLocal()`.

#### 3. `AplicarAjusteLocal(Vector3 desplazamiento)`

```csharp
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
```
* **Qué hace**: Acumula el desplazamiento en la variable `ajustePosicion`. Si el marcador aún no está fijado (`posicionFijada == false`), el cambio se verá reflejado automáticamente en el próximo fotograma cuando `ActualizarAnclaje` sume `ajustePosicion`. Si ya estaba fijado (`posicionFijada == true`), mueve el `objetoCascada` inmediatamente en su propio espacio local (`Space.Self`).

#### 4. `AlCambiarImagen(ARTrackedImagesChangedEventArgs args)` y `ActualizarAnclaje(ARTrackedImage imagen)`

```csharp
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
```
* **`AlCambiarImagen`**: Recibe las listas de imágenes recién detectadas (`args.added`) y las que cambiaron de posición (`args.updated`). **Punto clave**: Si `posicionFijada` es `true`, hace un `return` inmediato, ignorando por completo la cámara AR para que la cascada quede estática en el mundo virtual.
* **`ActualizarAnclaje`**:
  1. Verifica que el nombre de la imagen detectada coincida con `nombreMarcador` (`"PianoAR81"`) y que ARFoundation la esté rastreando activamente (`TrackingState.Tracking`).
  2. Activa el `objetoCascada` si estaba oculto.
  3. **Cálculo matemático clave**: `imagen.transform.position + imagen.transform.TransformDirection(ajustePosicion)`. Usa `TransformDirection` para convertir el ajuste local `(X, Y, Z)` a la orientación real del marcador en el mundo, garantizando que "adelante" o "derecha" siempre estén alineados con el piano físico sin importar desde qué ángulo lo mire la cámara.
  4. Iguala la rotación del `objetoCascada` con la rotación del marcador (`imagen.transform.rotation`).

#### 5. `ConfirmarPosicion()` y `ReajustarPosicion()`

```csharp
public void ConfirmarPosicion()
{
    posicionFijada = true;

    if (botonFijar != null) botonFijar.SetActive(false);
    if (botonReajustar != null) botonReajustar.SetActive(true);

    // Apagamos el panel de calibración para limpiar la pantalla
    if (panelCalibracion != null) panelCalibracion.SetActive(false);

    if (gestorMenu != null) gestorMenu.IniciarCuentaRegresiva();
}

public void ReajustarPosicion()
{
    posicionFijada = false;

    if (botonReajustar != null) botonReajustar.SetActive(false);
    if (botonFijar != null) botonFijar.SetActive(true);

    // Volvemos a encender el panel de calibración
    if (panelCalibracion != null) panelCalibracion.SetActive(true);

    if (gestorMenu != null) gestorMenu.InterrumpirPractica();
}
```
* **`ConfirmarPosicion()`**: Se llama desde el botón "Fijar". Bloquea el tracking AR (`posicionFijada = true`), intercambia el botón "Fijar" por "Reajustar", oculta el panel de flechas de calibración para despejar la pantalla y le ordena a `gestorMenu` que comience la cuenta regresiva ("3, 2, 1...") para iniciar la canción.
* **`ReajustarPosicion()`**: Se llama desde el botón "Reajustar". Desbloquea el tracking AR (`posicionFijada = false`) para que el contenedor vuelva a seguir al marcador, restaura los botones de calibración y llama a `gestorMenu.InterrumpirPractica()` para detener inmediatamente la música y limpiar las notas en pantalla.

---

## 3. Script: `GestorMenu.cs`

**Ruta:** `Assets/Scripts/GestorMenu.cs`

### 3.1 Propósito de la Clase
Actúa como el controlador central de navegación de pantallas (UI), reproducción de audio y sincronización del inicio/fin de las prácticas. Conecta las decisiones del usuario en el menú (qué canción tocar, con qué mano y a qué velocidad) con el reproductor de audio (`AudioSource`) y el lector MIDI (`MidiReaderTest`).

### 3.2 Dependencias y Librerías
```csharp
using UnityEngine;
using TMPro;
using System.Collections;
```
* **`TMPro` (TextMeshPro)**: Para actualizar textos dinámicos en la interfaz (título de la canción seleccionada y números de la cuenta regresiva).
* **`System.Collections`**: Requerido para el uso de corrutinas (`IEnumerator` y `Coroutine`) en el temporizador de cuenta regresiva.

---

### 3.3 Variables de `GestorMenu`

#### A. Paneles Principales UI (`[Header("Paneles Principales UI")]`)
| Variable | Tipo | Visibilidad | Qué controla / A qué se refiere |
| :--- | :--- | :--- | :--- |
| `panelMenuPrincipal` | `GameObject` | `public` | Pantalla de inicio de la aplicación. |
| `panelSeleccionCanciones` | `GameObject` | `public` | Pantalla donde se listan las canciones disponibles y las opciones de mano. |
| `panelJuegoAR` | `GameObject` | `public` | Interfaz superpuesta durante la vista de cámara AR (contiene controles de calibración, velocidad y botón de salir). |

#### B. Sub-Elementos de Selección (`[Header("Sub-Elementos de Selección")]`)
| Variable | Tipo | Visibilidad | Qué controla / A qué se refiere |
| :--- | :--- | :--- | :--- |
| `contenedorOpcionesMano` | `GameObject` | `public` | Contenedor con los botones para elegir qué mano practicar (ej. Izquierda, Derecha, Ambas). Permanece oculto hasta que se elige una canción. |
| `textoTituloCancion` | `TextMeshProUGUI` | `public` | Texto en pantalla que muestra `"Selecciona una pista para escucharla"` o `"Practicando: [Nombre]"`. |

#### C. Referencias Externas (`[Header("Referencias Externas")]`)
| Variable | Tipo | Visibilidad | Qué controla / A qué se refiere |
| :--- | :--- | :--- | :--- |
| `lectorMidi` | `MidiReaderTest` | `public` | Referencia directa al componente `MidiReaderTest` que genera y mueve las notas 3D. |
| `reproductorAudio` | `AudioSource` | `public` | Componente de audio de Unity que reproduce tanto el preview en el menú como la pista musical durante la práctica. |

#### D. UI AR, Cuenta Regresiva y Variables Internas
| Variable | Tipo | Visibilidad | Valor por defecto | Qué controla / A qué se refiere |
| :--- | :--- | :--- | :--- | :--- |
| `textoCuentaRegresiva` | `TextMeshProUGUI` | `public` | *Asignado en Inspector* | Texto grande en el centro de la pantalla AR que muestra `"3"`, `"2"`, `"1"`, `"¡A TOCAR!"`. |
| `archivoMidiPendiente` | `string` | `private` | `""` | Almacena el nombre completo del archivo `.mid` armado (ej. `"EnderLilies_Derecha.mid"`) esperando a que el usuario fije el piano en AR. |
| `rutinaCuenta` | `Coroutine` | `private` | `null` | Guarda la referencia a la corrutina activa de cuenta regresiva para poder cancelarla si el usuario presiona "Reajustar" o "Salir" antes de que termine. |
| `cancionSeleccionada` | `string` | `private` | `""` | Guarda el identificador base de la canción elegida en el menú (ej. `"EnderLilies"`). |

* **¿Qué se puede modificar en `GestorMenu`?**:
  * Los textos mostrados al usuario dentro de `MostrarSeleccionCanciones()`, `SeleccionarCancion()` y `RutinaCuenta()`.
  * Los tiempos de espera de la cuenta regresiva (`WaitForSeconds(1f)`).
* **¿Qué NO se debe modificar?**:
  * **Nomenclatura de archivos y carpetas**: `SeleccionarCancion` busca audios en `"Audios/" + idCancion` dentro de `Assets/Resources/Audios/`, e `IniciarPractica` construye el nombre del archivo MIDI como `cancionSeleccionada + "_" + sufijoMano + ".mid"`. Si cambias esta estructura en el código sin renombrar tus archivos en `Resources/Audios` y `StreamingAssets`, las canciones dejarán de cargar.

---

### 3.4 Métodos y Funciones de `GestorMenu`

#### 1. `Start()`, `MostrarMenuPrincipal()` y `MostrarSeleccionCanciones()`

```csharp
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
```
* **`Start()`**: Al arrancar la escena, lleva siempre al usuario al menú principal.
* **`MostrarMenuPrincipal()`**: Activa solo el `panelMenuPrincipal`, apaga los demás paneles, detiene cualquier música que esté sonando y limpia `cancionSeleccionada`.
* **`MostrarSeleccionCanciones()`**: Abre el catálogo de canciones, oculta los botones de selección de mano (`contenedorOpcionesMano`) hasta que el usuario toque una canción, y reinicia el texto de ayuda.

#### 2. `SeleccionarCancion(string idCancion)`

```csharp
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
```
* **Qué hace**: Se asigna a los botones de cada canción en la UI pasando su identificador por parámetro (`idCancion`).
  1. Guarda el ID en `cancionSeleccionada`.
  2. Muestra los botones para elegir la mano (`contenedorOpcionesMano.SetActive(true)`).
  3. Carga dinámicamente el archivo de audio desde `Assets/Resources/Audios/[idCancion]`.
  4. Si lo encuentra, reinicia el `reproductorAudio`, asegura que el tono/velocidad esté al 100% (`pitch = 1f`) y reproduce un adelanto de la canción.

#### 3. `IniciarPractica(string sufijoMano)`

```csharp
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
```
* **Qué hace**: Se asigna a los botones de selección de mano (ej. pasando `"Derecha"`, `"Izquierda"`, `"Ambas"` o el sufijo que manejes en tus archivos).
  1. Combina el nombre de la canción con el sufijo para formar el nombre del archivo MIDI: `<cancionSeleccionada>_<sufijoMano>.mid` y lo deja listo en `archivoMidiPendiente`.
  2. Cambia del panel de selección al `panelJuegoAR`.
  3. Detiene la música de previsualización y rebobina la pista a `0f` segundos, dejando todo en espera hasta que el usuario apunte al marcador AR y presione el botón **Fijar**.

#### 4. `IniciarCuentaRegresiva()` y `RutinaCuenta()`

```csharp
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
```
* **Qué hacen**: Cuando el usuario confirma la posición del piano en `AnclajePianoAR.ConfirmarPosicion()`, se llama a `IniciarCuentaRegresiva()`.
  * La corrutina `RutinaCuenta()` muestra en pantalla `"3"`, `"2"`, `"1"` esperando 1 segundo real entre cada número.
  * Al llegar a `"¡A TOCAR!"`, arranca simultáneamente el audio (`reproductorAudio.Play()`) y la cascada visual de notas (`lectorMidi.IniciarCascada(archivoMidiPendiente)`).
  * Espera 1 segundo adicional y oculta el texto `"¡A TOCAR!"`.

#### 5. `InterrumpirPractica()`, `CambiarVelocidadJuego(float multiplicador)`, `SalirDeAR()` y `SalirAplicacion()`

```csharp
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
```
* **`InterrumpirPractica()`**: Cancela la cuenta regresiva si estaba en curso, oculta el texto, detiene el audio y llama a `lectorMidi.DetenerCascada()` para guardar todas las notas activas de vuelta en su Pool.
* **`CambiarVelocidadJuego(float multiplicador)`**: Permite cambiar la velocidad de práctica (ej. `0.5f` para 50%, `1.0f` para 100%) manteniendo sincronizados tanto la velocidad a la que caen los cubos MIDI (`lectorMidi.CambiarVelocidadCaida`) como la velocidad de reproducción del audio (`reproductorAudio.pitch`).
* **`SalirDeAR()`**: Limpia la sesión actual llamando a `InterrumpirPractica()` y regresa al menú principal.
* **`SalirAplicacion()`**: Cierra la aplicación (`Application.Quit()`).

---

## 4. Script: `MidiReaderTest.cs`

**Ruta:** `Assets/Scripts/MidiReaderTest.cs`

### 4.1 Propósito de la Clase
Es el núcleo técnico del visualizador de piano. Lee archivos estándar `.mid` binarios utilizando la biblioteca **Melanchall.DryWetMidi**, extrae el mapa de tempo y la lista de notas con su tiempo de inicio y duración en microsegundos, calcula la posición física exacta sobre un teclado real (distinguiendo teclas blancas y negras/sostenidos a partir del Do central MIDI 60), y administra el ciclo de vida visual de las notas mediante dos colas de reciclaje (*Object Pools*) para evitar tirones de memoria (*Garbage Collection*) en dispositivos móviles AR.

### 4.2 Dependencias y Librerías
```csharp
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using UnityEngine.Networking;
using System.Collections;
using TMPro;
```
* **`Melanchall.DryWetMidi.Core` y `.Interaction`**: Motor de lectura de archivos MIDI (`MidiFile`, `TempoMap`, `GetNotes()`, `MetricTimeSpan`).
* **`UnityEngine.Networking`**: Necesario para `UnityWebRequest`, utilizado para extraer archivos `.mid` desde el interior del paquete comprimido `.apk` en Android (`Application.streamingAssetsPath`).
* **`System.IO`**: Manejo de rutas (`Path.Combine`) y escritura en memoria persistente (`File.WriteAllBytes`, `File.Exists`).

---

### 4.3 Subclase Interna: `DatosNota`

Dentro de `MidiReaderTest` se declara una clase privada auxiliar que almacena toda la información precalculada de cada nota musical antes de que aparezca en pantalla:

```csharp
private class DatosNota
{
    public float posicionX;      // Posición horizontal en metros respecto al Do central (MIDI 60)
    public float posicionY;      // Altura inicial en metros según el segundo en que debe sonar
    public float alturaNota;     // Largo vertical del bloque en metros según cuánto dura presionada la tecla
    public bool esSostenido;     // true si es tecla negra (#/b), false si es tecla blanca
    public bool estaActiva;      // true mientras tiene un GameObject (cubo) visible en escena
    public bool estaTerminada;   // true cuando ya pasó por debajo del teclado y fue reciclada
    public GameObject cuboActivo;// Referencia al objeto 3D prestado del Pool mientras cae
    public string letraNota;     // Nombre en cifrado americano ("C", "C#", "D", etc.)
}
```
* **¿Qué NO se debe modificar?**: No elimines ninguno de estos campos, ya que el bucle `Update()` los consulta en cada fotograma para decidir cuándo activar, escalar y reciclar cada nota.

---

### 4.4 Variables de `MidiReaderTest`

#### A. Configuración de Archivo y Objetos Visuales
| Variable | Tipo | Visibilidad | Valor por defecto | Qué controla / A qué se refiere |
| :--- | :--- | :--- | :--- | :--- |
| `nombreArchivoMidi` | `string` | `public` | `"EnderLilies.mid"` | Nombre del archivo MIDI actual. Es sobrescrito automáticamente cuando `GestorMenu` llama a `IniciarCascada(nombreArchivo)`. |
| `prefabNotaBlanca` | `GameObject` | `public` | *Asignado en Inspector* | Prefab 3D para las notas naturales (C, D, E, F, G, A, B). **Importante:** Su estructura debe tener como primer hijo (`GetChild(0)`) la malla visual del cubo y un componente `TextMeshPro` en sus hijos. |
| `prefabNotaNegra` | `GameObject` | `public` | *Asignado en Inspector* | Prefab 3D para las notas sostenidas/bemoles (C#, D#, F#, G#, A#). Requiere la misma jerarquía interna que `prefabNotaBlanca`. |
| `contenedorCascada` | `Transform` | `public` | *Asignado en Inspector* | Contenedor padre donde se instancian las notas y que desciende constantemente en el eje Y local durante la canción. |

#### B. Ajustes de Cascada (`[Header("Ajustes de Cascada")]`)
| Variable | Tipo | Visibilidad | Valor por defecto | Qué controla / A qué se refiere |
| :--- | :--- | :--- | :--- | :--- |
| `velocidadCaida` | `float` | `public` | `2f` | Velocidad en metros por segundo a la que desciende el `contenedorCascada` (y factor de escala vertical de las notas). |
| `anchoTecla` | `float` | `public` | `0.024f` | Ancho físico en metros de una tecla blanca real de tu piano (`0.024f` = 2.4 centímetros). |

* **¿Qué se puede modificar?**:
  * **`anchoTecla`**: ¡Muy importante para la calibración física! Si en un piano distinto notas que a medida que las notas se alejan hacia los extremos (octavas altas o bajas) se van desfasando respecto a las teclas reales, puedes ajustar `anchoTecla` ligeramente (por ejemplo, `0.0235f` para 2.35 cm).
  * **`velocidadCaida`**: Puedes cambiar el valor inicial en el Inspector antes de dar Play si quieres que las notas sean más largas y caigan más rápido, o más cortas y caigan más despacio.

#### C. Configuración del Pool y Variables Internas (`[Header("Configuración del Pool")]`)
| Variable | Tipo | Visibilidad | Valor por defecto | Qué controla / A qué se refiere |
| :--- | :--- | :--- | :--- | :--- |
| `tamanoPool` | `int` | `public` | `40` | Cantidad de bloques blancos y negros (40 de cada uno) que se pre-instancian al inicio para evitar crear objetos en medio de la canción. |
| `alturaAparicionY` | `float` | `public` | `15f` | Distancia en el eje Y local a partir de la cual una nota se vuelve visible y toma un cubo del Pool. |
| `alturaReciclajeY` | `float` | `public` | `-2f` | Límite inferior en el eje Y local; cuando una nota baja de este valor, se apaga y se devuelve al Pool. |
| `todasLasNotas` | `List<DatosNota>` | `private` | Lista vacía | Lista en memoria con los datos matemáticos de todas las notas de la canción cargada. |
| `poolDeNotasBlancas` | `Queue<GameObject>` | `private` | Cola vacía | Cola de reciclaje de objetos 3D para notas blancas. |
| `poolDeNotasNegras` | `Queue<GameObject>` | `private` | Cola vacía | Cola de reciclaje de objetos 3D para notas negras. |
| `velocidadOriginal` | `float` | `private` | `velocidadCaida` | Guarda el valor inicial de `velocidadCaida` al arrancar (`Start`) para poder calcular porcentajes de velocidad (25%, 50%, 75%, 100%) sin perder la referencia base ni deformar la geometría de las notas. |

---

### 4.5 Métodos y Funciones de `MidiReaderTest`

#### 1. `Start()` e `InicializarPools()`

```csharp
void Start()
{
    velocidadOriginal = velocidadCaida;
    InicializarPools();
}

void InicializarPools()
{
    for (int i = 0; i < tamanoPool; i++)
    {
        GameObject objBlanca = Instantiate(prefabNotaBlanca, contenedorCascada);
        objBlanca.SetActive(false);
        poolDeNotasBlancas.Enqueue(objBlanca);

        GameObject objNegra = Instantiate(prefabNotaNegra, contenedorCascada);
        objNegra.SetActive(false);
        poolDeNotasNegras.Enqueue(objNegra);
    }
}
```
* **Qué hacen**: Al iniciar la aplicación, guarda la `velocidadOriginal` y crea en memoria 40 instancias desactivadas de `prefabNotaBlanca` y 40 de `prefabNotaNegra` como hijos de `contenedorCascada`, guardándolas en sus respectivas colas (`Queue`). Si en algún momento de una canción muy densa hay más de 40 notas simultáneas en pantalla, el script creará nuevas instancias dinámicamente sin fallar.

#### 2. Cálculos Musicales y Físicos: `EsSostenidoOBemol(int numeroMidi)` y `ObtenerPosicionXReal(int numeroMidi)`

```csharp
bool EsSostenidoOBemol(int numeroMidi)
{
    int notaEnOctava = numeroMidi % 12;
    return notaEnOctava == 1 || notaEnOctava == 3 || notaEnOctava == 6 || notaEnOctava == 8 || notaEnOctava == 10;
}

float ObtenerPosicionXReal(int numeroMidi)
{
    int distanciaMidi = numeroMidi - 60;

    int octavas = Mathf.FloorToInt((float)distanciaMidi / 12f);

    int notaEnOctava = distanciaMidi - (octavas * 12);

    float[] posiciones = new float[12] { 0f, 0.5f, 1f, 1.5f, 2f, 3f, 3.5f, 4f, 4.5f, 5f, 5.5f, 6f };

    float anchoOctavaFisica = 7f * anchoTecla;

    return (octavas * anchoOctavaFisica) + (posiciones[notaEnOctava] * anchoTecla);
}
```
* **`EsSostenidoOBemol`**: En el estándar MIDI, cada octava tiene 12 semitonos y cualquier Do (C) es múltiplo de 12. Al aplicar módulo 12 (`numeroMidi % 12`), los residuos `1 (C#)`, `3 (D#)`, `6 (F#)`, `8 (G#)` y `10 (A#)` corresponden exactamente a las 5 teclas negras de cada octava.
* **`ObtenerPosicionXReal`**: Convierte un número de nota MIDI (0 a 127) en su posición horizontal exacta en metros:
  1. Toma como centro (`X = 0`) la nota **MIDI 60** (Do central / C4).
  2. Calcula a cuántas octavas de distancia está la nota (`octavas`) y cuál de las 12 notas dentro de su octava es (`notaEnOctava`).
  3. Utiliza el arreglo `posiciones = { 0f, 0.5f, 1f, 1.5f, 2f, 3f, 3.5f, 4f, 4.5f, 5f, 5.5f, 6f }`. Observa que entre el índice 4 (**Mi / E**, valor `2f`) y el índice 5 (**Fa / F**, valor `3f`) hay un salto entero de 1 tecla porque en un piano real no existe tecla negra entre Mi y Fa.
  4. Multiplica las octavas por el ancho de 7 teclas blancas (`7f * anchoTecla`) y le suma el desplazamiento de la nota dentro de la octava.
  * **¿Qué NO se debe modificar?**: No alteres el arreglo `posiciones` ni la constante `12`, ya que representan la geometría física universal de un teclado musical.

#### 3. `ProcesarMidiFisico(string rutaArchivo)`

```csharp
public void ProcesarMidiFisico(string rutaArchivo)
{
    if (!File.Exists(rutaArchivo))
    {
        Debug.LogError("No se encontró el archivo MIDI en la ruta: " + rutaArchivo);
        return;
    }

    MidiFile archivoMidi = MidiFile.Read(rutaArchivo);
    TempoMap mapaTempo = archivoMidi.GetTempoMap();
    var notasMidi = archivoMidi.GetNotes().ToList();

    foreach (var nota in notasMidi)
    {
        float tiempoInicio = (float)nota.TimeAs<MetricTimeSpan>(mapaTempo).TotalMicroseconds / 1000000f;
        float duracion = (float)nota.LengthAs<MetricTimeSpan>(mapaTempo).TotalMicroseconds / 1000000f;

        float xCalculado = ObtenerPosicionXReal(nota.NoteNumber);
        float alturaCalculada = duracion * velocidadOriginal;
        float yCalculado = (tiempoInicio * velocidadOriginal) + (alturaCalculada / 2f);
        string[] nombresNotas = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        string letraTraducida = nombresNotas[nota.NoteNumber % 12];

        todasLasNotas.Add(new DatosNota
        {
            posicionX = xCalculado,
            posicionY = yCalculado,
            alturaNota = alturaCalculada,
            esSostenido = EsSostenidoOBemol(nota.NoteNumber),
            estaActiva = false,
            estaTerminada = false,
            letraNota = letraTraducida
        });
    }
}
```
* **Qué hace**:
  1. Abre el archivo físico `.mid` desde el almacenamiento y extrae su `TempoMap` (indispensable para convertir *ticks* MIDI a segundos reales respetando los cambios de BPM de la canción).
  2. Convierte el instante de inicio (`tiempoInicio`) y la duración (`duracion`) de microsegundos a **segundos** (dividiendo entre `1,000,000`).
  3. Calcula la altura del bloque (`alturaCalculada = duracion * velocidadOriginal`) y su posición vertical (`yCalculado = (tiempoInicio * velocidadOriginal) + (alturaCalculada / 2f)`). Se suma la mitad de la altura porque el pivote de un cubo en Unity está en su centro geométrico; así el borde inferior de la nota toca la tecla exactamente en `tiempoInicio`.
  4. Traduce el número MIDI al nombre de la nota (`"C"`, `"C#"`, etc.) y guarda el registro en `todasLasNotas`.

#### 4. `Update()` (Movimiento de Cascada y Reciclaje de Objetos)

```csharp
void Update()
{
    if (contenedorCascada != null)
    {
        contenedorCascada.Translate(Vector3.down * velocidadCaida * Time.deltaTime, Space.Self);
    }

    float yLocalContenedor = contenedorCascada.localPosition.y;
    
    foreach (var nota in todasLasNotas)
    {
        if (nota.estaTerminada) continue;

        float yLocalActual = yLocalContenedor + nota.posicionY;

        if (!nota.estaActiva && yLocalActual <= alturaAparicionY)
        {
            GameObject cuboReciclado;
            Queue<GameObject> poolCorrecto = nota.esSostenido ? poolDeNotasNegras : poolDeNotasBlancas;
            GameObject prefabCorrecto = nota.esSostenido ? prefabNotaNegra : prefabNotaBlanca;

            if (poolCorrecto.Count > 0)
                cuboReciclado = poolCorrecto.Dequeue();
            else
                cuboReciclado = Instantiate(prefabCorrecto, contenedorCascada);

            cuboReciclado.transform.localPosition = new Vector3(nota.posicionX, nota.posicionY, -0.02f);

            Transform cuboVisual = cuboReciclado.transform.GetChild(0);
            cuboVisual.localScale = new Vector3(anchoTecla * 0.9f, nota.alturaNota, 0.03f);

            TextMeshPro texto = cuboReciclado.GetComponentInChildren<TextMeshPro>();
            if (texto != null)
            {
                texto.text = nota.letraNota;

                float bordeInferior = -(nota.alturaNota / 2f) + 0.02f;
                texto.transform.localPosition = new Vector3(0, bordeInferior, -0.03f);
            }

            cuboReciclado.SetActive(true);
            nota.cuboActivo = cuboReciclado;
            nota.estaActiva = true;
        }

        if (nota.estaActiva && yLocalActual < alturaReciclajeY)
        {
            nota.cuboActivo.SetActive(false);
            if (nota.esSostenido)
                poolDeNotasNegras.Enqueue(nota.cuboActivo);
            else
                poolDeNotasBlancas.Enqueue(nota.cuboActivo);

            nota.cuboActivo = null;
            nota.estaTerminada = true;
        }
    }
}
```
* **Cómo funciona cuadro a cuadro**:
  1. **Movimiento unificado**: En lugar de mover cientos de notas por separado cada frame, mueve únicamente el padre `contenedorCascada` hacia abajo (`Vector3.down * velocidadCaida * Time.deltaTime`).
  2. **Cálculo de altura visible**: La posición real de cada nota respecto al piano es `yLocalActual = yLocalContenedor + nota.posicionY`.
  3. **Aparición (Spawn desde el Pool)**: Cuando la nota desciende hasta entrar en el rango de visión (`yLocalActual <= alturaAparicionY`), saca un cubo de la cola correspondiente (`poolDeNotasBlancas` o `poolDeNotasNegras`).
     * Escala el cubo hijo (`GetChild(0)`) con un ancho del 90% de la tecla (`anchoTecla * 0.9f`) para dejar un pequeño margen visual entre teclas contiguas, y le da el largo de `nota.alturaNota`.
     * Escribe el nombre de la nota (`"C"`, `"F#"`, etc.) en el componente `TextMeshPro` y lo coloca justo en el borde inferior del bloque (`-(nota.alturaNota / 2f) + 0.02f`).
  4. **Reciclaje (Despawn hacia el Pool)**: Cuando la nota ya pasó por las teclas y bajó más allá de `alturaReciclajeY` (`-2f`), desactiva el `GameObject`, lo devuelve a su cola con `Enqueue()` y marca `nota.estaTerminada = true`.
* **¿Qué NO se debe modificar?**: No cambies la jerarquía interna de los Prefabs (`prefabNotaBlanca` y `prefabNotaNegra`), ya que `cuboReciclado.transform.GetChild(0)` requiere que el primer hijo sea siempre la malla del cubo a escalar (y no el texto, para evitar que las letras se deformen al alargar la nota).

#### 5. Control de Interfaz, Inicio, Detención y Velocidades

```csharp
public void EstablecerVelocidad25() { velocidadCaida = velocidadOriginal * 0.25f; }
public void EstablecerVelocidad50() { velocidadCaida = velocidadOriginal * 0.5f; }
public void EstablecerVelocidad75() { velocidadCaida = velocidadOriginal * 0.75f; }
public void EstablecerVelocidad100() { velocidadCaida = velocidadOriginal; }

public void CambiarVelocidadCaida(float multiplicador)
{
    velocidadCaida = velocidadOriginal * multiplicador;
}

public void IniciarCascada(string nombreArchivo)
{
    nombreArchivoMidi = nombreArchivo;
    todasLasNotas.Clear();

    if (contenedorCascada != null)
    {
        contenedorCascada.gameObject.SetActive(true);
        contenedorCascada.localPosition = Vector3.zero;
    }

    StartCoroutine(ExtraerYLeerMidi(nombreArchivo));
}

public void DetenerCascada()
{
    foreach (var nota in todasLasNotas)
    {
        if (nota.estaActiva && nota.cuboActivo != null)
        {
            nota.cuboActivo.SetActive(false);

            if (nota.esSostenido)
                poolDeNotasNegras.Enqueue(nota.cuboActivo);
            else
                poolDeNotasBlancas.Enqueue(nota.cuboActivo);

            nota.cuboActivo = null;
            nota.estaActiva = false;
        }
    }

    todasLasNotas.Clear();

    if (contenedorCascada != null)
    {
        contenedorCascada.gameObject.SetActive(false);
    }
}
```
* **Funciones de Velocidad (`EstablecerVelocidad25/50/75/100` y `CambiarVelocidadCaida`)**: Modifican `velocidadCaida` multiplicando siempre a partir de `velocidadOriginal`. De esta manera, aunque cambies la velocidad varias veces en medio de la práctica, la proporción siempre es exacta.
* **`IniciarCascada(string nombreArchivo)`**: Limpia cualquier canción previa, reactiva el `contenedorCascada`, lo regresa a la posición `(0, 0, 0)` y lanza la corrutina `ExtraerYLeerMidi`.
* **`DetenerCascada()`**: Recorre todas las notas que estén activas en ese instante en pantalla, las apaga, devuelve sus cubos 3D intactos a sus respectivos Pools (`poolDeNotasNegras` / `poolDeNotasBlancas`), vacía la lista `todasLasNotas` y apaga el `contenedorCascada`.

#### 6. `ExtraerYLeerMidi(string nombreArchivo)` (Compatibilidad con Android)

```csharp
public IEnumerator ExtraerYLeerMidi(string nombreArchivo)
{
    string rutaStreaming = Path.Combine(Application.streamingAssetsPath, nombreArchivo);
    string rutaPersistente = Path.Combine(Application.persistentDataPath, nombreArchivo);

    if (rutaStreaming.Contains("://") || rutaStreaming.Contains(":///"))
    {
        using (UnityWebRequest www = UnityWebRequest.Get(rutaStreaming))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                File.WriteAllBytes(rutaPersistente, www.downloadHandler.data);

                ProcesarMidiFisico(rutaPersistente);
            }
            else
            {
                Debug.LogError("Error extrayendo MIDI: " + www.error);
            }
        }
    }
    else
    {
        ProcesarMidiFisico(rutaStreaming);
    }
}
```
* **Por qué existe y qué hace**: En Android, la carpeta `Assets/StreamingAssets` queda empaquetada dentro del archivo comprimido `.apk` (bajo una ruta `jar:file:///...`), por lo que librerías estándar de C# como `File.Exists` o `MidiFile.Read` no pueden leer el archivo directamente desde ahí.
  * Este método detecta si la ruta contiene `"://"` (Android). Si es así, extrae los bytes del archivo `.mid` mediante `UnityWebRequest`, los guarda como un archivo físico real en `Application.persistentDataPath` y luego llama a `ProcesarMidiFisico(rutaPersistente)`.
  * Si estás probando en el Editor de Unity en PC (donde la ruta no tiene `"://"`), lee el archivo directamente desde `StreamingAssets`.

---

## 5. Resumen Rápido: Guía de Modificación Segura

| Qué deseas ajustar en el proyecto | Dónde hacerlo | ¿Es seguro? |
| :--- | :--- | :--- |
| **Agregar una nueva canción** | 1. Colocar el `.mp3`/`.wav` en `Assets/Resources/Audios/<ID>.mp3`<br>2. Colocar los `.mid` en `Assets/StreamingAssets/<ID>_<Mano>.mid`<br>3. En el botón de la UI, llamar a `GestorMenu.SeleccionarCancion("<ID>")` | **Sí, totalmente seguro.** No requiere tocar código. |
| **Ajustar el ancho de las teclas para que coincida con otro piano** | En el Inspector de `MidiReaderTest`, cambiar `anchoTecla` (valor actual: `0.024`). | **Sí, totalmente seguro.** |
| **Hacer que los botones de calibración AR sean más precisos** | En el Inspector de `AnclajePianoAR`, reducir `pasoAjuste` (ej. de `0.01` a `0.005`). | **Sí, totalmente seguro.** |
| **Cambiar la posición inicial de la cascada respecto al marcador QR/Imagen** | En el Inspector de `AnclajePianoAR`, modificar `ajustePosicion (X, Y, Z)`. | **Sí, totalmente seguro.** |
| **Cambiar el orden de los hijos dentro de `prefabNotaBlanca` o `prefabNotaNegra`** | En los Prefabs de las notas. | **NO.** `MidiReaderTest` usa `GetChild(0)` esperando que el primer hijo sea siempre la malla del cubo a estirar. |
| **Cambiar el nombre de la imagen en `XRReferenceImageLibrary`** | En el asset de imagen AR. | **Cuidado:** Si lo cambias, debes actualizar `nombreMarcador` en `AnclajePianoAR` exactamente con el mismo texto. |
