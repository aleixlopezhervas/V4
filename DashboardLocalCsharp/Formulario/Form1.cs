using GMap.NET;
using GMap.NET.MapProviders;
using GMap.NET.WindowsForms;
using GMap.NET.WindowsForms.Markers;
using csDronLink;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;

namespace Formulario
{

    
    public partial class App : Form
    {
        private Dron dron = new Dron();
        private string mjpegUrl = "http://localhost:8888/";

        private List<string> capturedPhotoPaths = new List<string>();
        private string photosFolder = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "DronePhotos");

        private byte[] latestJpegBytes = null;
        private readonly object jpegLock = new object();

        private List<int> selectedObjectIds = new List<int>();

        private double currentLatDeg = double.NaN;
        private double currentLonDeg = double.NaN;
        private double currentHeading = 0;
        private double currentAlt = double.NaN;

        private GMapControl gmap;
        private GMapOverlay droneOverlay;
        private GMapMarker droneMarker;
        private System.Windows.Forms.Timer mapTimer;
        private bool posicionActualizada = false;

        private GMapOverlay destinationOverlay;
        private GMapMarker destinationMarker;
        private double destLat = double.NaN;
        private double destLon = double.NaN;
        private double destAlt = double.NaN;
        private const double ARRIVAL_THRESHOLD = 0.00004; // Aprox 4.5 metros

        // Variables para navegación automática al destino
        private System.Windows.Forms.Timer navigationTimer;
        private bool isNavigatingToDestination = false;

        // Estructura para almacenar waypoint con altitud
        private struct WaypointData
        {
            public PointLatLng Position { get; set; }
            public float Altitud { get; set; }
            public float Heading { get; set; }
            public string Directriz { get; set; }
            public string InstruccionId { get; set; }

            public WaypointData(double lat, double lon, float altitud, float heading = 0, string directriz = "None")
            {
                Position = new PointLatLng(lat, lon);
                Altitud = altitud;
                Heading = heading;
                Directriz = directriz ?? "None";
                InstruccionId = null;
            }
        }

        private const double WAYPOINT_ARRIVAL_THRESHOLD = 0.00004;
        private const float FLIGHT_ALTITUDE = 5f;

        private const string API_BASE = "http://dronseetac.upc.edu:8104/api";
        private DroneAPIService droneAPIService;
        private GMapControl bdGmap;
        private GMapOverlay bdWaypointLinesOverlay;
        private GMapOverlay bdWaypointsOverlay;
        private GMapOverlay bdDroneOverlay;
        private GMapOverlay bdTraceOverlay;
        private GMapMarker bdDroneMarker;
        private List<WaypointData> bdWaypoints = new List<WaypointData>();
        private List<PointLatLng> bdTrace = new List<PointLatLng>();
        private int bdCurrentWaypointIndex = 0;
        private bool bdIsFlying = false;
        private bool bdIsPaused = false;
        private System.Windows.Forms.Timer bdWaypointTimer;

        // Estado de ejecución de directrices en BD flight
        private bool bdDirectrizEjecutada = false;
        private bool isBdWaiting = false;
        private DateTime bdWaitEndTime = DateTime.MinValue;

        // Variables para grabación de video durante BD flight
        private bool bdVideoRecording = false;
        private string bdVideoFilePath;
        private System.Windows.Forms.Timer bdVideoFrameTimer;
        private System.IO.BinaryWriter bdVideoWriter;
        private List<byte[]> bdVideoFrames = new List<byte[]>();

        // Variables para preview en vivo de grabación
        private PictureBox bdVideoPreview;
        private System.Windows.Forms.Timer bdVideoPreviewTimer;
        private Label bdRecordingStatusLabel;
        private Button bdOpenFolderBtn;
        private int bdFramesCaptured = 0;
        private long bdVideoSizeBytes = 0;
        private byte[] lastProcessedVidFrame = null;
        private string currentBdFirstInstruccionId = null;
        private WebBrowser bdVideoPlaybackBrowser;

        // Variables de paginación para vuelos
        private int flightPageIndex = 0;
        private const int VUELOS_POR_PAGINA = 10;
        private List<VueloDto> allVuelosCache = new List<VueloDto>();

        // =========================================================
        // VARIABLES DEL TAB DE EDICIÓN DE RUTAS (Form2)
        // =========================================================
        private GMapControl editGmap;
        private GMapOverlay editWaypointsOverlay;
        private GMapOverlay editRouteOverlay;

        private List<Instruccion> instrucciones = new List<Instruccion>();
        private int instruccionCounter = 0;
        private Instruccion instruccionSeleccionada = null;

        private string vueloActualCargado = null;

        public App()
        {
            InitializeComponent();

            if (DesignMode) return;

            CheckForIllegalCrossThreadCalls = false;

            Font letraGrande = new Font("Arial", 14);
            Font letraPequeña = new Font("Arial", 12);

            // Botones de navegación
            button9.Text = "NW"; button9.Tag = "NorthWest"; button9.Click += navButton_Click; button9.Font = letraGrande;
            button10.Text = "N"; button10.Tag = "North"; button10.Click += navButton_Click; button10.Font = letraGrande;
            button11.Text = "NE"; button11.Tag = "NorthEast"; button11.Click += navButton_Click; button11.Font = letraGrande;
            button12.Text = "W"; button12.Tag = "West"; button12.Click += navButton_Click; button12.Font = letraGrande;
            button13.Text = "Stop"; button13.Tag = "Stop"; button13.Click += navButton_Click; button13.Font = letraPequeña;
            button14.Text = "E"; button14.Tag = "East"; button14.Click += navButton_Click; button14.Font = letraGrande;
            button15.Text = "SW"; button15.Tag = "SouthWest"; button15.Click += navButton_Click; button15.Font = letraGrande;
            button16.Text = "S"; button16.Tag = "South"; button16.Click += navButton_Click; button16.Font = letraGrande;
            button17.Text = "SE"; button17.Tag = "SouthEast"; button17.Click += navButton_Click; button17.Font = letraGrande;
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
            if (DesignMode) return;

            // Inicializar servicio API (compartido por BD y edición de rutas)
            try
            {
                droneAPIService = new DroneAPIService(API_BASE);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error conectando a API: {ex.Message}", "Error");
                droneAPIService = null;
            }

            InicializarMapa();
            InicializarMapaVuelo();
            InicializarBotonesDeteccion();
            InicializarMapaBD();
            InicializarMapaEdicion();
            InicializarControlesFunciones();
            await CargarListaVuelos();
        }

        // =========================================================
        // COMANDOS DEL DRON (USANDO CLASE DRON)
        // =========================================================

        private void but_connect_Click(object sender, EventArgs e)
        {
            // Opciones de conexión soportadas por csDronLink:
            // dron.Conectar("produccion", "COM8"); // Para conectar por radio telemetría o USB directamente.
            // dron.Conectar("simulacion");         // Para conectar por TCP interno a (127.0.0.1:5763).
            // NOTA: Para usar MAVProxy y Mission Planner a la vez en COM8 ejecuta:
            // "mavproxy --master=COM8 --baudrate 57600 --out 127.0.0.1:14550 --out 127.0.0.1:5763"
            // y luego conéctate aquí con "simulacion" y en Mission Planner elige UDP/TCP 14550.

            DialogResult res = MessageBox.Show("¿Quieres conectar por COM8? (Si haces clic en NO, conectará en modo simulación/TCP)", "Modo de conexión", MessageBoxButtons.YesNoCancel);
            if (res == DialogResult.Yes)
            {
                dron.Conectar("produccion", "COM8");
            }
            else if (res == DialogResult.No)
            {
                dron.Conectar("simulacion");
            }
            else
            {
                return;
            }

            but_connect.BackColor = Color.Green;
            but_connect.ForeColor = Color.White;
        }

        private void EnAire(byte id, object param)
        {
            despegarBtn.BackColor = Color.Green;
            despegarBtn.ForeColor = Color.White;
            despegarBtn.Text = (string)param;
        }

        private void but_takeoff_Click(object sender, EventArgs e)
        {
            int alturaSeleccionada = AlturatrackBar.Value;
            if (alturaSeleccionada != 0)
            {
                // Poner en modo guiado para asegurar que despega de nuevo si previamente aterrizó
                dron.PonModoGuiado();
                System.Threading.Thread.Sleep(300); // Esperar que procese el modo

                dron.Despegar(alturaSeleccionada, bloquear: false, EnAire, "Volando");
                despegarBtn.BackColor = Color.Yellow;
            }
            else
            {
                MessageBox.Show("Selecciona una altura de despegue mayor que 0");
            }
        }

        private void navButton_Click(object sender, EventArgs e)
        {
            Button b = (Button)sender;
            string tag = b.Tag.ToString();

            if (tag == "Stop")
            {
                isNavigatingToDestination = false;
                if (navigationTimer != null) navigationTimer.Stop();
            }

            dron.Navegar(tag);
        }

        private void EnTierra(byte id, object mensaje)
        {
            if ((string)mensaje == "Aterrizaje")
                button7.BackColor = Color.Green;
            else
                button6.BackColor = Color.Green;
        }

        private void aterrizarBtn_Click(object sender, EventArgs e)
        {
            isNavigatingToDestination = false;
            if (navigationTimer != null) navigationTimer.Stop();
            dron.Aterrizar(bloquear: false, EnTierra, "Aterrizaje");
            button7.BackColor = Color.Yellow;
        }

        private void RTLBtn_Click(object sender, EventArgs e)
        {
            isNavigatingToDestination = false;
            if (navigationTimer != null) navigationTimer.Stop();
            dron.RTL(bloquear: false, EnTierra, "RTL");
            button6.BackColor = Color.Yellow;
        }

        private void enviarTelemetriaBtn_Click(object sender, EventArgs e)
        {
            dron.EnviarDatosTelemetria(ProcesarTelemetria);
        }

        private void detenerTelemetriaBtn_Click(object sender, EventArgs e)
        {
            dron.DetenerDatosTelemetria();
        }

        private void ProcesarTelemetria(byte id, List<(string nombre, float valor)> telemetria)
        {
            double lat = ((double)telemetria[1].valor) / 0.1E+8;
            double lon = ((double)telemetria[2].valor) / 0.1E+8;
            double heading = ((double)telemetria[3].valor) / 100;

            altitudLbl.Text = telemetria[0].valor.ToString("F1") + " m";
            latitudLbl.Text = lat.ToString();
            longitudLbl.Text = lon.ToString();
            headLbl.Text = heading.ToString("F1") + " °";
            BatteryLbl.Text = "N/A";

            ActualizarTelemetria(lat, lon, telemetria[0].valor, heading);
        }

        private void headingTrackBar_Scroll(object sender, EventArgs e)
        {
            int n = headingTrackBar.Value;
            headingLbl.Text = n.ToString();
        }

