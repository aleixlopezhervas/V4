## Documentación Técnica: Route Planner
### Introducción
El apartado de Planificación de Rutas (Route Planner) es un componente de la aplicación diseñado para permitir la creación, parametrización, modificación y persistencia de misiones de vuelo autónomo.
Utiliza una interfaz gráfica basada en mapas interactivos y se apoya en una arquitectura de cliente-servidor para almacenar las misiones y su información en una base de datos externa a través de una API REST.

### Funcionalidades del Planificador
La aplicación ofrece un conjunto completo de herramientas para la manipulación de rutas, gestionadas a través de la interfaz gráfica y eventos de control. 

<img width="1319" height="691" alt="Interfaz del Planificador de Rutas" src="https://github.com/user-attachments/assets/321f738c-b788-4483-ac9c-7da63b2b4a8c" />

*Figura 1. Interfaz del Planificador de Rutas*

<br>
<p align="center"><b>Tabla de Funcionalidades</b></p>

| Ref. en Fig. 1 | Funcionalidad | Control de Interfaz | Descripción de la Operación |
| ---------------- | ---------------- | ---------------- | ---------------- |
|   1 | Creación de Waypoints | Clic Izquierdo en el Mapa | Transforma las coordenadas (X,Y) del clic en el mapa a coordenadas geográficas (Lat/Lon) instanciando una nueva Instruccion al final de la ruta. |
|   2 | Selección y Visualización | Recuadro de Waypoints (Lista) | Muestra el orden secuencial de la misión. Permite seleccionar una instrucción específica para cargar sus propiedades actuales y habilitar su edición, reordenamiento o eliminación. |
|   3 | Parametrización | Botón "Aplicar" | Aplica los valores de Altitud, Heading y Directriz (ej. TakePhoto, Hover) de los controles de la interfaz a la instrucción previamente seleccionada en la lista. |
|   4 | Reordenamiento | Botones "Subir" / "Bajar" | Modifica el índice de una instrucción seleccionada dentro de la lista temporal, actualizando automáticamente el trazado visual de la ruta. |
|   5 | Eliminación Unitaria | Botón "Eliminar" | Remueve un waypoint específico de la lista y reconstruye las líneas de ruta para conectar los puntos adyacentes restantes. |
|   6 | Limpieza Global | Botón "Limpiar Ruta" | Purga la lista de instrucciones en memoria, reinicia los contadores de identificadores y limpia los marcadores del mapa. |
|   7 | Guardado | Botón "Guardar Ruta" | Genera una previsualización de la ruta, solicita un nombre (Nametag) al usuario y transmite el objeto Vuelo completo a la API para su almacenamiento. |
|   8 | Carga de Rutas | Botón "Cargar Ruta" | Realiza una petición GET a la API, presenta un selector de vuelos disponibles y renderiza la ruta seleccionada en el mapa de edición. |

### Guía de Uso del Planificador
El flujo de trabajo diseñado para el operador del planificador sigue una secuencia lógica de diseño y configuración geométrica.

#### Fase 1: Trazado Espacial

1. **Navegación del Mapa:** Utilice el ratón para desplazar el mapa hasta la zona de vuelo deseada.

2. **Definición de Puntos:** Haga clic izquierdo sobre el mapa en las ubicaciones donde desea que el dron transite. Cada clic generará un marcador numérico y una línea de trayectoria azul que lo conecta con el punto anterior. El nuevo punto aparecerá automáticamente en la lista de la interfaz.

#### Fase 2: Configuración de Parámetros (Directrices)
Al crear un punto, este adopta parámetros por defecto (Altitud de 5 metros, Heading de 0 grados y ninguna acción asociada). Para modificar un punto:

1. **Selección:** Haga clic en la instrucción correspondiente dentro del panel de lista lateral.

2. **Ajuste:** Modifique la Altitud (altura de vuelo para ese segmento) y el Heading (hacia dónde apuntará el morro del dron).

3. **Asignación de Tareas:** Seleccione una acción del menú desplegable de funciones (ej. StartVideoRecording o ReturnToHome).

4. **Aplicación:** Es imperativo hacer clic en el botón Aplicar para que los cambios se guarden en la configuración del punto.

