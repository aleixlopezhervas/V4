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
