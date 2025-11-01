using System.Net;
using System.Text;
using System.Text.Json;

Console.WriteLine("=== ACC Telemetry Test Server ===");
Console.WriteLine("Standalone telemetry server with mock data");
Console.WriteLine("Runs independently - no ACC or main app required\n");

int port = 8080;

// Check for custom port
if (args.Length > 0 && int.TryParse(args[0], out int customPort))
{
    port = customPort;
}

// Create HTTP server
var server = new MockTelemetryServer(port);

Console.WriteLine($"Starting server on http://localhost:{port}/telemetry");
Console.WriteLine("Press Ctrl+C to stop\n");

if (server.Start())
{
    Console.WriteLine("✓ Server started successfully");
    Console.WriteLine($"✓ Test with: curl http://localhost:{port}/telemetry");
    Console.WriteLine($"✓ Or open Streamlit dashboard (ensure it points to port {port})\n");

    // Keep running until Ctrl+C
    Console.CancelKeyPress += (sender, e) =>
    {
        e.Cancel = true;
        server.Stop();
        Console.WriteLine("\n\nServer stopped. Goodbye!");
        Environment.Exit(0);
    };

    // Keep the main thread alive
    Thread.Sleep(Timeout.Infinite);
}
else
{
    Console.WriteLine("❌ Failed to start server");
    Console.WriteLine("  - Check if port is already in use");
    Console.WriteLine($"  - Try a different port: dotnet run {port + 1}");
}

/// <summary>
/// Standalone mock telemetry server that doesn't depend on ACCRPMMonitor
/// </summary>
class MockTelemetryServer
{
    private HttpListener? _listener;
    private bool _isRunning;
    private Thread? _serverThread;
    private Thread? _dataThread;
    private TelemetrySnapshot? _latestSnapshot;
    private readonly object _snapshotLock = new object();
    private readonly int _port;

    public MockTelemetryServer(int port = 8080)
    {
        _port = port;
    }

    public bool Start()
    {
        try
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{_port}/");
            _listener.Start();
            _isRunning = true;

            // Start HTTP request handler thread
            _serverThread = new Thread(ListenForRequests);
            _serverThread.IsBackground = true;
            _serverThread.Start();

            // Start mock data generator thread
            _dataThread = new Thread(GenerateMockData);
            _dataThread.IsBackground = true;
            _dataThread.Start();

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error starting server: {ex.Message}");
            return false;
        }
    }

    public void Stop()
    {
        _isRunning = false;
        _listener?.Stop();
        _listener?.Close();
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
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Request error: {ex.Message}");
            }
        }
    }

    private void HandleRequest(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        // CORS headers
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
                    response.StatusCode = 503;
                    byte[] buffer = Encoding.UTF8.GetBytes("{\"error\":\"No data available\"}");
                    response.ContentLength64 = buffer.Length;
                    response.OutputStream.Write(buffer, 0, buffer.Length);
                }
            }
            else if (request.Url?.AbsolutePath == "/")
            {
                var info = $@"{{
                    ""service"": ""ACC Telemetry Test Server"",
                    ""version"": ""1.0"",
                    ""mode"": ""mock_data"",
                    ""endpoints"": [""GET /telemetry""],
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
                byte[] buffer = Encoding.UTF8.GetBytes("{\"error\":\"Not found\"}");
                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Handler error: {ex.Message}");
            response.StatusCode = 500;
        }
        finally
        {
            response.Close();
        }
    }

    private void GenerateMockData()
    {
        int frameCount = 0;
        int rpm = 2000;
        int gear = 2;
        float speed = 50.0f;
        float fuel = 80.0f;
        bool accelerating = true;

        Console.WriteLine("Mock data generator started\n");

        while (_isRunning)
        {
            frameCount++;

            // Simulate driving dynamics
            if (accelerating)
            {
                rpm += 50;
                speed += 0.5f;

                if (rpm >= 7000 && gear < 6)
                {
                    gear++;
                    rpm = 5000;
                    Console.WriteLine($"[SHIFT UP] → Gear {gear}");
                }
                else if (rpm >= 7500)
                {
                    accelerating = false;
                }
            }
            else
            {
                rpm -= 50;
                speed -= 0.3f;

                if (rpm <= 3000 && gear > 1)
                {
                    gear--;
                    rpm = 5000;
                    Console.WriteLine($"[SHIFT DOWN] → Gear {gear}");
                }
                else if (rpm <= 2000)
                {
                    accelerating = true;
                }
            }

            // Generate tire data
            float basePressure = 27.0f + (speed / 300.0f) * 3.0f;
            float baseTemp = 60.0f + (speed / 200.0f) * 40.0f;

            Random rnd = new Random(frameCount);
            float pressureNoise = (float)(rnd.NextDouble() - 0.5) * 0.5f;
            float tempNoise = (float)(rnd.NextDouble() - 0.5) * 2.0f;

            var snapshot = new TelemetrySnapshot
            {
                Timestamp = DateTime.UtcNow,
                RPM = rpm,
                Gear = gear,
                SpeedKmh = speed,
                Fuel = fuel,

                TirePressureFL = basePressure - 0.3f + pressureNoise,
                TirePressureFR = basePressure - 0.2f + pressureNoise,
                TirePressureRL = basePressure + 0.2f + pressureNoise,
                TirePressureRR = basePressure + 0.3f + pressureNoise,

                TireTempFL = baseTemp + 5.0f + tempNoise,
                TireTempFR = baseTemp + 4.0f + tempNoise,
                TireTempRL = baseTemp - 2.0f + tempNoise,
                TireTempRR = baseTemp - 3.0f + tempNoise
            };

            snapshot.TirePressureAvg = (snapshot.TirePressureFL + snapshot.TirePressureFR +
                                       snapshot.TirePressureRL + snapshot.TirePressureRR) / 4f;
            snapshot.TireTempAvg = (snapshot.TireTempFL + snapshot.TireTempFR +
                                   snapshot.TireTempRL + snapshot.TireTempRR) / 4f;

            lock (_snapshotLock)
            {
                _latestSnapshot = snapshot;
            }

            fuel -= 0.002f;

            // Console feedback every 50 frames (~5 seconds)
            if (frameCount % 50 == 0)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] RPM={rpm} Gear={gear} Speed={speed:F1}km/h " +
                                $"Pressure={snapshot.TirePressureFL:F1}psi Temp={snapshot.TireTempFL:F1}°C");
            }

            Thread.Sleep(100); // 10Hz update rate
        }
    }
}

class TelemetrySnapshot
{
    public DateTime Timestamp { get; set; }
    public int RPM { get; set; }
    public int Gear { get; set; }
    public float SpeedKmh { get; set; }
    public float Fuel { get; set; }

    public float TirePressureFL { get; set; }
    public float TirePressureFR { get; set; }
    public float TirePressureRL { get; set; }
    public float TirePressureRR { get; set; }
    public float TirePressureAvg { get; set; }

    public float TireTempFL { get; set; }
    public float TireTempFR { get; set; }
    public float TireTempRL { get; set; }
    public float TireTempRR { get; set; }
    public float TireTempAvg { get; set; }
}