#### Fase 3: Edición y Refinamiento Secuencial
Si el orden de los puntos no es el deseado:

- Seleccione el punto en la lista y utilice los botones Subir o Bajar para alterar el orden en el que el dron los visitará. El mapa redibujará las líneas instantáneamente para reflejar el nuevo camino.

- Si un punto es erróneo, selecciónelo y presione Eliminar. Para descartar el trabajo actual y empezar desde cero, utilice Limpiar Ruta.

#### Fase 4: Almacenamiento
Una vez finalizado el diseño:

1. Presione **Guardar Ruta**.

2. El sistema solicitará un identificador o nombre para la misión.

3. Se presentará un resumen (preview) detallado con todas las coordenadas y comandos. Confirme la operación para que la ruta sea almacenada en la base de datos central, quedando disponible para su ejecución posterior en el módulo de vuelo.
<img width="690" height="450" alt="Captura de pantalla 2026-05-23 200612" src="https://github.com/user-attachments/assets/4b877152-5d21-4e7e-ba26-1fcf7f445856" />

*Figura 2. Preview de Ruta de Ejemplo*

### 4. Estructura de Datos y Arquitectura Subyacente

El módulo de planificación de rutas está diseñado bajo una arquitectura Cliente-Servidor. La aplicación local actúa como el cliente visual, gestionando el estado en memoria y la renderización cartográfica, mientras que la persistencia y gestión de misiones se delega a un backend remoto a través de una API RESTful. 

Para lograr esto, el código se divide en tres capas fundamentales: el Modelo de Datos (entidades lógicas), la Capa de Servicios (comunicación HTTP) y la Capa de Presentación (motor de mapas).

#### 4.1. Modelo de Datos (Entidades Lógicas)
La estructura de una misión se fundamenta en un modelo jerárquico fuertemente tipado. Las clases utilizan la directiva `[JsonPropertyName]` de la librería `System.Text.Json` para mapear directamente los objetos de C# a los documentos JSON esperados por la base de datos en MongoDB.

1. **`Punto`**: Representa la primitiva espacial. Almacena las coordenadas geográficas tridimensionales (`Lat`, `Long`, `Altitud`) y la orientación de la nariz del dron (`Heading`). Al instanciarse, genera automáticamente un `Guid` temporal para su trazabilidad local antes de ser enviado al servidor.
2. **`Directriz`**: Es una enumeración (`enum`) que define el catálogo de comportamientos autónomos que el dron puede ejecutar al alcanzar un `Punto` (ej. `TakePhoto`, `StartVideoRecording`, `Hover`, `ReturnToHome`).
3. **`Instruccion`**: Es el bloque de construcción (nodo) de la misión. Actúa como una clase contenedora que vincula un objeto `Punto` con una `Directriz`. Además, añade metadatos fundamentales para la lógica del servidor:
   * `ID_Vuelo`: Llave foránea que relaciona la instrucción con una misión principal.
   * `Trail`: Un entero que define el orden secuencial estricto en el que el dron debe recorrer los puntos.
   * `VisualId`: Identificador transitorio generado estáticamente (`contador`) para enumerar los waypoints en la interfaz gráfica del usuario de forma amigable (ej. "I1", "I2").
4. **`Vuelo`**: Actúa como el elemento raíz (*Aggregate Root*). Engloba los metadatos de la misión (`NameTag`, `Fecha`, `NumVersiones`) y contiene la lista enlazada de objetos `Instruccion` (`List<Instruccion>`), definiendo la ruta completa.

#### 4.2. Capa de Servicios (`DroneAPIService`)
Toda la persistencia de datos está encapsulada en la clase `DroneAPIService`, aislando la lógica de red de la interfaz gráfica. Esta clase utiliza `HttpClient` para comunicarse asíncronamente (`async/await`) con el endpoint remoto (`http://dronseetac.upc.edu:8104/api`).

El flujo de trabajo HTTP se divide en las siguientes operaciones críticas:

