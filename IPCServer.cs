using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;

namespace ACCRPMMonitor;

/// <summary>
/// IPC server that supports both named pipes (local) and UDP (network).
/// Intelligently chooses transport based on connection source.
/// </summary>
public class IPCServer : IDisposable
{
    private const string NamedPipeName = "ACCRPMMonitor_Telemetry";
    private const int UDPPort = 7777;

    private NamedPipeServerStream? _namedPipeServer;
    private UdpClient? _udpServer;
    private TelemetryData _latestTelemetry = new();
    private bool _isRunning = false;
    private Task? _namedPipeTask;
    private Task? _udpTask;
    private CancellationTokenSource _cancellationTokenSource = new();

    public event EventHandler<TelemetryData>? TelemetryUpdated;

    /// <summary>
    /// Starts the IPC server (named pipes + UDP listener).
    /// </summary>
    public void Start()
    {
        if (_isRunning) return;

        _isRunning = true;
        _cancellationTokenSource = new CancellationTokenSource();

        // Start named pipe server for local connections
        _namedPipeTask = Task.Run(() => RunNamedPipeServer(_cancellationTokenSource.Token));

        // Start UDP server for network connections
        _udpTask = Task.Run(() => RunUDPServer(_cancellationTokenSource.Token));

        Console.WriteLine("[IPC] Server started - Named pipes (local) and UDP (network) listening");
    }

    /// <summary>
    /// Stops the IPC server.
    /// </summary>
    public void Stop()
    {
        if (!_isRunning) return;

        _isRunning = false;
        _cancellationTokenSource.Cancel();

        try
        {
            _namedPipeTask?.Wait(5000);
            _udpTask?.Wait(5000);
        }
        catch { }

        Console.WriteLine("[IPC] Server stopped");
    }

    /// <summary>
    /// Publishes telemetry to all connected clients.
    /// </summary>
    public void PublishTelemetry(TelemetryData telemetry)
    {
        _latestTelemetry = telemetry;
        TelemetryUpdated?.Invoke(this, telemetry);
    }

    /// <summary>
    /// Gets the latest telemetry data.
    /// </summary>
    public TelemetryData GetLatestTelemetry() => _latestTelemetry;

