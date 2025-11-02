using System.Net;
using System.Text;
using System.Text.Json;
using System.Diagnostics;
using System.Windows.Threading;

namespace ACCRPMMonitor;

/// <summary>
/// Telemetry Server - Manages real-time ACC telemetry data display
/// Displays data in WPF window and optionally serves over HTTP
/// </summary>
public class TelemetryServer : IDisposable
{
    private readonly string _instanceId = Guid.NewGuid().ToString().Substring(0, 8);
    private HttpListener? _listener;
    private bool _isRunning;
    private Thread? _serverThread;
    private SharedMemoryReader? _accMemory;
    private TelemetrySnapshot? _latestSnapshot;
    private readonly object _snapshotLock = new object();
    private TelemetryWindow? _telemetryWindow;
    private Thread? _wpfThread;
    private readonly ManualResetEvent _windowReadyEvent = new ManualResetEvent(false);

    public int Port { get; set; } = 8080;
    public bool IsRunning => _isRunning;
    public bool EnableHttpServer { get; set; } = false;

    /// <summary>
    /// Starts the telemetry display (WPF window and optional HTTP server)
    /// </summary>
    public bool Start(SharedMemoryReader accMemory)
    {
        if (_isRunning)
        {
            Console.WriteLine("[TELEMETRY] Server already running");
            return false;
        }

        _accMemory = accMemory;
        _isRunning = true;

        try
        {
            // Launch WPF window in separate thread
            _wpfThread = new Thread(() =>
            {
                try
                {
                    _telemetryWindow = new TelemetryWindow();
                    _telemetryWindow.Closed += (s, e) =>
                    {
                        Dispatcher.CurrentDispatcher.InvokeShutdown();
                    };
                    _telemetryWindow.Show();

                    // Signal that window is ready
                    _windowReadyEvent.Set();

                    Dispatcher.Run();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[TELEMETRY] WPF thread error: {ex.Message}");
                    _windowReadyEvent.Set(); // Still signal so we don't hang
                }
            });
            _wpfThread.SetApartmentState(ApartmentState.STA);
            _wpfThread.IsBackground = false; // Keep thread alive
            _wpfThread.Start();

            // Wait for window to be created (max 5 seconds)
            if (_windowReadyEvent.WaitOne(5000))
            {
                Console.WriteLine("[TELEMETRY] WPF window launched");
            }
            else
            {
                Console.WriteLine("[TELEMETRY] Warning: WPF window launch timeout");
            }

            // Optionally start HTTP server
            if (EnableHttpServer)
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://localhost:{Port}/");
                _listener.Start();

                _serverThread = new Thread(ListenForRequests);
                _serverThread.IsBackground = true;
                _serverThread.Start();

                Console.WriteLine($"[TELEMETRY] HTTP server started on http://localhost:{Port}/telemetry");
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TELEMETRY] Failed to start: {ex.Message}");
            _isRunning = false;
            return false;
        }
    }

    /// <summary>
    /// Updates the latest telemetry snapshot (call this from your monitoring loop)
    /// </summary>
    public void UpdateTelemetry(WheelAndTireData? tireData, int rpm, int gear, float speed, float fuel)
    {
        if (tireData == null)
        {
            ShowNoData();
            return;
        }

        var snapshot = new TelemetrySnapshot
        {
            Timestamp = DateTime.UtcNow,
            RPM = rpm,
            Gear = gear,
            SpeedKmh = speed,
            Fuel = fuel,

            // Tire Pressures (PSI)
            TirePressureFL = tireData.WheelPressureFL,
            TirePressureFR = tireData.WheelPressureFR,
            TirePressureRL = tireData.WheelPressureRL,
            TirePressureRR = tireData.WheelPressureRR,
            TirePressureAvg = tireData.AverageWheelPressure,

            // Tire Temperatures (Celsius)
            TireTempFL = tireData.TyreCoreTempFL,
            TireTempFR = tireData.TyreCoreTempFR,
            TireTempRL = tireData.TyreCoreTempRL,
            TireTempRR = tireData.TyreCoreTempRR,
            TireTempAvg = tireData.AverageTyreCoreTemp
        };

        lock (_snapshotLock)
        {
            _latestSnapshot = snapshot;
        }

        // Update WPF window
        _telemetryWindow?.UpdateTelemetry(snapshot);
    }

    /// <summary>
    /// Shows "No data" state in the telemetry window
    /// </summary>
    public void ShowNoData()
    {
        _telemetryWindow?.ShowNoData();
    }

    private void ListenForRequests()
    {
        while (_isRunning && _listener != null)
        {
            try
            {
                var context = _listener.GetContext();
                ThreadPool.QueueUserWorkItem(_ => HandleRequest(context));
            }
            catch (HttpListenerException)
            {
                // Listener stopped
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Server error: {ex.Message}");
            }
        }
    }

    private void HandleRequest(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        // Enable CORS for browser access
        response.AddHeader("Access-Control-Allow-Origin", "*");
        response.AddHeader("Access-Control-Allow-Methods", "GET, OPTIONS");
        response.AddHeader("Access-Control-Allow-Headers", "Content-Type");

        if (request.HttpMethod == "OPTIONS")
        {
            response.StatusCode = 200;
            response.Close();
            return;
        }

        try
        {
            if (request.Url?.AbsolutePath == "/telemetry")
            {
                TelemetrySnapshot? snapshot;
                lock (_snapshotLock)
                {
                    snapshot = _latestSnapshot;
                }

                if (snapshot != null)
                {
                    var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        WriteIndented = false
                    });

                    response.ContentType = "application/json";
                    response.StatusCode = 200;

                    byte[] buffer = Encoding.UTF8.GetBytes(json);
                    response.ContentLength64 = buffer.Length;
                    response.OutputStream.Write(buffer, 0, buffer.Length);
                }
                else
                {
                    response.StatusCode = 503; // Service Unavailable
                    byte[] buffer = Encoding.UTF8.GetBytes("{\"error\":\"No telemetry data available\"}");
                    response.ContentLength64 = buffer.Length;
                    response.OutputStream.Write(buffer, 0, buffer.Length);
                }
            }
            else if (request.Url?.AbsolutePath == "/")
            {
                // Root endpoint - show info
                var info = $@"{{
                    ""service"": ""ACC Telemetry Server"",
                    ""version"": ""1.0"",
                    ""endpoints"": [
                        ""GET /telemetry - Get current tire telemetry data""
                    ],
                    ""status"": ""running""
                }}";

                response.ContentType = "application/json";
                response.StatusCode = 200;
                byte[] buffer = Encoding.UTF8.GetBytes(info);
                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
            }
            else
            {
                response.StatusCode = 404;
                byte[] buffer = Encoding.UTF8.GetBytes("{\"error\":\"Endpoint not found\"}");
                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Request handling error: {ex.Message}");
            response.StatusCode = 500;
        }
        finally
        {
            response.Close();
        }
    }

    public void Stop()
    {
        _isRunning = false;

        // Stop HTTP listener if running
        if (_listener != null)
        {
            _listener.Stop();
            _listener.Close();
        }

        // Close WPF window if it exists and dispatcher is still running
        try
        {
            if (_telemetryWindow != null && !_telemetryWindow.Dispatcher.HasShutdownStarted)
            {
                _telemetryWindow.Dispatcher.Invoke(() =>
                {
                    _telemetryWindow?.Close();
                });
            }
        }
        catch (TaskCanceledException)
        {
            // Window already closed, ignore
        }
        catch (Exception)
        {
            // Ignore any other dispatcher-related errors during shutdown
        }

        Console.WriteLine("Telemetry display stopped");
    }

    public void Dispose()
    {
        Stop();
        _listener?.Close();
    }
}

/// <summary>
/// Telemetry data snapshot for JSON serialization
/// </summary>
public class TelemetrySnapshot
{
    public DateTime Timestamp { get; set; }
    public int RPM { get; set; }
    public int Gear { get; set; }
    public float SpeedKmh { get; set; }
    public float Fuel { get; set; }

    // Tire Pressures (PSI)
    public float TirePressureFL { get; set; }
    public float TirePressureFR { get; set; }
    public float TirePressureRL { get; set; }
    public float TirePressureRR { get; set; }
    public float TirePressureAvg { get; set; }

    // Tire Temperatures (Celsius)
    public float TireTempFL { get; set; }
    public float TireTempFR { get; set; }
    public float TireTempRL { get; set; }
    public float TireTempRR { get; set; }
    public float TireTempAvg { get; set; }
}