* **Persistencia Inicial (`GuardarRutaAsync`)**: Realiza una transacción en dos pasos. Primero, envía un método `POST` al endpoint `/vuelo` para registrar la misión y obtener un `_id` de servidor. Posteriormente, mapea la lista local de instrucciones, inyectando el `_id` del vuelo y calculando el índice secuencial (`trail = index + 1`), para enviarlas mediante un `POST` al endpoint `/instrucciones`.
* **Carga de Datos (`CargarRutaAsync` / `ObtenerInstruccionesVueloAsync`)**: Implementa un algoritmo de **paginación robusta**. Dado que una misión puede contener cientos de waypoints, el servicio ejecuta peticiones `GET` iterativas en bloques (`page=1&limit=100`), acumulando las respuestas mediante la des-serialización de `JsonDocument` hasta que la propiedad `hayMas` detecta el final del conjunto de datos. Posteriormente filtra y ordena las instrucciones basándose en su atributo `Trail` (`OrderBy(i => i.Trail)`).
* **Sincronización y Actualización (`ActualizarRutaAsync`)**: Para modificar una ruta existente, el planificador descarga el estado actual de la base de datos, lo compara con el estado local y genera un bloque JSON (Payload) híbrido. Este bloque reutiliza los `_id` existentes para los puntos modificados e inyecta nuevos objetos para los puntos añadidos, enviándolos mediante un `PUT`. Adicionalmente, detecta si la ruta local tiene menos puntos que la remota, disparando eventos `DELETE` (`EliminarInstruccionAsync`) para purgar los nodos sobrantes.

#### 4.3. Renderización y Motor Cartográfico (`GMap.NET`)
El estado local (`List<Instruccion>`) interactúa constantemente con el componente `GMapControl`. La arquitectura visual está separada en capas superpuestas (`GMapOverlay`):
* **Capa de Nodos (`editWaypointsOverlay`)**: Por cada `Instruccion` añadida al modelo, se crea un objeto `WaypointMarker` basado en sus coordenadas (`Lat` / `Long`). El marcador almacena como `Tag` el `Id` de la instrucción, lo que permite relacionar los clics en el mapa con el objeto exacto en la memoria.
* **Capa de Aristas (`editRouteOverlay`)**: La topología de la ruta se calcula recorriendo secuencialmente la lista de instrucciones (`for i = 0 to instrucciones.Count - 1`). Se extraen los puntos adyacentes (Punto `i` y Punto `i+1`) para instanciar objetos `GMapRoute`, dibujando los vectores que representan el camino que el dron volará de manera autónoma.



## Documentación Técnica: Ejecución de Vuelos

### Introducción

El módulo de Ejecución de Vuelos es el componente responsable de materializar las misiones diseñadas en el Route Planner. Permite cargar rutas almacenadas en la base de datos y ejecutarlas de forma autónoma sobre el dron real o simulado, gestionando el desplazamiento entre waypoints, la ejecución de directrices y el registro multimedia del vuelo.

### Funcionalidades del Módulo de Vuelo BD

<br>
<p align="center"><b>Tabla de Funcionalidades</b></p>

| Funcionalidad | Control de Interfaz | Descripción de la Operación |
|---|---|---|
| Carga de Vuelo | Botón "Cargar Vuelo BD" | Realiza una petición a la API, muestra los vuelos disponibles paginados de 10 en 10 y renderiza la ruta seleccionada en el mapa de ejecución. |
| Paginación de Vuelos | Botones "Anterior" / "Siguiente" | Permite navegar por el listado completo de vuelos almacenados cuando supera los 10 registros. |
| Inicio de Vuelo | Botón "Iniciar Vuelo" | Ordena el despegue (si el dron está en tierra) y comienza la secuencia de navegación autónoma hacia los waypoints en orden de `Trail`. |
| Pausa / Reanudación | Botón "Pausar Vuelo" / "Reanudar Vuelo" | Detiene el movimiento del dron en su posición actual y lo reanuda dirigiéndolo de nuevo al waypoint pendiente. |
| Detención | Botón "Detener Vuelo" | Aborta la secuencia de vuelo, detiene el dron y finaliza cualquier grabación de video en curso. |
| Grabación Automática | Automático al iniciar | Si la ruta no contiene directrices explícitas de video, el sistema inicia la grabación del vuelo completo por defecto. |
| Preview en Vivo | Panel izquierdo de video | Muestra el stream MJPEG del dron en tiempo real mientras se graba, con indicadores del número de frames y tamaño acumulado. |
| Reproducción de Video Previo | Panel derecho de video | Reproduce el último video asociado a la primera instrucción del vuelo cargado (recuperado de Cloudinary). |
| Traza de Vuelo | Mapa interactivo | Dibuja en tiempo real el rastro de la trayectoria efectivamente recorrida por el dron, solapándose sobre la ruta planificada. |

