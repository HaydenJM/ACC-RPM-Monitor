using System.Windows.Threading;

namespace ACCRPMMonitor;

/// <summary>
/// Telemetry Server - Manages the WPF telemetry overlay window
/// </summary>
public class TelemetryServer : IDisposable
{
    private bool _isRunning;
    private SharedMemoryReader? _accMemory;
    private TelemetryWindow? _telemetryWindow;
    private Thread? _wpfThread;
    private readonly ManualResetEvent _windowReadyEvent = new ManualResetEvent(false);
    private readonly object _lock = new object();

    // Lap tracking
    private WheelAndTireData? _lapStartData;
    private WheelAndTireData? _lapEndData;
    private int _lastCompletedLaps = -1;

    public bool IsRunning => _isRunning;

    /// <summary>
    /// Shows the telemetry window
    /// </summary>
    public bool Start(SharedMemoryReader accMemory)
    {
        lock (_lock)
        {
            _accMemory = accMemory;

            // If window doesn't exist yet, create it
            if (_telemetryWindow == null)
            {
                _windowReadyEvent.Reset();

                // Launch WPF window in separate thread
                _wpfThread = new Thread(() =>
                {
                    try
                    {
                        _telemetryWindow = new TelemetryWindow();
                        _telemetryWindow.Closed += (s, e) =>
                        {
                            _isRunning = false;
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
                    return false;
                }
            }
            else
            {
                // Window exists, just show it
                try
                {
                    _telemetryWindow.Dispatcher.Invoke(() =>
                    {
                        _telemetryWindow.Show();
                        _telemetryWindow.Activate();
                    });
                    Console.WriteLine("[TELEMETRY] WPF window shown");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[TELEMETRY] Error showing window: {ex.Message}");
                    return false;
                }
            }

            _isRunning = true;
            return true;
        }
    }

    /// <summary>
    /// Updates the latest telemetry snapshot and tracks lap data
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

        // Update WPF window
        _telemetryWindow?.UpdateTelemetry(snapshot);
    }

    /// <summary>
    /// Updates lap comparison data when a lap is completed
    /// </summary>
    public void UpdateLapData(int completedLaps, WheelAndTireData? currentTireData)
    {
        if (currentTireData == null)
            return;

        // Check if lap was just completed
        if (_lastCompletedLaps >= 0 && completedLaps > _lastCompletedLaps)
        {
            // Lap completed - save end data
            _lapEndData = _lapStartData; // Previous start becomes the end of completed lap

            // Set new lap start data
            _lapStartData = currentTireData;

            // Update window with lap comparison
            if (_lapStartData != null && _lapEndData != null)
            {
                var lapStart = new LapTireData
                {
                    AvgPressure = _lapEndData.AverageWheelPressure,
                    AvgTemp = _lapEndData.AverageTyreCoreTemp
                };

                var lapEnd = new LapTireData
                {
                    AvgPressure = _lapStartData.AverageWheelPressure,
                    AvgTemp = _lapStartData.AverageTyreCoreTemp
                };

                _telemetryWindow?.UpdateLapComparison(lapStart, lapEnd);
            }
        }
        else if (_lastCompletedLaps < 0)
        {
            // First lap - just set start data
            _lapStartData = currentTireData;
        }

        _lastCompletedLaps = completedLaps;
    }

    /// <summary>
    /// Shows "No data" state in the telemetry window
    /// </summary>
    public void ShowNoData()
    {
        _telemetryWindow?.ShowNoData();
    }

    /// <summary>
    /// Hides the telemetry window (does not close it)
    /// </summary>
    public void Stop()
    {
        lock (_lock)
        {
            _isRunning = false;

            // Hide window if it exists
            try
            {
                if (_telemetryWindow != null && !_telemetryWindow.Dispatcher.HasShutdownStarted)
                {
                    _telemetryWindow.Dispatcher.Invoke(() =>
                    {
                        _telemetryWindow?.Hide();
                    });
                    Console.WriteLine("[TELEMETRY] Window hidden");
                }
            }
            catch (TaskCanceledException)
            {
                // Window already closed, ignore
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TELEMETRY] Error hiding window: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Closes and disposes the telemetry window
    /// </summary>
    public void Dispose()
    {
        lock (_lock)
        {
            _isRunning = false;

            // Close WPF window if it exists
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

            _telemetryWindow = null;
            Console.WriteLine("[TELEMETRY] Disposed");
        }
    }
}

/// <summary>
/// Telemetry data snapshot
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