        private void headingTrackBar_MouseUp(object sender, MouseEventArgs e)
        {
            float valorSeleccionado = headingTrackBar.Value;
            dron.CambiarHeading(valorSeleccionado, bloquear: false);
        }

        private void velocidadTrackBar_Scroll(object sender, EventArgs e)
        {
            int n = velocidadTrackBar.Value;
            velocidadLbl.Text = n.ToString();
        }

        private void velocidadTrackBar_MouseUp(object sender, MouseEventArgs e)
        {
            int valorSeleccionado = velocidadTrackBar.Value;
            dron.CambiaVelocidad(valorSeleccionado);

            if (!double.IsNaN(destLat) && !double.IsNaN(destLon))
            {
                double altDestino = double.IsNaN(currentAlt) ? 5 : currentAlt;
                EnviarDronHacia(destLat, destLon, altDestino);
            }
        }

        private void AlturatrackBar_Scroll(object sender, EventArgs e)
        {
            AlturaLbl.Text = AlturatrackBar.Value.ToString();
        }

        private void Alt_changeTrackBar_Scroll(object sender, EventArgs e)
        {
            AltChangeLbl.Text = Alt_changeTrackBar.Value.ToString();
        }

        private void Alt_changeTrackBar_MouseUp(object sender, MouseEventArgs e)
        {
            int altitud = Alt_changeTrackBar.Value;
            if (!double.IsNaN(currentLatDeg) && !double.IsNaN(currentLonDeg))
            {
                // Para cambiar de altitud se comanda un IrAlPunto sobre la misma coordenada
                dron.IrAlPunto((float)currentLatDeg, (float)currentLonDeg, altitud);
            }
        }


        // =========================================================
        // PROCESAMIENTO DE DATOS LOCALES DEL MAPA
        // =========================================================

        private void EnviarDronHacia(double lat, double lon, double alt)
        {
            destLat = lat;
            destLon = lon;

            if (double.IsNaN(currentAlt))
            {
                destAlt = 5;
            }
            else
            {
                destAlt = currentAlt;
            }

            try
            {
                var method = dron.GetType().GetMethod("goto");
                if (method != null)
                {
                    method.Invoke(dron, new object[] { lat, lon, destAlt, false });
                    return;
                }
            }
            catch (Exception ex)
            {
                // Error al invocar goto
            }

            if (navigationTimer == null)
            {
                navigationTimer = new System.Windows.Forms.Timer();
                navigationTimer.Interval = 500;
                navigationTimer.Tick += NavigationTimer_Tick;
            }

            isNavigatingToDestination = true;
            navigationTimer.Start();
            Console.WriteLine($"Iniciando navegación automática hacia: Lat={lat}, Lon={lon}, Alt={destAlt}");
        }

        private void NavigationTimer_Tick(object sender, EventArgs e)
        {
            if (!isNavigatingToDestination || double.IsNaN(currentLatDeg) || double.IsNaN(currentLonDeg) ||
                double.IsNaN(destLat) || double.IsNaN(destLon))
            {
                return;
            }

            double dLat = destLat - currentLatDeg;
            double dLon = destLon - currentLonDeg;

            if (Math.Abs(dLat) < ARRIVAL_THRESHOLD && Math.Abs(dLon) < ARRIVAL_THRESHOLD)
            {
                isNavigatingToDestination = false;
                navigationTimer.Stop();
                dron.Navegar("Stop");
                Console.WriteLine("Destino alcanzado");
                return;
            }

            string direccion = CalcularDireccion(dLat, dLon);
            dron.Navegar(direccion);
        }

        private string CalcularDireccion(double dLat, double dLon)
        {
            double angulo = Math.Atan2(dLon, dLat) * 180 / Math.PI;

            if (angulo < 0) angulo += 360;

            if (angulo >= 337.5 || angulo < 22.5) return "North";
            if (angulo >= 22.5 && angulo < 67.5) return "NorthEast";
            if (angulo >= 67.5 && angulo < 112.5) return "East";
            if (angulo >= 112.5 && angulo < 157.5) return "SouthEast";
            if (angulo >= 157.5 && angulo < 202.5) return "South";
            if (angulo >= 202.5 && angulo < 247.5) return "SouthWest";
            if (angulo >= 247.5 && angulo < 292.5) return "West";
            if (angulo >= 292.5 && angulo < 337.5) return "NorthWest";

            return "Stop";
        }

        private void ActualizarTelemetria(double lat, double lon, double alt, double heading)
        {
            currentLatDeg = lat;
            currentLonDeg = lon;
            currentAlt = alt;
            currentHeading = heading;
            posicionActualizada = true;
            // Vuelo de waypoints removido
        }

        // =========================================================
        // VIDEO
        // =========================================================

        private Thread videoThread;
        private bool videoRunning = false;

        private System.Net.HttpWebRequest activeRequest = null;

        private System.Windows.Forms.Timer displayTimer;

        private void startVideoBtn_Click(object sender, EventArgs e)
        {
            videoRunning = false;
            activeRequest?.Abort();
            videoThread?.Join(1000);

            videoRunning = true;
            videoThread = new Thread(LeerMJPEG);
            videoThread.IsBackground = true;
            videoThread.Start();

            displayTimer = new System.Windows.Forms.Timer();
            displayTimer.Interval = 66;
            displayTimer.Tick += DisplayTimer_Tick;
            displayTimer.Start();
        }

        private void stopVideoBtn_Click(object sender, EventArgs e)
        {
            videoRunning = false;
            activeRequest?.Abort();
            displayTimer?.Stop();
            videoPictureBox.Image = null;
            lock (jpegLock) latestJpegBytes = null;
        }