    private async Task RunNamedPipeServer(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && _isRunning)
            {
                try
                {
                    _namedPipeServer = new NamedPipeServerStream(NamedPipeName, PipeDirection.Out);
                    await _namedPipeServer.WaitForConnectionAsync(cancellationToken);

                    using var writer = new StreamWriter(_namedPipeServer) { AutoFlush = true };

                    // Send telemetry continuously while connected
                    while (_namedPipeServer.IsConnected && !cancellationToken.IsCancellationRequested)
                    {
                        var json = _latestTelemetry.ToJson();
                        await writer.WriteLineAsync(json);
                        await Task.Delay(50, cancellationToken); // 50ms update rate
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[IPC] Named pipe error: {ex.Message}");
                }
                finally
                {
                    _namedPipeServer?.Dispose();
                    _namedPipeServer = null;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[IPC] Named pipe server error: {ex.Message}");
        }
    }

    private async Task RunUDPServer(CancellationToken cancellationToken)
    {
        try
        {
            _udpServer = new UdpClient(UDPPort);
            Console.WriteLine($"[IPC] UDP server listening on port {UDPPort}");

            while (!cancellationToken.IsCancellationRequested && _isRunning)
            {
                try
                {
                    var task = _udpServer.ReceiveAsync();
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    cts.CancelAfter(1000);

                    try
                    {
                        var result = await task;
                        // Client pinged us, send back telemetry
                        var json = _latestTelemetry.ToJson();
                        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                        await _udpServer.SendAsync(bytes, bytes.Length, result.RemoteEndPoint);
                    }
                    catch (OperationCanceledException)
                    {
                        // Timeout, just continue listening
                    }
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    if (!cancellationToken.IsCancellationRequested)
                        Console.WriteLine($"[IPC] UDP error: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[IPC] UDP server error: {ex.Message}");
        }
        finally
        {
            _udpServer?.Dispose();
            _udpServer = null;
        }
    }

    public void Dispose()
    {
        Stop();
        _namedPipeServer?.Dispose();
        _udpServer?.Dispose();
        _cancellationTokenSource?.Dispose();
    }
}

/// <summary>
/// IPC client that intelligently selects transport (named pipes for local, UDP for network).
/// </summary>
public class IPCClient : IDisposable
{
    private const string NamedPipeName = "ACCRPMMonitor_Telemetry";
    private const int UDPPort = 7777;

    private readonly string _serverAddress;
    private readonly bool _isLocalhost;
    private Task? _updateTask;
    private CancellationTokenSource _cancellationTokenSource = new();
    private bool _isConnected = false;

    public event EventHandler<TelemetryData>? TelemetryReceived;

    public IPCClient(string serverAddress = "localhost")
    {
        _serverAddress = serverAddress;
        _isLocalhost = serverAddress == "localhost" || serverAddress == "127.0.0.1" || serverAddress == ".";
    }

    /// <summary>
    /// Starts receiving telemetry updates from the server.
    /// </summary>
    public void Connect()
    {
        if (_isConnected) return;

        _cancellationTokenSource = new CancellationTokenSource();

        if (_isLocalhost)
        {
            _updateTask = Task.Run(() => ConnectNamedPipe(_cancellationTokenSource.Token));
            Console.WriteLine("[IPC] Connecting via named pipes (local)...");
        }
        else
        {
            _updateTask = Task.Run(() => ConnectUDP(_cancellationTokenSource.Token));
            Console.WriteLine($"[IPC] Connecting via UDP to {_serverAddress}:{UDPPort}...");
        }
    }

    /// <summary>
    /// Disconnects from the server.
    /// </summary>
    public void Disconnect()
    {
        _isConnected = false;
        _cancellationTokenSource.Cancel();
        try { _updateTask?.Wait(5000); } catch { }
        Console.WriteLine("[IPC] Disconnected");
    }

    public bool IsConnected => _isConnected;

    private async Task ConnectNamedPipe(CancellationToken cancellationToken)
    {
        try
        {
            using var pipeClient = new NamedPipeClientStream(".", NamedPipeName, PipeDirection.In);

            try
            {
                // Use a timeout without cancellation token to avoid OperationCanceledException on connection
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await pipeClient.ConnectAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("[IPC] Named pipe connection timeout - is headless server running?");
                return;
            }

            _isConnected = true;
            Console.WriteLine("[IPC] Connected via named pipes");

            using var reader = new StreamReader(pipeClient);

            while (_isConnected && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var line = await reader.ReadLineAsync(cancellationToken);
                    if (string.IsNullOrEmpty(line)) break;

                    var data = TelemetryData.FromJson(line);
                    if (data != null)
                    {
                        TelemetryReceived?.Invoke(this, data);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[IPC] Named pipe error: {ex.Message}");
        }
        finally
        {
            _isConnected = false;
        }
    }

    private async Task ConnectUDP(CancellationToken cancellationToken)
    {
        try
        {
            using var udpClient = new UdpClient();
            udpClient.Client.ReceiveTimeout = 5000;

            _isConnected = true;
            Console.WriteLine("[IPC] Connected via UDP");

            while (_isConnected && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Send ping to get telemetry
                    var pingBytes = System.Text.Encoding.UTF8.GetBytes("ping");
                    await udpClient.SendAsync(pingBytes, pingBytes.Length, _serverAddress, UDPPort);

                    // Receive telemetry response
                    var result = await udpClient.ReceiveAsync(cancellationToken);
                    var json = System.Text.Encoding.UTF8.GetString(result.Buffer);

                    var data = TelemetryData.FromJson(json);
                    if (data != null)
                    {
                        TelemetryReceived?.Invoke(this, data);
                    }

                    await Task.Delay(50, cancellationToken); // 50ms update rate
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    if (_isConnected)
                        Console.WriteLine($"[IPC] UDP error: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[IPC] UDP client error: {ex.Message}");
        }
        finally
        {
            _isConnected = false;
        }
    }

    public void Dispose()
    {
        Disconnect();
        _cancellationTokenSource?.Dispose();
    }
}