### Guía de Uso del Módulo de Vuelo

#### Fase 1: Selección y Carga de Misión

1. **Navegar al listado:** El panel lateral presenta los vuelos disponibles en la base de datos, paginados en bloques de 10. Utilice los botones `Anterior` y `Siguiente` para explorar el catálogo.

2. **Seleccionar vuelo:** Haga clic sobre el vuelo deseado en la lista o haga doble clic para cargarlo directamente.

3. **Cargar vuelo:** Pulse `Cargar Vuelo BD` (o el doble clic). El sistema recuperará las instrucciones de la API mediante paginación robusta, las ordenará por su atributo `Trail` y las renderizará en el mapa como marcadores numerados conectados por líneas azules. El mapa centrará y ajustará el zoom automáticamente en función de la extensión geográfica de la ruta.

#### Fase 2: Verificación Previa

Antes de iniciar el vuelo, compruebe en el panel de lista de waypoints que el orden y número de instrucciones coinciden con la misión diseñada. El mapa mostrará la trayectoria planificada completa para su inspección visual.

Si el vuelo tiene un video asociado de una ejecución anterior, este se cargará automáticamente en el panel derecho de reproducción.

#### Fase 3: Ejecución Autónoma

1. **Iniciar vuelo:** Pulse `Iniciar Vuelo`. Si el dron se encuentra en tierra, el sistema ejecutará automáticamente el despegue a la altitud del primer waypoint antes de comenzar la navegación.

2. **Seguimiento en tiempo real:** La posición actual del dron (marcador rojo) se actualiza continuamente a partir de los datos de telemetría. El sistema compara esta posición con el waypoint objetivo y, cuando la diferencia es inferior al umbral de llegada (~4,5 metros), considera el punto alcanzado y avanza al siguiente.

3. **Ejecución de directrices:** Al alcanzar cada waypoint, el sistema ejecuta la acción asociada (ver tabla de directrices). Algunas directrices introducen una espera controlada para garantizar que el dron se estabiliza antes de continuar.

4. **Pausa y reanudación:** En cualquier momento puede pausar el vuelo con `Pausar Vuelo`. El dron detendrá su movimiento. Al pulsar `Reanudar Vuelo`, retomará la navegación hacia el waypoint en el que se encontraba.

#### Fase 4: Finalización y Almacenamiento de Medios

Al alcanzar el último waypoint, o al pulsar `Detener Vuelo`, el sistema:

- Detiene la grabación de video si estaba activa.
- Ensambla los frames JPEG capturados en un archivo `.avi` con codec MJPEG.
- Sube el archivo de video a Cloudinary a través de la API y lo vincula a la primera instrucción del vuelo.
- El indicador de estado del panel de grabación confirmará el resultado de la subida.

### Catálogo de Directrices de Ejecución

| Directriz | Comportamiento en Vuelo |
|---|---|
| `TakePhoto` | Captura un fotograma del stream activo y lo guarda como JPEG. Introduce una pausa de 2 segundos para estabilización. |
| `StartVideoRecording` | Inicia la grabación del stream de video si no estaba activa. |
| `StopVideoRecording` | Detiene la grabación y desencadena el guardado y subida del video. |
| `ChangeAltitude` | Ordena al dron ir a la altitud definida en el punto, manteniéndose en la misma coordenada horizontal. Pausa de 3 segundos. |
| `ChangeHeading` | Rota el morro del dron al ángulo `Heading` del punto sin desplazamiento lateral. Pausa de 3 segundos. |
| `Hover` | Detiene el dron en su posición durante 5 segundos antes de continuar. |
| `Wait` | Equivalente a `Hover`: pausa de 5 segundos en el punto. |
| `ReturnToHome` | Activa el modo RTL del dron y detiene la secuencia de waypoints. |
| `None` | Sin acción asociada; el dron avanza directamente al siguiente waypoint. |