        private void DisplayTimer_Tick(object sender, EventArgs e)
        {
            byte[] jpeg;
            lock (jpegLock)
            {
                if (latestJpegBytes == null) return;
                jpeg = latestJpegBytes;
                latestJpegBytes = null;
            }

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    Image img;
                    using (var ms = new System.IO.MemoryStream(jpeg))
                        img = new Bitmap(Image.FromStream(ms));

                    this.BeginInvoke((MethodInvoker)delegate
                    {
                        var old = videoPictureBox.Image;
                        videoPictureBox.Image = img;
                        old?.Dispose();
                    });
                }
                catch { }
            });
        }

        private void LeerMJPEG()
        {
            try
            {
                var request = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(mjpegUrl);
                request.Timeout = 5000;
                request.ReadWriteTimeout = 5000;
                activeRequest = request;

                var response = request.GetResponse();
                var stream = response.GetResponseStream();
                var buffer = new byte[4096];
                var ms = new System.IO.MemoryStream();
                int bytesRead;

                while (videoRunning && (bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, bytesRead);
                    var data = ms.ToArray();

                    int start = FindBytes(data, new byte[] { 0xFF, 0xD8 });
                    int end = FindBytes(data, new byte[] { 0xFF, 0xD9 });

                    if (start >= 0 && end >= 0 && end > start)
                    {
                        var jpeg = new byte[end - start + 2];
                        Array.Copy(data, start, jpeg, 0, jpeg.Length);

                        if (start >= 0 && end >= 0 && end > start)
                        {
                            var jpegFrame = new byte[end - start + 2];
                            Array.Copy(data, start, jpegFrame, 0, jpegFrame.Length);

                            lock (jpegLock)
                                latestJpegBytes = jpegFrame;

                            var leftover = new byte[data.Length - (end + 2)];
                            Array.Copy(data, end + 2, leftover, 0, leftover.Length);
                            ms = new System.IO.MemoryStream();
                            ms.Write(leftover, 0, leftover.Length);
                        }

                        var remaining = new byte[data.Length - (end + 2)];
                        Array.Copy(data, end + 2, remaining, 0, remaining.Length);
                        ms = new System.IO.MemoryStream();
                        ms.Write(remaining, 0, remaining.Length);
                    }

                    if (ms.Length > 2 * 1024 * 1024)
                        ms = new System.IO.MemoryStream();
                }
            }
            catch (System.Net.WebException ex) when (ex.Status == System.Net.WebExceptionStatus.RequestCanceled)
            {
                Console.WriteLine("Stream de video detenido.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error video: " + ex.Message);
            }
            finally
            {
                activeRequest = null;
            }
        }

        private int FindBytes(byte[] buffer, byte[] pattern)
        {
            for (int i = 0; i <= buffer.Length - pattern.Length; i++)
            {
                bool found = true;
                for (int j = 0; j < pattern.Length; j++)
                    if (buffer[i + j] != pattern[j]) { found = false; break; }
                if (found) return i;
            }
            return -1;
        }

        // =========================================================
        // DETECCIÓN
        // =========================================================

        private readonly Dictionary<string, int> cocoObjects = new Dictionary<string, int>
        {
            { "Banana",   46 },
            { "Reloj",    74 },
            { "Pizza",    53 },
            { "Avión",     4 },
            { "Coche",     2 },
            { "Moto",      3 },
            { "Persona",   0 },
            { "Perro",    16 },
            { "Gato",     15 },
            { "Silla",    56 }
        };

        private void EnviarDeteccion()
        {
            Console.WriteLine($"Detección con {selectedObjectIds.Count} objetos seleccionados");
        }

        private void stopDetectionBtn_Click(object sender, EventArgs e)
        {
            selectedObjectIds.Clear();
            foreach (Control c in detectionPanel.Controls)
                if (c is Button b) b.BackColor = SystemColors.Control;
        }

        private void InicializarBotonesDeteccion()
        {
            foreach (var obj in cocoObjects)
            {
                string nombre = obj.Key;
                int id = obj.Value;

                var btn = new Button
                {
                    Text = nombre,
                    Size = new Size(75, 28),
                    Margin = new Padding(3)
                };

                btn.Click += (s, e) =>
                {
                    if (selectedObjectIds.Contains(id))
                    {
                        selectedObjectIds.Remove(id);
                        btn.BackColor = SystemColors.Control;
                    }
                    else
                    {
                        selectedObjectIds.Add(id);
                        btn.BackColor = Color.LightGreen;
                    }
                    EnviarDeteccion();
                };

                detectionPanel.Controls.Add(btn);
            }
        }

        // =========================================================
        // MAPA PRINCIPAL
        // =========================================================

        private void InicializarMapa()
        {
            gmap = new GMapControl();
            gmap.Dock = DockStyle.Fill;
            mapPanel.Controls.Add(gmap);

            gmap.MouseClick += Gmap_MouseClick;

            GMaps.Instance.Mode = AccessMode.ServerAndCache;
            gmap.MapProvider = GMapProviders.GoogleSatelliteMap;
            gmap.Position = new PointLatLng(41.27, 1.98);
            gmap.MinZoom = 2;
            gmap.MaxZoom = 25;
            gmap.Zoom = 19;
            gmap.ShowTileGridLines = false;
            gmap.RetryLoadTile = 3;
            gmap.ShowCenter = false;
            gmap.IgnoreMarkerOnMouseWheel = true;

            destinationOverlay = new GMapOverlay("destination");
            gmap.Overlays.Add(destinationOverlay);
            droneOverlay = new GMapOverlay("drone");
            gmap.Overlays.Add(droneOverlay);

            mapTimer = new System.Windows.Forms.Timer();
            mapTimer.Interval = 500;
            mapTimer.Tick += MapTimer_Tick;
            mapTimer.Start();
        }

        private void MapTimer_Tick(object sender, EventArgs e)
        {
            if (!posicionActualizada) return;
            if (double.IsNaN(currentLatDeg) || double.IsNaN(currentLonDeg)) return;

            posicionActualizada = false;

            var pos = new PointLatLng(currentLatDeg, currentLonDeg);

            if (droneMarker == null)
            {
                droneMarker = new RedDotMarker(pos, 8, currentHeading);
                droneOverlay.Markers.Add(droneMarker);
            }
            else
            {
                droneMarker.Position = pos;
                ((RedDotMarker)droneMarker).Heading = currentHeading;
            }

            if (!double.IsNaN(destLat) && !double.IsNaN(destLon))
            {
                double dLat = Math.Abs(currentLatDeg - destLat);
                double dLon = Math.Abs(currentLonDeg - destLon);
                if (dLat < ARRIVAL_THRESHOLD && dLon < ARRIVAL_THRESHOLD)
                {
                    destinationOverlay.Markers.Clear();
                    destLat = double.NaN;
                    destLon = double.NaN;
                }
            }

            gmap.Position = pos;
            gmap.Refresh();
        }

        private void ActualizarPosicionDron(double lat, double lon)
        {
            currentLatDeg = lat;
            currentLonDeg = lon;
            posicionActualizada = true;
        }

        private void Gmap_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                PointLatLng clickPos = gmap.FromLocalToLatLng(e.X, e.Y);
                double altDestino = double.IsNaN(currentAlt) ? 5 : currentAlt;

                destLat = clickPos.Lat;
                destLon = clickPos.Lng;

                destinationOverlay.Markers.Clear();
                destinationMarker = new BlueXMarker(clickPos, 6);
                destinationOverlay.Markers.Add(destinationMarker);

                EnviarDronHacia(clickPos.Lat, clickPos.Lng, altDestino);
                Console.WriteLine($"Destinación: Lat={clickPos.Lat}, Lon={clickPos.Lng}, Alt={altDestino}");
            }
        }

        private void captureBtn_Click(object sender, EventArgs e)
        {
            TomarFoto(true);
        }

        private void TomarFoto(bool mostrarMensaje)
        {
            byte[] jpeg;
            lock (jpegLock) jpeg = latestJpegBytes;

            Image imagenActiva = videoPictureBox?.Image;
            if (imagenActiva == null && bdVideoPreview != null)
                imagenActiva = bdVideoPreview.Image;

            if (jpeg == null && imagenActiva == null)
            {
                if (mostrarMensaje) MessageBox.Show("No hay stream de video activo.");
                else Console.WriteLine("[AVISO] Foto fallida: no hay stream de video activo.");
                return;
            }

            System.IO.Directory.CreateDirectory(photosFolder);
            string filename = $"drone_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
            string path = System.IO.Path.Combine(photosFolder, filename);

            try
            {
                if (jpeg != null)
                {
                    File.WriteAllBytes(path, jpeg);
                }
                else
                {
                    lock (imagenActiva)
                    {
                        using (Bitmap bmp = new Bitmap(imagenActiva.Width, imagenActiva.Height))
                        using (Graphics g = Graphics.FromImage(bmp))
                        {
                            g.DrawImage(imagenActiva, 0, 0);
                            bmp.Save(path, System.Drawing.Imaging.ImageFormat.Jpeg);
                        }
                    }
                }

                capturedPhotoPaths.Add(path);
                if (mostrarMensaje) MessageBox.Show($"Foto guardada en:\n{path}");
                else Console.WriteLine($"[ÉXITO] Foto automática guardada en: {path}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Guardando foto: {ex.Message}");
            }
        }

        private void galleryBtn_Click(object sender, EventArgs e)
        {
            if (capturedPhotoPaths.Count == 0)
            {
                MessageBox.Show("No hay fotos capturadas aún.");
                return;
            }
            using (var gallery = new GalleryForm(capturedPhotoPaths, photosFolder))
                gallery.ShowDialog();
        }

        // =========================================================
        // VUELO DE WAYPOINTS
        // =========================================================

        private void InicializarMapaVuelo()
        {
            // Pestaña "Vuelo" eliminada
        }

        

        // =========================================================
        // PESTAÑA CARGAR VUELO DESDE BASE DE DATOS
        // =========================================================

        private void InicializarMapaBD()
        {
            bdGmap = new GMapControl();
            bdGmap.Dock = DockStyle.Fill;
            bdMapPanel.Controls.Add(bdGmap);

            GMaps.Instance.Mode = AccessMode.ServerAndCache;
            bdGmap.MapProvider = GMapProviders.GoogleSatelliteMap;
            bdGmap.Position = new PointLatLng(41.27, 1.98);
            bdGmap.MinZoom = 2;
            bdGmap.MaxZoom = 25;
            bdGmap.Zoom = 19;
            bdGmap.ShowTileGridLines = false;
            bdGmap.ShowCenter = false;
            bdGmap.IgnoreMarkerOnMouseWheel = true;

            bdTraceOverlay = new GMapOverlay("bd_trace");
            bdGmap.Overlays.Add(bdTraceOverlay);
            bdWaypointLinesOverlay = new GMapOverlay("bd_lines");
            bdGmap.Overlays.Add(bdWaypointLinesOverlay);
            bdWaypointsOverlay = new GMapOverlay("bd_waypoints");
            bdGmap.Overlays.Add(bdWaypointsOverlay);
            bdDroneOverlay = new GMapOverlay("bd_drone");
            bdGmap.Overlays.Add(bdDroneOverlay);

            // Crear controles para preview de video
            CrearControlesPreviewVideo();
        }

        private void CrearControlesPreviewVideo()
        {
            // Los controles ya están en el Designer, solo inicializar el timer para preview en vivo
            bdVideoPreviewTimer = new System.Windows.Forms.Timer();
            bdVideoPreviewTimer.Interval = 66; // 15 FPS
            bdVideoPreviewTimer.Tick += BdVideoPreviewTimer_Tick;
        }

        private void BdVideoPreviewTimer_Tick(object sender, EventArgs e)
        {
            if (!bdVideoRecording) return;

            byte[] jpeg;
            lock (jpegLock)
            {
                if (latestJpegBytes == null) return;
                jpeg = new byte[latestJpegBytes.Length];
                Array.Copy(latestJpegBytes, jpeg, latestJpegBytes.Length);
            }

            if (jpeg != null && jpeg.Length > 0)
            {
                try
                {
                    using (var ms = new System.IO.MemoryStream(jpeg))
                    {
                        var img = new Bitmap(Image.FromStream(ms));
                        var old = bdVideoPreview.Image;
                        bdVideoPreview.Image = img;
                        old?.Dispose();
                    }
                }
                catch { }
            }
        }

        private void BdOpenFolderBtn_Click(object sender, EventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(bdVideoFilePath))
                {
                    string folderPath = System.IO.Path.GetDirectoryName(bdVideoFilePath);
                    System.Diagnostics.Process.Start(folderPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al abrir carpeta: {ex.Message}", "Error");
            }
        }

        private async Task CargarListaVuelos()
        {
            try
            {
                var vuelos = await droneAPIService.ObtenerTodosVuelosAsync();

                allVuelosCache.Clear();
                foreach (var vuelo in vuelos)
                {
                    allVuelosCache.Add(new VueloDto
                    {
                        Id = vuelo.ID,
                        Nametag = vuelo.NameTag,
                        Datetime = vuelo.Fecha.ToString("yyyy-MM-dd HH:mm:ss")
                    });
                }

                Console.WriteLine($"✓ Total de vuelos cargados: {allVuelosCache.Count}");

                flightPageIndex = 0;
                MostrarPaginaVuelos();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar vuelos: {ex.Message}");
                Console.WriteLine($"Error completo: {ex}");
            }
        }

        private void MostrarPaginaVuelos()
        {
            vuelosListBox.Items.Clear();

            int inicio = flightPageIndex * VUELOS_POR_PAGINA;
            int fin = Math.Min(inicio + VUELOS_POR_PAGINA, allVuelosCache.Count);

            if (inicio >= allVuelosCache.Count && flightPageIndex > 0)
            {
                flightPageIndex--;
                MostrarPaginaVuelos();
                return;
            }

            for (int i = inicio; i < fin; i++)
            {
                vuelosListBox.Items.Add(allVuelosCache[i]);
                Console.WriteLine($"✓ Vuelo página actual: ID={allVuelosCache[i].Id}, Nametag={allVuelosCache[i].Nametag}");
            }

            int paginaActual = flightPageIndex + 1;
            int totalPaginas = (allVuelosCache.Count + VUELOS_POR_PAGINA - 1) / VUELOS_POR_PAGINA;
            vuelosPageLabel.Text = $"Página {paginaActual} de {totalPaginas} ({allVuelosCache.Count} vuelos)";

            vuelosPreviousBtn.Enabled = flightPageIndex > 0;
            vuelosNextBtn.Enabled = (flightPageIndex + 1) < totalPaginas;

            if (vuelosListBox.Items.Count > 0)
                vuelosListBox.SelectedIndex = 0;
        }

        private void VuelosPreviousBtn_Click(object sender, EventArgs e)
        {
            if (flightPageIndex > 0)
            {
                flightPageIndex--;
                MostrarPaginaVuelos();
            }
        }

        private void VuelosNextBtn_Click(object sender, EventArgs e)
        {
            int totalPaginas = (allVuelosCache.Count + VUELOS_POR_PAGINA - 1) / VUELOS_POR_PAGINA;
            if (flightPageIndex + 1 < totalPaginas)
            {
                flightPageIndex++;
                MostrarPaginaVuelos();
            }
        }

        private async void loadFromDBBtn_Click(object sender, EventArgs e)
        {
            if (vuelosListBox.SelectedItem == null)
            {
                MessageBox.Show("Selecciona un vuelo de la lista.");
                return;
            }

            var vuelo = (VueloDto)vuelosListBox.SelectedItem;
            await CargarWaypointsBD(vuelo.Id);
        }

        private async void VuelosListBox_DoubleClick(object sender, EventArgs e)
        {
            if (vuelosListBox.SelectedItem == null)
                return;

            var vuelo = (VueloDto)vuelosListBox.SelectedItem;
            await CargarWaypointsBD(vuelo.Id);
        }

        private async Task CargarWaypointsBD(string vueloId)
        {
            try
            {
                if (string.IsNullOrEmpty(vueloId))
                {
                    MessageBox.Show("ID de vuelo inválido.");
                    return;
                }

                Console.WriteLine($"Cargando waypoints para vuelo: {vueloId}");

                var vuelo = await droneAPIService.CargarRutaAsync(vueloId);

                if (vuelo.Instrucciones == null || vuelo.Instrucciones.Count == 0)
                {
                    MessageBox.Show("Este vuelo no tiene instrucciones registradas.");
                    return;
                }

                currentBdFirstInstruccionId = vuelo.Instrucciones[0].Id;

                bdWaypoints.Clear();
                bdWaypointsListBox.Items.Clear();
                bdWaypointsOverlay.Markers.Clear();
                bdWaypointLinesOverlay.Routes.Clear();
                bdTrace.Clear();
                bdDroneOverlay.Markers.Clear();
                bdTraceOverlay.Markers.Clear();

                int count = 0;
                foreach (var instr in vuelo.Instrucciones)
                {
                    if (instr.Punto == null)
                    {
                        Console.WriteLine($"Instrucción sin punto en trail {instr.Trail}");
                        continue;
                    }

                    count++;
                    float altitud = !double.IsNaN(instr.Punto.Altitud) ? (float)instr.Punto.Altitud : FLIGHT_ALTITUDE;
                    float heading = !double.IsNaN(instr.Punto.Heading) ? (float)instr.Punto.Heading : 0f;

                    var waypointData = new WaypointData(
                        instr.Punto.Lat,
                        instr.Punto.Long,
                        altitud,
                        heading,
                        instr.Directriz
                    );
                    waypointData.InstruccionId = instr.Id; // Guardar el Id para la grabación de video si es necesario

                    bdWaypoints.Add(waypointData);
                    bdWaypointsListBox.Items.Add($"WP {count}: ({instr.Punto.Lat:F6}, {instr.Punto.Long:F6}) Alt: {altitud}m | Dir: {instr.Directriz}");

                    var marker = new WaypointMarker(waypointData.Position, count);
                    bdWaypointsOverlay.Markers.Add(marker);

                    Console.WriteLine($"✓ Waypoint {count} (trail {instr.Trail}): Lat={instr.Punto.Lat}, Lon={instr.Punto.Long}, Alt={altitud}m");
                }

                for (int i = 0; i < bdWaypoints.Count - 1; i++)
                {
                    var route = new GMapRoute(
                        new List<PointLatLng> { bdWaypoints[i].Position, bdWaypoints[i + 1].Position }, "route");
                    route.Stroke = new Pen(Color.Blue, 1);
                    bdWaypointLinesOverlay.Routes.Add(route);
                }

                bdWaypointsLabel.Text = $"Waypoints ({bdWaypoints.Count})";
                bdGmap.Refresh();

                if (bdWaypoints.Count > 0)
                {
                    double minLat = bdWaypoints[0].Position.Lat;
                    double maxLat = bdWaypoints[0].Position.Lat;
                    double minLng = bdWaypoints[0].Position.Lng;
                    double maxLng = bdWaypoints[0].Position.Lng;

                    foreach (var wp in bdWaypoints)
                    {
                        if (wp.Position.Lat < minLat) minLat = wp.Position.Lat;
                        if (wp.Position.Lat > maxLat) maxLat = wp.Position.Lat;
                        if (wp.Position.Lng < minLng) minLng = wp.Position.Lng;
                        if (wp.Position.Lng > maxLng) maxLng = wp.Position.Lng;
                    }

                    double centerLat = (minLat + maxLat) / 2;
                    double centerLng = (minLng + maxLng) / 2;
                    bdGmap.Position = new PointLatLng(centerLat, centerLng);

                    double maxDistance = 0;
                    foreach (var wp in bdWaypoints)
                    {
                        double dLat = Math.Abs(wp.Position.Lat - centerLat);
                        double dLng = Math.Abs(wp.Position.Lng - centerLng);
                        double distance = Math.Sqrt(dLat * dLat + dLng * dLng);
                        if (distance > maxDistance) maxDistance = distance;
                    }

                    if (maxDistance > 0.1) bdGmap.Zoom = 13;
                    else if (maxDistance > 0.01) bdGmap.Zoom = 16;
                    else if (maxDistance > 0.001) bdGmap.Zoom = 18;
                    else bdGmap.Zoom = 19;

                    MessageBox.Show($"Cargados {bdWaypoints.Count} waypoints.", "Éxito");

                    try
                    {
                        var urls = await droneAPIService.ObtenerMediaInstruccionAsync(currentBdFirstInstruccionId);
                        var videoUrl = urls.FirstOrDefault(u => u.EndsWith(".avi", StringComparison.OrdinalIgnoreCase) || u.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) || u.Contains("video/upload"));

                        if (!string.IsNullOrEmpty(videoUrl))
                        {
                            PlayVideoInPreview(videoUrl);
                        }
                    }
                    catch (Exception me)
                    {
                        Console.WriteLine($"[AVISO] No se encontraron vídeos: {me.Message}");
                    }
                }
                else
                {
                    MessageBox.Show("Este vuelo no tiene instrucciones con puntos GPS.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar instrucciones: {ex.Message}");
                Console.WriteLine($"Error completo: {ex}");
            }
        }

        private void PlayVideoInPreview(string urlMedia)
        {
            if (bdVideoPlaybackBrowser == null) return;

            // Transcodificar al vuelo desde Cloudinary cambiando la extensión a mp4 soportado por IE
            if (urlMedia.Contains("cloudinary") && (urlMedia.EndsWith(".avi") || urlMedia.EndsWith(".mjpeg")))
            {
                urlMedia = urlMedia.Replace(".avi", ".mp4").Replace(".mjpeg", ".mp4");
            }

            string html = $@"
            <!DOCTYPE html>
            <html>
            <head>
                <meta http-equiv='X-UA-Compatible' content='IE=edge' />
                <style>
                    html, body {{ margin:0; padding:0; background:black; height:100%; overflow:hidden; }}
                    .center {{ display: flex; justify-content: center; align-items: center; height: 100%; }}
                </style>
            </head>
            <body>
                <div class='center'>
                    <video src='{urlMedia}' controls autoplay style='max-width:100%; max-height:100%;'></video>
                </div>
            </body>
            </html>";

            bdVideoPlaybackBrowser.DocumentText = html;
        }

        private void startBDFlightBtn_Click(object sender, EventArgs e)
        {
            if (bdWaypoints.Count == 0)
            {
                MessageBox.Show("Carga un vuelo desde la BD primero.");
                return;
            }

            bdIsFlying = true;
            bdIsPaused = false;
            bdCurrentWaypointIndex = 0;
            bdDirectrizEjecutada = false;
            isBdWaiting = false;
            bdTrace.Clear();
            bdDroneOverlay.Markers.Clear();
            bdTraceOverlay.Markers.Clear();

            startBDFlightBtn.Enabled = false;
            loadFromDBBtn.Enabled = false;
            pauseResumeBDBtn.Enabled = true;
            pauseResumeBDBtn.Text = "Pausar Vuelo";

            // Iniciar grabación de video automáticamente si no hay directrices explícitas de video
            bool hasVideoDirectives = bdWaypoints.Any(w => w.Directriz == "StartVideoRecording" || w.Directriz == "StopVideoRecording");
            if (!hasVideoDirectives)
            {
                Console.WriteLine("[INFO] No se encontraron directrices de video. Grabando vuelo completo por defecto.");
                IniciarGrabacionVideoBD();
            }

            if (bdWaypointTimer == null)
            {
                bdWaypointTimer = new System.Windows.Forms.Timer();
                bdWaypointTimer.Interval = 500;
                bdWaypointTimer.Tick += BDWaypointTimer_Tick;
            }
            bdWaypointTimer.Start();

            Console.WriteLine($"[BD FLIGHT START] Iniciando vuelo con {bdWaypoints.Count} waypoints");
            Console.WriteLine($"[TELEMETRIA ACTUAL] Lat={currentLatDeg:F8}, Lon={currentLonDeg:F8}, Alt={currentAlt:F2}m");
            Console.WriteLine($"[VIDEO GRABANDO] Archivo: {bdVideoFilePath}");

            var wp = bdWaypoints[bdCurrentWaypointIndex];
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    if (double.IsNaN(currentAlt) || currentAlt < 1.5)
                    {
                        int altDespegue = (int)wp.Altitud;
                        if (altDespegue < 1) altDespegue = 5; // Altura mínima de seguridad

                        Console.WriteLine($"[COMANDO ENVIADO] Dron en tierra. Despegando a la altitud del primer WP: {altDespegue}m");
                        dron.PonModoGuiado();
                        Thread.Sleep(300);
                        dron.Despegar(altDespegue, bloquear: true, EnAire, "Volando");
                        Thread.Sleep(500);
                    }

                    if (wp.Heading != 0 && wp.Directriz != "ChangeHeading")
                    {
                        Console.WriteLine($"[COMANDO ENVIADO] Fijando heading a {wp.Heading}° para trayecto al primer WP");
                        dron.EscribirParametros(new List<(string, float)> { ("WP_YAW_BEHAVIOR", 0f) });
                        Thread.Sleep(300);

                        dron.CambiarHeading(wp.Heading, bloquear: true);
                    }
                    else
                    {
                        dron.EscribirParametros(new List<(string, float)> { ("WP_YAW_BEHAVIOR", 1f) });
                        Thread.Sleep(300);
                    }
                    dron.IrAlPunto((float)wp.Position.Lat, (float)wp.Position.Lng, wp.Altitud);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Al navegar al primer waypoint: {ex.Message}");
                    Console.WriteLine($"[ERROR STACK] {ex.StackTrace}");
                }
            });
        }

        private void IniciarGrabacionVideoBD()
        {
            try
            {
                // Crear archivo en Desktop (visible y accesible)
                string videoFolder = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "DroneVideos");
                System.IO.Directory.CreateDirectory(videoFolder);

                bdVideoFilePath = System.IO.Path.Combine(
                    videoFolder,
                    $"bd_flight_{DateTime.Now:yyyyMMdd_HHmmss}.avi"
                );

                bdVideoRecording = true;
                bdVideoFrames.Clear();
                bdFramesCaptured = 0;
                bdVideoSizeBytes = 0;

                // Iniciar timer para capturar frames
                if (bdVideoFrameTimer == null)
                {
                    bdVideoFrameTimer = new System.Windows.Forms.Timer();
                    bdVideoFrameTimer.Interval = 66; // Capturar 15 FPS (mismo que displayTimer)
                    bdVideoFrameTimer.Tick += BdVideoFrameTimer_Tick;
                }
                bdVideoFrameTimer.Start();

                // Iniciar timer para mostrar preview en vivo
                if (bdVideoPreviewTimer != null)
                    bdVideoPreviewTimer.Start();

                // Actualizar UI
                if (bdRecordingStatusLabel != null)
                {
                    bdRecordingStatusLabel.Text = $"🔴 GRABANDO: {System.IO.Path.GetFileName(bdVideoFilePath)}";
                    bdRecordingStatusLabel.ForeColor = Color.Red;
                }

                if (bdOpenFolderBtn != null)
                {
                    bdOpenFolderBtn.Enabled = true;
                }

                // Mostrar indicador visual de grabación
                MostrarIndicadorGrabacion(true, bdVideoFilePath);
                Console.WriteLine($"[VIDEO] Iniciada grabación en: {bdVideoFilePath}");
                Console.WriteLine($"[VIDEO] Preview iniciado - {bdRecordingStatusLabel?.Text}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR VIDEO] No se pudo iniciar grabación: {ex.Message}");
                bdVideoRecording = false;
                MostrarIndicadorGrabacion(false, "");
            }
        }

        private void MostrarIndicadorGrabacion(bool grabando, string rutaArchivo)
        {
            try
            {
                // Buscar o crear label para mostrar estado
                Label lblGrabando = this.Controls.Find("lblGrabando", true).FirstOrDefault() as Label;

                if (lblGrabando == null)
                {
                    // Si no existe, crearlo dinámicamente cerca del botón de iniciar
                    lblGrabando = new Label
                    {
                        Name = "lblGrabando",
                        AutoSize = true,
                        Font = new Font("Arial", 11, FontStyle.Bold),
                        ForeColor = Color.Red
                    };
                    this.Controls.Add(lblGrabando);
                    lblGrabando.BringToFront();
                }

                if (grabando)
                {
                    lblGrabando.Text = $"🔴 GRABANDO: {System.IO.Path.GetFileName(rutaArchivo)}";
                    lblGrabando.ForeColor = Color.Red;
                    lblGrabando.Visible = true;
                }
                else
                {
                    lblGrabando.Text = "";
                    lblGrabando.Visible = false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] No se pudo mostrar indicador: {ex.Message}");
            }
        }

        private void BdVideoFrameTimer_Tick(object sender, EventArgs e)
        {
            if (!bdVideoRecording) return;

            byte[] currentRef;
            lock (jpegLock)
            {
                currentRef = latestJpegBytes;
            }

            if (currentRef == null || object.ReferenceEquals(currentRef, lastProcessedVidFrame))
                return;

            lastProcessedVidFrame = currentRef;

            byte[] jpeg = new byte[currentRef.Length];
            Array.Copy(currentRef, jpeg, currentRef.Length);

            bdVideoFrames.Add(jpeg);
            bdVideoSizeBytes += jpeg.Length;
            bdFramesCaptured++;

            // Actualizar estado cada 5 frames
            if (bdFramesCaptured % 5 == 0)
            {
                if (bdRecordingStatusLabel != null)
                {
                    bdRecordingStatusLabel.Text = $"🔴 GRABANDO: {System.IO.Path.GetFileName(bdVideoFilePath)} | {bdFramesCaptured} frames | {bdVideoSizeBytes / 1024} KB";
                    bdRecordingStatusLabel.ForeColor = Color.Red;
                }
            }
        }

        private async Task DetenerGrabacionVideoBD(string instruccionId)
        {
            try
            {
                bdVideoRecording = false;
                MostrarIndicadorGrabacion(false, "");
                if (bdVideoFrameTimer != null)
                    bdVideoFrameTimer.Stop();
                if (bdVideoPreviewTimer != null)
                    bdVideoPreviewTimer.Stop();

                MostrarIndicadorGrabacion(false, "");

                long totalSizeBytes = bdVideoSizeBytes;
                Console.WriteLine($"[VIDEO] Grabación detenida. {bdVideoFrames.Count} frames capturados | {totalSizeBytes / 1024} KB");

                if (bdVideoFrames.Count > 0)
                {
                    // Guardar video como archivo MP4 simple (concatenación de JPEGs)
                    GuardarVideoDesdeFrames(bdVideoFilePath, bdVideoFrames);

                    // Actualizar estado con resultado
                    if (bdRecordingStatusLabel != null)
                    {
                        bdRecordingStatusLabel.Text = $"✓ Guardado: {bdVideoFrames.Count} frames | {totalSizeBytes / 1024} KB | Subiendo...";
                        bdRecordingStatusLabel.ForeColor = Color.LimeGreen;
                    }

                    // Subir a Cloudinary
                    if (droneAPIService != null && !string.IsNullOrEmpty(instruccionId))
                    {
                        try
                        {
                            Console.WriteLine($"[VIDEO] Subiendo vídeo a Cloudinary...");
                            string urlMedia = await droneAPIService.SubirMediaAsync(bdVideoFilePath, instruccionId, "video");
                            Console.WriteLine($"[VIDEO] Vídeo subido exitosamente: {urlMedia}");

                            if (bdRecordingStatusLabel != null)
                            {
                                bdRecordingStatusLabel.Text = $"✓ SUBIDO: {bdVideoFrames.Count} frames | {totalSizeBytes / 1024} KB";
                                bdRecordingStatusLabel.ForeColor = Color.LimeGreen;
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[ERROR] No se pudo subir vídeo: {ex.Message}");
                            if (bdRecordingStatusLabel != null)
                            {
                                bdRecordingStatusLabel.Text = $"✓ Guardado: {bdVideoFrames.Count} frames | {totalSizeBytes / 1024} KB | (Error subida)";
                                bdRecordingStatusLabel.ForeColor = Color.Orange;
                            }
                        }
                    }
                }

                bdVideoFrames.Clear();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR VIDEO] No se pudo detener grabación: {ex.Message}");
            }
        }

        private void GuardarVideoDesdeFrames(string filePath, List<byte[]> frames)
        {
            try
            {
                int fps = 15;
                int width = 640;
                int height = 480;

                if (frames.Count > 0)
                {
                    try
                    {
                        using (var ms = new System.IO.MemoryStream(frames[0]))
                        using (var img = Image.FromStream(ms))
                        {
                            width = img.Width;
                            height = img.Height;
                        }
                    } catch {}
                }

                // Generador minimalista de contenedor AVI en C# (Codec MJPEG)
                using (var fs = new System.IO.FileStream(filePath, System.IO.FileMode.Create))
                using (var bw = new System.IO.BinaryWriter(fs))
                {
                    int moviListSize = 4 + frames.Sum(f => 8 + f.Length + (f.Length % 2));
                    int idx1Size = 8 + (frames.Count * 16);
                    int fileSize = 4 + (8 + 192) + (8 + moviListSize) + idx1Size;

                    bw.Write(Encoding.ASCII.GetBytes("RIFF"));
                    bw.Write(fileSize);
                    bw.Write(Encoding.ASCII.GetBytes("AVI "));

                    bw.Write(Encoding.ASCII.GetBytes("LIST"));
                    bw.Write(192);
                    bw.Write(Encoding.ASCII.GetBytes("hdrl"));

                    bw.Write(Encoding.ASCII.GetBytes("avih"));
                    bw.Write(56);
                    bw.Write(1000000 / fps);
                    bw.Write(0);
                    bw.Write(0);
                    bw.Write(0);
                    bw.Write(frames.Count);
                    bw.Write(0);
                    bw.Write(1);
                    bw.Write(0);
                    bw.Write(width);
                    bw.Write(height);
                    bw.Write(0);
                    bw.Write(0);
                    bw.Write(0);
                    bw.Write(0);

                    bw.Write(Encoding.ASCII.GetBytes("LIST"));
                    bw.Write(116);
                    bw.Write(Encoding.ASCII.GetBytes("strl"));

                    bw.Write(Encoding.ASCII.GetBytes("strh"));
                    bw.Write(56);
                    bw.Write(Encoding.ASCII.GetBytes("vids"));
                    bw.Write(Encoding.ASCII.GetBytes("MJPG"));
                    bw.Write(0);
                    bw.Write(0);
                    bw.Write(0);
                    bw.Write(1);
                    bw.Write(fps);
                    bw.Write(0);
                    bw.Write(frames.Count);
                    bw.Write(0);
                    bw.Write(-1);
                    bw.Write(0);
                    bw.Write((short)0);
                    bw.Write((short)0);
                    bw.Write((short)width);
                    bw.Write((short)height);

                    bw.Write(Encoding.ASCII.GetBytes("strf"));
                    bw.Write(40);
                    bw.Write(40);
                    bw.Write(width);
                    bw.Write(height);
                    bw.Write((short)1);
                    bw.Write((short)24);
                    bw.Write(Encoding.ASCII.GetBytes("MJPG"));
                    bw.Write(width * height * 3);
                    bw.Write(0);
                    bw.Write(0);
                    bw.Write(0);
                    bw.Write(0);

                    bw.Write(Encoding.ASCII.GetBytes("LIST"));
                    bw.Write(moviListSize);
                    bw.Write(Encoding.ASCII.GetBytes("movi"));

                    int moviOffset = 4;
                    List<int> idxOffsets = new List<int>(frames.Count);
                    List<int> idxLengths = new List<int>(frames.Count);

                    foreach (var f in frames)
                    {
                        idxOffsets.Add(moviOffset);
                        idxLengths.Add(f.Length);

                        bw.Write(Encoding.ASCII.GetBytes("00dc"));
                        bw.Write(f.Length);
                        bw.Write(f);
                        if (f.Length % 2 != 0)
                        {
                            bw.Write((byte)0);
                            moviOffset += 8 + f.Length + 1;
                        }
                        else
                        {
                            moviOffset += 8 + f.Length;
                        }
                    }

                    // Escribir chunk idx1 para indexar fotogramas (duración y navegación)
                    bw.Write(Encoding.ASCII.GetBytes("idx1"));
                    bw.Write(frames.Count * 16);
                    for (int i = 0; i < frames.Count; i++)
                    {
                        bw.Write(Encoding.ASCII.GetBytes("00dc"));
                        bw.Write(16); // 16 = 0x10 = AVIIF_KEYFRAME
                        bw.Write(idxOffsets[i]);
                        bw.Write(idxLengths[i]);
                    }
                }

                Console.WriteLine($"[VIDEO] Archivo AVI configurado: {filePath} ({new System.IO.FileInfo(filePath).Length / 1024}KB)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] No se pudo guardar vídeo: {ex.Message}");
            }
        }

        private void stopBDFlightBtn_Click(object sender, EventArgs e)
        {
            bdIsFlying = false;
            bdIsPaused = false;
            bdWaypointTimer?.Stop();
            bdVideoFrameTimer?.Stop();
            if (bdVideoPreviewTimer != null)
                bdVideoPreviewTimer.Stop();

            dron.Navegar("Stop");

            startBDFlightBtn.Enabled = true;
            loadFromDBBtn.Enabled = true;
            pauseResumeBDBtn.Enabled = false;
            pauseResumeBDBtn.BackColor = SystemColors.Control;
            pauseResumeBDBtn.Text = "Pausar Vuelo";
            bdCurrentWaypointIndex = 0;

            bdTrace.Clear();
            bdDroneOverlay.Markers.Clear();
            bdTraceOverlay.Markers.Clear();
            bdGmap.Refresh();

            bdVideoRecording = false;
            MostrarIndicadorGrabacion(false, "");

            if (bdRecordingStatusLabel != null)
            {
                bdRecordingStatusLabel.Text = "Vuelo detenido";
                bdRecordingStatusLabel.ForeColor = Color.Orange;
            }

            // Si se detiene y estábamos grabando, detener y subir
            if (bdVideoFrames.Count > 0)
            {
                Task.Run(() => DetenerGrabacionVideoBD(currentBdFirstInstruccionId));
            }
        }

        private void pauseResumeBDBtn_Click(object sender, EventArgs e)
        {
            if (!bdIsFlying)
                return;

            if (bdIsPaused)
            {
                bdIsPaused = false;
                pauseResumeBDBtn.Text = "Pausar Vuelo";
                pauseResumeBDBtn.BackColor = SystemColors.Control;
                Console.WriteLine("Vuelo reanudado desde waypoint " + (bdCurrentWaypointIndex + 1));

                if (bdCurrentWaypointIndex < bdWaypoints.Count)
                {
                    var wp = bdWaypoints[bdCurrentWaypointIndex];
                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        try
                        {
                            dron.IrAlPunto((float)wp.Position.Lat, (float)wp.Position.Lng, wp.Altitud);
                            Console.WriteLine($"Reanudado: Dirigiéndose a waypoint {bdCurrentWaypointIndex + 1}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error al reanudar vuelo: {ex.Message}");
                        }
                    });
                }
            }
            else
            {
                bdIsPaused = true;
                pauseResumeBDBtn.Text = "Reanudar Vuelo";
                pauseResumeBDBtn.BackColor = Color.Yellow;
                dron.Navegar("Stop");
                Console.WriteLine("Vuelo pausado en waypoint " + (bdCurrentWaypointIndex + 1));
            }
        }

        private void pauseResumeBtn_Click(object sender, EventArgs e)
        {
            // Función removida - Pestaña "Vuelo" eliminada
        }

        private void BDWaypointTimer_Tick(object sender, EventArgs e)
        {
            if (!bdIsFlying || bdCurrentWaypointIndex >= bdWaypoints.Count)
            {
                bdIsFlying = false;
                bdIsPaused = false;
                bdWaypointTimer.Stop();
                startBDFlightBtn.Enabled = true;
                loadFromDBBtn.Enabled = true;
                pauseResumeBDBtn.Enabled = false;
                pauseResumeBDBtn.BackColor = SystemColors.Control;
                pauseResumeBDBtn.Text = "Pausar Vuelo";

                Console.WriteLine("[BD FLIGHT COMPLETE] Vuelo completado - Todos los waypoints alcanzados");
                MessageBox.Show("Vuelo completado - Todos los waypoints alcanzados.");

                // Detener grabación de video siempre al final, asegurándonos de que si empezó por defecto (o explícita), termine.
                if (bdVideoRecording)
                {
                    Task.Run(() => DetenerGrabacionVideoBD(currentBdFirstInstruccionId));
                    MessageBox.Show("Vuelo completado - Todos los waypoints alcanzados.\nVídeo guardado y en proceso de subida.");
                }

                bdTrace.Clear();
                bdDroneOverlay.Markers.Clear();
                bdTraceOverlay.Markers.Clear();
                bdGmap.Refresh();
                return;
            }

            if (bdIsPaused)
                return;

            if (double.IsNaN(currentLatDeg) || double.IsNaN(currentLonDeg))
            {
                Console.WriteLine($"[WARNING] Telemetría NO disponible - Lat={currentLatDeg}, Lon={currentLonDeg}");
                return;
            }

            var dronePos = new PointLatLng(currentLatDeg, currentLonDeg);
            if (bdDroneMarker == null)
            {
                bdDroneMarker = new RedDotMarker(dronePos, 8, currentHeading);
                bdDroneOverlay.Markers.Add(bdDroneMarker);
            }
            else
            {
                bdDroneMarker.Position = dronePos;
                ((RedDotMarker)bdDroneMarker).Heading = currentHeading;
            }

            var currentWp = bdWaypoints[bdCurrentWaypointIndex];
            double dLat = Math.Abs(currentLatDeg - currentWp.Position.Lat);
            double dLon = Math.Abs(currentLonDeg - currentWp.Position.Lng);

            Console.WriteLine($"[WAYPOINT {bdCurrentWaypointIndex + 1}] Actual: ({currentLatDeg:F8}, {currentLonDeg:F8}) | Target: ({currentWp.Position.Lat:F8}, {currentWp.Position.Lng:F8}) | ΔLat={dLat:E6}, ΔLon={dLon:E6} | Threshold={WAYPOINT_ARRIVAL_THRESHOLD:E6}");

            if (dLat < WAYPOINT_ARRIVAL_THRESHOLD && dLon < WAYPOINT_ARRIVAL_THRESHOLD)
            {
                if (!bdDirectrizEjecutada && currentWp.Directriz != "None")
                {
                    EjecutarDirectrizWaypointBD(currentWp);
                    bdDirectrizEjecutada = true;
                }

                if (isBdWaiting)
                {
                    if (DateTime.Now < bdWaitEndTime) return;
                    isBdWaiting = false; // Fin de la espera
                }

                Console.WriteLine($"[WAYPOINT {bdCurrentWaypointIndex + 1} REACHED] ✓ Llegamos al waypoint");
                bdCurrentWaypointIndex++;
                bdDirectrizEjecutada = false; // Preparar para el siguiente waypoint

                if (bdCurrentWaypointIndex < bdWaypoints.Count)
                {
                    var nextWp = bdWaypoints[bdCurrentWaypointIndex];
                    var currentIndex = bdCurrentWaypointIndex;

                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        try
                        {
                            if (currentWp.Heading != 0 && currentWp.Directriz != "ChangeHeading")
                            {
                                Console.WriteLine($"[COMANDO ENVIADO] Fijando heading a {currentWp.Heading}° para el trayecto hacia WP {currentIndex + 1}");
                                dron.EscribirParametros(new List<(string, float)> { ("WP_YAW_BEHAVIOR", 0f) });
                                Thread.Sleep(300);

                                dron.CambiarHeading(currentWp.Heading, bloquear: true);
                            }
                            else
                            {
                                dron.EscribirParametros(new List<(string, float)> { ("WP_YAW_BEHAVIOR", 1f) });
                                Thread.Sleep(300);
                            }

                            dron.IrAlPunto((float)nextWp.Position.Lat, (float)nextWp.Position.Lng, nextWp.Altitud);
                        }
                        catch (Exception ex)
                        {
                            // Error al navegar a waypoint
                        }
                    });

                    this.BeginInvoke((MethodInvoker)delegate
                    {
                        if (bdWaypointsListBox.Items.Count > currentIndex)
                            bdWaypointsListBox.SelectedIndex = currentIndex;
                    });
                }
            }

            bdGmap.Refresh();
        }

        private void EjecutarDirectrizWaypointBD(WaypointData wp)
        {
            if (string.IsNullOrEmpty(wp.Directriz) || wp.Directriz == "None") return;

            try
            {
                switch (wp.Directriz)
                {
                    case "TakePhoto":
                        TomarFoto(false); // Toma la foto guardándola silenciosamente
                        isBdWaiting = true;
                        bdWaitEndTime = DateTime.Now.AddSeconds(2); // Detiene el dron 2 segundos para estabilizarse y no saltar al siguiente WP
                        break;
                    case "StartVideoRecording":
                        if (!bdVideoRecording) IniciarGrabacionVideoBD();
                        break;
                    case "StopVideoRecording":
                        if (bdVideoRecording)
                        {
                             Task.Run(() => DetenerGrabacionVideoBD(wp.InstruccionId ?? currentBdFirstInstruccionId));
                        }
                        break;
                    case "ChangeAltitude":
                        dron.IrAlPunto((float)currentLatDeg, (float)currentLonDeg, wp.Altitud);
                        isBdWaiting = true;
                        bdWaitEndTime = DateTime.Now.AddSeconds(3); // Dar tiempo al dron para ajustar altitud
                        break;
                    case "ChangeHeading":
                        dron.CambiarHeading(wp.Heading, bloquear: false);
                        isBdWaiting = true;
                        bdWaitEndTime = DateTime.Now.AddSeconds(3); // Dar tiempo al dron para rotar
                        break;
                    case "Hover":
                        dron.Navegar("Stop");
                        isBdWaiting = true;
                        bdWaitEndTime = DateTime.Now.AddSeconds(5); // Mantener 5 segundos
                        break;
                    case "Wait":
                        dron.Navegar("Stop");
                        isBdWaiting = true;
                        bdWaitEndTime = DateTime.Now.AddSeconds(5); // Esperar 5 segundos
                        break;
                    case "ReturnToHome":
                        RTLBtn_Click(null, null);
                        isBdWaiting = true;
                        bdWaitEndTime = DateTime.Now.AddHours(1); // Detiene efectivamente la secuencia de waypoints
                        stopBDFlightBtn_Click(null, null);
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR DIRECTRIZ] {ex.Message}");
            }
        }

        // =========================================================
        // PESTAÑA EDICIÓN DE RUTAS (Form2)
        // =========================================================

        private void InicializarMapaEdicion()
        {
            editGmap = new GMapControl();
            editGmap.Dock = DockStyle.Fill;
            panelMap.Controls.Add(editGmap);

            GMaps.Instance.Mode = AccessMode.ServerAndCache;
            editGmap.MapProvider = GMapProviders.GoogleSatelliteMap;
            editGmap.Position = new PointLatLng(41.27641, 1.98862);
            editGmap.MinZoom = 2;
            editGmap.MaxZoom = 25;
            editGmap.Zoom = 19;
            editGmap.ShowTileGridLines = false;
            editGmap.ShowCenter = false;
            editGmap.IgnoreMarkerOnMouseWheel = true;
            editGmap.CanDragMap = true;
            editGmap.RetryLoadTile = 3;

            editRouteOverlay = new GMapOverlay("route");
            editGmap.Overlays.Add(editRouteOverlay);

            editWaypointsOverlay = new GMapOverlay("waypoints");
            editGmap.Overlays.Add(editWaypointsOverlay);

            editGmap.MouseClick += EditGmap_MouseClick;
        }

        private void EditGmap_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                var posScreen = new Point(e.X, e.Y);
                var posGMap = editGmap.FromLocalToLatLng(posScreen.X, posScreen.Y);

                int puntoId = (int)(DateTime.Now.Ticks % int.MaxValue);
                Punto punto = new Punto(puntoId, posGMap.Lat, posGMap.Lng);

                int instruccionId = (int)(DateTime.Now.Ticks % int.MaxValue);
                Instruccion instr = new Instruccion(instruccionId, punto);
                instrucciones.Add(instr);

                AgregarMarcadorEdicion(instr);
                ActualizarListaInstrucciones();
                ActualizarRutaEdicion();

                // Novedad: Seleccionar automáticamente el punto recién creado en el ListBox
                ListBox listBox = this.Controls.Find("waypointListBox", true).FirstOrDefault() as ListBox;
                if (listBox != null && instrucciones.Count > 0)
                {
                    listBox.SelectedIndex = instrucciones.Count - 1;
                }
            }
        }

        private void AgregarMarcadorEdicion(Instruccion instr)
        {
            var posGMap = new PointLatLng(instr.Punto.Lat, instr.Punto.Long);

            // Utilizamos el mismo WaypointMarker pero sin el tooltip persistente 
            // para que se vea idéntico a la pestaña de Base de Datos
            var marker = new WaypointMarker(posGMap, instr.VisualId);
            marker.Tag = instr.Id;

            // Si aún quieres ver el ID al pasar el ratón, descomenta la siguiente línea:
            // marker.ToolTipText = $"I{instr.VisualId}"; 
            // marker.ToolTipMode = MarkerTooltipMode.OnMouseOver; // En lugar de Always

            editWaypointsOverlay.Markers.Add(marker);
            editGmap.Refresh();
        }

        private void ActualizarListaInstrucciones()
        {
            ListBox listBox = this.Controls.Find("waypointListBox", true).FirstOrDefault() as ListBox;

            if (listBox != null)
            {
                listBox.DataSource = null;
                listBox.DataSource = new List<Instruccion>(instrucciones);
                listBox.DisplayMember = "ToString";
            }
        }

        private void ListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            ListBox listBox = sender as ListBox;
            if (listBox.SelectedIndex >= 0)
            {
                instruccionSeleccionada = instrucciones[listBox.SelectedIndex];
                CargarDatosInstruccion(instruccionSeleccionada);
            }
        }

        private void InicializarControlesFunciones()
        {
            ComboBox cbFuncion = this.Controls.Find("cbFuncion", true).FirstOrDefault() as ComboBox;
            if (cbFuncion != null)
            {
                cbFuncion.DataSource = Enum.GetValues(typeof(Directriz)).Cast<Directriz>().ToList();
            }

            ListBox listBox = this.Controls.Find("waypointListBox", true).FirstOrDefault() as ListBox;
            if (listBox != null)
            {
                listBox.SelectedIndexChanged += ListBox_SelectedIndexChanged;
            }

            Button btnAplicar = this.Controls.Find("btnAplicar", true).FirstOrDefault() as Button;
            if (btnAplicar != null)
            {
                btnAplicar.Click += BtnAplicar_Click;
            }

            Button btnSubir = this.Controls.Find("btnSubir", true).FirstOrDefault() as Button;
            if (btnSubir != null)
            {
                btnSubir.Click += BtnSubir_Click;
            }

            Button btnBajar = this.Controls.Find("btnBajar", true).FirstOrDefault() as Button;
            if (btnBajar != null)
            {
                btnBajar.Click += BtnBajar_Click;
            }

            Button btnEliminar = this.Controls.Find("btnEliminar", true).FirstOrDefault() as Button;
            if (btnEliminar != null)
            {
                btnEliminar.Click += BtnEliminar_Click;
            }

            Button btnLimpiarRuta = this.Controls.Find("btnLimpiarRuta", true).FirstOrDefault() as Button;
            if (btnLimpiarRuta != null)
            {
                btnLimpiarRuta.Click += BtnLimpiarRuta_Click;
            }

            Button btnGuardarRuta = this.Controls.Find("btnGuardarRuta", true).FirstOrDefault() as Button;
            if (btnGuardarRuta != null)
            {
                btnGuardarRuta.Click += BtnGuardarRuta_Click;
            }

            Button btnCargarRuta = this.Controls.Find("btnCargarRuta", true).FirstOrDefault() as Button;
            if (btnCargarRuta != null)
            {
                btnCargarRuta.Click += BtnCargarRuta_Click;
            }

            Button btnActualizarRuta = this.Controls.Find("btnActualizarRuta", true).FirstOrDefault() as Button;
            if (btnActualizarRuta != null)
            {
                btnActualizarRuta.Click += BtnActualizarRuta_Click;
            }
        }

        private void CargarDatosInstruccion(Instruccion instr)
        {
            ComboBox cbFuncion = this.Controls.Find("cbFuncion", true).FirstOrDefault() as ComboBox;
            if (cbFuncion != null)
            {
                if (Enum.TryParse<Directriz>(instr.Directriz, out Directriz dir))
                    cbFuncion.SelectedItem = dir;
            }

            NumericUpDown nudAltitud = this.Controls.Find("nudAltitud", true).FirstOrDefault() as NumericUpDown;
            if (nudAltitud != null)
            {
                nudAltitud.Value = (decimal)instr.Punto.Altitud;
            }

            NumericUpDown nudHeading = this.Controls.Find("nudHeading", true).FirstOrDefault() as NumericUpDown;
            if (nudHeading != null)
            {
                nudHeading.Value = (decimal)instr.Punto.Heading;
            }

            Label lblLatitud = this.Controls.Find("lblLatitud", true).FirstOrDefault() as Label;
            if (lblLatitud != null)
            {
                lblLatitud.Text = $"Lat: {instr.Punto.Lat}";
            }

            Label lblLongitud = this.Controls.Find("lblLongitud", true).FirstOrDefault() as Label;
            if (lblLongitud != null)
            {
                lblLongitud.Text = $"Lon: {instr.Punto.Long}";
            }
        }

        private void BtnAplicar_Click(object sender, EventArgs e)
        {
            if (instruccionSeleccionada == null)
            {
                MessageBox.Show("Selecciona una instrucción primero", "Error");
                return;
            }

            string idSeleccionado = instruccionSeleccionada.Id;

            ComboBox cbFuncion = this.Controls.Find("cbFuncion", true).FirstOrDefault() as ComboBox;
            NumericUpDown nudAltitud = this.Controls.Find("nudAltitud", true).FirstOrDefault() as NumericUpDown;
            NumericUpDown nudHeading = this.Controls.Find("nudHeading", true).FirstOrDefault() as NumericUpDown;

            if (cbFuncion != null && cbFuncion.SelectedItem != null)
            {
                instruccionSeleccionada.Directriz = cbFuncion.SelectedItem.ToString();
            }

            if (nudAltitud != null)
            {
                instruccionSeleccionada.Punto.Altitud = (float)nudAltitud.Value;
            }

            if (nudHeading != null)
            {
                instruccionSeleccionada.Punto.Heading = (float)nudHeading.Value;
            }

            ActualizarListaInstrucciones();

            ListBox listBox = this.Controls.Find("waypointListBox", true).FirstOrDefault() as ListBox;
            if (listBox != null)
            {
                int nuevoIndice = instrucciones.FindIndex(i => i.Id == idSeleccionado);
                if (nuevoIndice >= 0)
                {
                    listBox.SelectedIndex = nuevoIndice;
                }
            }

            MessageBox.Show("Cambios aplicados correctamente", "Éxito");
        }

        private void ActualizarRutaEdicion()
        {
            editRouteOverlay.Routes.Clear();

            // Dibujamos la ruta exactamente igual que en CargarWaypointsBD 
            // (Segmento por segmento con Pen grosor 1)
            for (int i = 0; i < instrucciones.Count - 1; i++)
            {
                var pos1 = new PointLatLng(instrucciones[i].Punto.Lat, instrucciones[i].Punto.Long);
                var pos2 = new PointLatLng(instrucciones[i + 1].Punto.Lat, instrucciones[i + 1].Punto.Long);

                var route = new GMapRoute(new List<PointLatLng> { pos1, pos2 }, "route");
                route.Stroke = new Pen(Color.Blue, 1);
                editRouteOverlay.Routes.Add(route);
            }

            editGmap.Refresh();
        }

        private void BtnSubir_Click(object sender, EventArgs e)
        {
            if (instruccionSeleccionada == null)
            {
                MessageBox.Show("Selecciona una instrucción primero", "Error");
                return;
            }

            int indice = instrucciones.IndexOf(instruccionSeleccionada);

            if (indice > 0)
            {
                var temp = instrucciones[indice];
                instrucciones[indice] = instrucciones[indice - 1];
                instrucciones[indice - 1] = temp;

                ActualizarListaInstrucciones();
                ActualizarRutaEdicion();

                ListBox listBox = this.Controls.Find("waypointListBox", true).FirstOrDefault() as ListBox;
                if (listBox != null)
                {
                    listBox.SelectedIndex = indice - 1;
                }
            }
            else
            {
                MessageBox.Show("Esta instrucción ya está al inicio", "Aviso");
            }
        }

        private void BtnBajar_Click(object sender, EventArgs e)
        {
            if (instruccionSeleccionada == null)
            {
                MessageBox.Show("Selecciona una instrucción primero", "Error");
                return;
            }

            int indice = instrucciones.IndexOf(instruccionSeleccionada);

            if (indice < instrucciones.Count - 1)
            {
                var temp = instrucciones[indice];
                instrucciones[indice] = instrucciones[indice + 1];
                instrucciones[indice + 1] = temp;

                ActualizarListaInstrucciones();
                ActualizarRutaEdicion();

                ListBox listBox = this.Controls.Find("waypointListBox", true).FirstOrDefault() as ListBox;
                if (listBox != null)
                {
                    listBox.SelectedIndex = indice + 1;
                }
            }
            else
            {
                MessageBox.Show("Esta instrucción ya está al final", "Aviso");
            }
        }

        private void BtnEliminar_Click(object sender, EventArgs e)
        {
            if (instruccionSeleccionada == null)
            {
                MessageBox.Show("Selecciona una instrucción para eliminar", "Error");
                return;
            }

            DialogResult resultado = MessageBox.Show(
                $"¿Estás seguro de que quieres eliminar I{instruccionSeleccionada.VisualId}?",
                "Confirmar eliminación",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );

            if (resultado == DialogResult.Yes)
            {
                instrucciones.Remove(instruccionSeleccionada);
                instruccionSeleccionada = null;

                ActualizarListaInstrucciones();
                ActualizarRutaEdicion();
                RefrescarMarcadoresEdicion();

                MessageBox.Show("Instrucción eliminada correctamente", "Éxito");
            }
        }

        private void RefrescarMarcadoresEdicion()
        {
            editWaypointsOverlay.Markers.Clear();

            foreach (var instr in instrucciones)
            {
                AgregarMarcadorEdicion(instr);
            }

            editGmap.Refresh();
        }

        private void BtnLimpiarRuta_Click(object sender, EventArgs e)
        {
            if (instrucciones.Count == 0)
            {
                MessageBox.Show("No hay instrucciones para limpiar", "Aviso");
                return;
            }

            DialogResult resultado = MessageBox.Show(
                $"¿Estás seguro de que quieres eliminar todas las instrucciones ({instrucciones.Count})?\n\nEsta acción no se puede deshacer.",
                "Limpiar ruta completa",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );

            if (resultado == DialogResult.Yes)
            {
                instrucciones.Clear();
                instruccionSeleccionada = null;
                instruccionCounter = 0;
                vueloActualCargado = null;

                Instruccion.ReiniciarContador();

                ActualizarListaInstrucciones();
                ActualizarRutaEdicion();
                RefrescarMarcadoresEdicion();
                LimpiarControlesEdicion();

                //panel_versiones.Visible = false;
                //panel_versiones.Controls.Clear();

                MessageBox.Show("Ruta limpiada completamente", "Éxito");
            }
        }

        private async void BtnGuardarRuta_Click(object sender, EventArgs e)
        {
            if (instrucciones.Count == 0)
            {
                MessageBox.Show("No hay instrucciones para guardar", "Aviso");
                return;
            }

            if (droneAPIService == null)
            {
                MessageBox.Show("No hay conexión a la API", "Error");
                return;
            }

            NombVueloForm formNombre = new NombVueloForm();
            DialogResult resultadoNombre = formNombre.ShowDialog();

            if (resultadoNombre != DialogResult.OK)
            {
                return;
            }

            string nametag = formNombre.ObtenerNombre();

            if (string.IsNullOrWhiteSpace(nametag))
            {
                MessageBox.Show("El nombre del vuelo no puede estar vacío", "Error");
                return;
            }

            Vuelo vueloTemporal = new Vuelo();
            vueloTemporal.NameTag = nametag;
            vueloTemporal.Instrucciones = new List<Instruccion>(instrucciones);

            string previewGuardado = GenerarPreviewGuardado(vueloTemporal);

            PreviewForm previewForm = new PreviewForm(previewGuardado);
            DialogResult resultado = previewForm.ShowDialog();

            if (resultado == DialogResult.Yes)
            {
                try
                {
                    Vuelo vuelo = new Vuelo();
                    vuelo.NameTag = nametag;
                    vuelo.Instrucciones = new List<Instruccion>(instrucciones);

                    await droneAPIService.GuardarRutaAsync(vuelo);
                    vueloActualCargado = vuelo.ID;

                    MessageBox.Show(
                        $"Ruta guardada en la API\nNombre: {vuelo.NameTag}\nID: {vuelo.ID}",
                        "Éxito"
                    );
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error: {ex.Message}", "Error al guardar");
                }
            }
        }

        private string GenerarPreviewGuardado(Vuelo vuelo)
        {
            string preview = "";
            preview += "═══════════════════════════════════════════════════════════\n";
            preview += "        PREVISUALIZACIÓN DE RUTA A GUARDAR\n";
            preview += "═══════════════════════════════════════════════════════════\n\n";

            preview += $"ID de Vuelo: {vuelo.ID}\n";
            preview += $"Fecha: {vuelo.Fecha:yyyy-MM-dd HH:mm:ss}\n";
            preview += $"Total de Instrucciones: {vuelo.Instrucciones.Count}\n";
            preview += "\n";
            preview += "═══════════════════════════════════════════════════════════\n\n";

            for (int i = 0; i < vuelo.Instrucciones.Count; i++)
            {
                var instr = vuelo.Instrucciones[i];

                preview += $"INSTRUCCIÓN I{instr.VisualId}\n";
                preview += "───────────────────────────────────────────────────────\n";
                preview += $"  Ubicación:\n";
                preview += $"    Latitud:   {instr.Punto.Lat,15:F6}\n";
                preview += $"    Longitud:  {instr.Punto.Long,15:F6}\n";
                preview += $"\n";
                preview += $"  Parámetros:\n";
                preview += $"    Altitud:   {instr.Punto.Altitud,15:F2} m\n";
                preview += $"    Heading:   {instr.Punto.Heading,15:F2}°\n";
                preview += $"\n";
                preview += $"  Función: {instr.Directriz}\n";
                preview += "\n";
            }

            preview += "═══════════════════════════════════════════════════════════\n";

            return preview;
        }

        private void LimpiarControlesEdicion()
        {
            ComboBox cbFuncion = this.Controls.Find("cbFuncion", true).FirstOrDefault() as ComboBox;
            if (cbFuncion != null)
            {
                cbFuncion.SelectedIndex = 0;
            }

            NumericUpDown nudAltitud = this.Controls.Find("nudAltitud", true).FirstOrDefault() as NumericUpDown;
            if (nudAltitud != null)
            {
                nudAltitud.Value = 5;
            }

            NumericUpDown nudHeading = this.Controls.Find("nudHeading", true).FirstOrDefault() as NumericUpDown;
            if (nudHeading != null)
            {
                nudHeading.Value = 0;
            }

            Label lblLatitud = this.Controls.Find("lblLatitud", true).FirstOrDefault() as Label;
            if (lblLatitud != null)
            {
                lblLatitud.Text = "Lat: --";
            }

            Label lblLongitud = this.Controls.Find("lblLongitud", true).FirstOrDefault() as Label;
            if (lblLongitud != null)
            {
                lblLongitud.Text = "Lon: --";
            }
        }

        private async void BtnCargarRuta_Click(object sender, EventArgs e)
        {
            if (droneAPIService == null)
            {
                MessageBox.Show("No hay conexión a la API", "Error");
                return;
            }

            try
            {
                var vuelos = await droneAPIService.ObtenerTodosVuelosAsync();

                if (vuelos.Count == 0)
                {
                    MessageBox.Show("No hay rutas guardadas", "Aviso");
                    return;
                }

                var vueloSeleccionado = SeleccionarVuelo(vuelos);
                if (vueloSeleccionado == null) return;

                // MostrarSelectorVersiones(vueloSeleccionado); // <-- COMENTADO

                Instruccion.ReiniciarContador();

                // Cargamos la ruta directamente usando CargarRutaAsync
                var vueloCompleto = await droneAPIService.CargarRutaAsync(vueloSeleccionado.ID);

                // vueloCompleto.Instrucciones = await droneAPIService.ObtenerInstruccionesVueloVersionAsync(
                //     vueloSeleccionado.ID,
                //     vueloSeleccionado.NumVersiones
                // ); // <-- COMENTADO

                instrucciones.Clear();
                instruccionSeleccionada = null;
                instruccionCounter = 0;

                if (vueloCompleto.Instrucciones != null)
                {
                    instrucciones = new List<Instruccion>(vueloCompleto.Instrucciones);
                }

                vueloActualCargado = vueloSeleccionado.ID;

                ActualizarListaInstrucciones();
                ActualizarRutaEdicion();
                RefrescarMarcadoresEdicion();

                MessageBox.Show(
                    $"Ruta cargada: {vueloCompleto.NameTag}\n{instrucciones.Count} instrucciones",
                    "Éxito"
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error al cargar");
            }
        }

        private Vuelo SeleccionarVuelo(List<Vuelo> vuelos)
        {
            using (Form form = new Form())
            {
                form.Text = "Seleccionar Ruta";
                form.Width = 500;
                form.Height = 350;
                form.StartPosition = FormStartPosition.CenterParent;

                ListBox listBox = new ListBox
                {
                    Dock = DockStyle.Fill,
                    DataSource = vuelos,
                    DisplayMember = "NameTag"
                };

                Button btnOK = new Button
                {
                    Text = "Aceptar",
                    Dock = DockStyle.Bottom,
                    Height = 30,
                    BackColor = System.Drawing.Color.Green,
                    ForeColor = System.Drawing.Color.White
                };

                Button btnCancel = new Button
                {
                    Text = "Cancelar",
                    Dock = DockStyle.Bottom,
                    Height = 30,
                    BackColor = System.Drawing.Color.Red,
                    ForeColor = System.Drawing.Color.White
                };

                btnOK.Click += (s, e) =>
                {
                    var vueloSeleccionado = (Vuelo)listBox.SelectedItem;
                    MessageBox.Show($"Vuelo seleccionado:\nNombre: {vueloSeleccionado.NameTag}\nID: {vueloSeleccionado.ID}", "Debug");
                    form.DialogResult = DialogResult.OK;
                };

                btnCancel.Click += (s, e) => form.DialogResult = DialogResult.Cancel;

                form.Controls.Add(listBox);
                form.Controls.Add(btnOK);
                form.Controls.Add(btnCancel);

                if (form.ShowDialog() == DialogResult.OK)
                {
                    return (Vuelo)listBox.SelectedItem;
                }
                return null;
            }
        }

        private async void BtnActualizarRuta_Click(object sender, EventArgs e)
        {
            if (instrucciones.Count == 0)
            {
                MessageBox.Show("No hay instrucciones para guardar", "Aviso");
                return;
            }

            if (vueloActualCargado == null)
            {
                MessageBox.Show("No hay una ruta cargada. Use 'Guardar Ruta' primero.", "Aviso");
                return;
            }

            if (droneAPIService == null)
            {
                MessageBox.Show("No hay conexión a la API", "Error");
                return;
            }

            DialogResult resultado = MessageBox.Show(
                $"¿Deseas actualizar la ruta?\nID: {vueloActualCargado}",
                "Actualizar Ruta",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (resultado == DialogResult.Yes)
            {
                try
                {
                    Vuelo vuelo = new Vuelo();
                    vuelo.ID = vueloActualCargado;
                    vuelo.Instrucciones = new List<Instruccion>(instrucciones);

                    await droneAPIService.ActualizarRutaAsync(vueloActualCargado, vuelo);

                    MessageBox.Show("Ruta actualizada correctamente", "Éxito");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error: {ex.Message}", "Error al actualizar");
                }
            }
        }

        // =========================================================
        // CLASES AUXILIARES
        // =========================================================

        public class VueloDto
        {
            public string Id { get; set; }
            public string Nametag { get; set; }
            public string Datetime { get; set; }

            public override string ToString() =>
                $"{Nametag}  ({(Datetime?.Length > 10 ? Datetime.Substring(0, 10) : Datetime)})";
        }

        private void mapPanel_Paint(object sender, PaintEventArgs e)
        {

        }

        private void headLbl_Click(object sender, EventArgs e)
        {

        }
        private void label33_Click(object sender, EventArgs e)
        {

        }
    }
}