### Estructura de Datos y Arquitectura Subyacente

El módulo de ejecución reutiliza la misma capa de servicios (`DroneAPIService`) y modelo de datos (`Vuelo` / `Instruccion` / `Punto`) que el Route Planner. La lógica propia de este módulo se articula en tres componentes adicionales.

#### Motor de Navegación Autónoma (`BDWaypointTimer`)

Un temporizador de 500 ms actúa como bucle principal de control de vuelo. En cada tick:

1. Obtiene la posición actual del dron desde la telemetría (`currentLatDeg`, `currentLonDeg`).
2. Calcula la distancia al waypoint objetivo (`bdCurrentWaypointIndex`) en diferencia de coordenadas.
3. Si `ΔLat` y `ΔLon` son ambos inferiores a `WAYPOINT_ARRIVAL_THRESHOLD` (≈ 4,5 m), el punto se considera alcanzado.
4. Se ejecuta la directriz asociada (si la hay) y se avanza el índice al siguiente waypoint.
5. Se ordena `IrAlPunto` al dron con las coordenadas y altitud del nuevo objetivo.
6. El marcador de dron y la traza de vuelo se actualizan en el mapa.

El manejo del heading respeta el parámetro `WP_YAW_BEHAVIOR` del autopilot: si el waypoint tiene un heading explícito distinto de cero, se fija con `CambiarHeading` antes de avanzar; en caso contrario, el autopilot gestiona el rumbo automáticamente.

#### Grabador de Video en Vuelo

El sistema de grabación opera de forma completamente autónoma:

- Un temporizador de 66 ms (≈ 15 FPS) muestrea el último frame JPEG disponible del stream MJPEG.
- Solo se almacenan frames nuevos (control por referencia `lastProcessedVidFrame`) para evitar duplicados.
- Los frames se acumulan en memoria en `bdVideoFrames` durante el vuelo.
- Al finalizar la grabación, `GuardarVideoDesdeFrames` ensambla un contenedor AVI válido (RIFF/MJPEG) de forma nativa en C#, sin dependencias externas, incluyendo las cabeceras `avih`, `strh`, `strf` e índice `idx1`.
- El archivo resultante se sube a Cloudinary mediante un `POST` multipart a través de `DroneAPIService.SubirMediaAsync`.

#### Paginación de Vuelos

La lista de misiones disponibles se obtiene de la caché local (`allVuelosCache`), que se rellena al inicio de la aplicación con todos los vuelos del servidor. La paginación es puramente local: el índice `flightPageIndex` controla qué bloque de 10 entradas se muestra en el `ListBox`, permitiendo la navegación sin peticiones adicionales a la API.

### Diagrama de Flujo de Ejecución

```
[Seleccionar Vuelo] ──► [CargarRutaAsync] ──► [Ordenar por Trail] ──► [Renderizar en Mapa]
                                                                              │
                                                                    [Iniciar Vuelo]
                                                                              │
                                                              ┌───────────────▼──────────────┐
                                                              │  BDWaypointTimer (500 ms)    │
                                                              │                              │
                                                              │  ¿Telemetría disponible?     │
                                                              │        │  No ──► Esperar     │
                                                              │        │  Sí                 │
                                                              │  ¿Waypoint alcanzado?        │
                                                              │        │  No ──► Continuar   │
                                                              │        │  Sí                 │
                                                              │  Ejecutar Directriz          │
                                                              │  Avanzar índice              │
                                                              │  IrAlPunto (siguiente)       │
                                                              │        │                     │
                                                              │  ¿Último waypoint?           │
                                                              │        │  Sí ──► Finalizar   │
                                                              └────────┼─────────────────────┘
                                                                       │
                                                              [Detener grabación]
                                                              [Ensamblar AVI]
                                                              [Subir a Cloudinary]
```
