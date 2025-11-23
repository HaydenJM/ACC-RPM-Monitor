using System;
using System.Threading;

namespace ACCRPMMonitor;

/// <summary>
/// Monitors ACC game state changes (pause, live, replay, etc.)
/// Detects pause → unpause transitions to trigger vehicle/track config updates
///
/// ACC Status codes:
/// 0 = OFF (game closed)
/// 1 = REPLAY
/// 2 = LIVE (driving)
/// 3 = PAUSE
/// </summary>
public class GameStateMonitor : IDisposable
{
    private Thread? _monitorThread;
    private volatile bool _isRunning;
    private readonly SharedMemoryReader _memoryReader;
    private int _lastStatus = -1;
    private string? _lastVehicle = null;
    private string? _lastTrack = null;

    // Events for state changes
    public event EventHandler? PauseDetected;
    public event EventHandler? UnpauseDetected;
    public event EventHandler<VehicleTrackChangedEventArgs>? VehicleOrTrackChanged;

    public GameStateMonitor()
    {
        _memoryReader = new SharedMemoryReader();
    }

    public void Start()
    {
        if (_isRunning)
            return;

        _isRunning = true;

        _monitorThread = new Thread(MonitorLoop)
        {
            IsBackground = true,
            Name = "GameStateMonitor"
        };
        _monitorThread.Start();
    }

    public void Stop()
    {
        _isRunning = false;
        _monitorThread?.Join(1000);
    }

    private void MonitorLoop()
    {
        // Try to connect
        if (!_memoryReader.Connect())
        {
            // Will retry in loop
        }

        while (_isRunning)
        {
            try
            {
                // Try to connect if not connected
                if (!_memoryReader.IsConnected)
                {
                    if (!_memoryReader.Connect())
                    {
                        Thread.Sleep(500);
                        continue;
                    }
                }

                // Read current status
                int? currentStatus = _memoryReader.ReadStatus();

                if (currentStatus.HasValue)
                {
                    // Detect pause → unpause (PAUSE=3 → LIVE=2)
                    if (_lastStatus == 3 && currentStatus.Value == 2)
                    {
                        // Unpause detected! Check for vehicle/track changes
                        UnpauseDetected?.Invoke(this, EventArgs.Empty);

                        // Use separate detector to check vehicle/track
                        using var detector = new VehicleDetector();
                        if (detector.Connect())
                        {
                            string? newVehicle = detector.GetCarModel();
                            string? newTrack = detector.GetTrackName();

                            // Check if vehicle or track changed
                            bool vehicleChanged = newVehicle != null && newVehicle != _lastVehicle;
                            bool trackChanged = newTrack != null && newTrack != _lastTrack;

                            if (vehicleChanged || trackChanged)
                            {
                                VehicleOrTrackChanged?.Invoke(this, new VehicleTrackChangedEventArgs
                                {
                                    OldVehicle = _lastVehicle,
                                    NewVehicle = newVehicle,
                                    OldTrack = _lastTrack,
                                    NewTrack = newTrack,
                                    VehicleChanged = vehicleChanged,
                                    TrackChanged = trackChanged
                                });

                                _lastVehicle = newVehicle;
                                _lastTrack = newTrack;
                            }
                        }
                    }
                    // Detect live → pause
                    else if (_lastStatus == 2 && currentStatus.Value == 3)
                    {
                        PauseDetected?.Invoke(this, EventArgs.Empty);
                    }
                    // Initialize tracking on first live state
                    else if (_lastStatus < 0 && currentStatus.Value == 2)
                    {
                        using var detector = new VehicleDetector();
                        if (detector.Connect())
                        {
                            _lastVehicle = detector.GetCarModel();
                            _lastTrack = detector.GetTrackName();
                        }
                    }

                    _lastStatus = currentStatus.Value;
                }

                // Check every 100ms for responsiveness
                Thread.Sleep(100);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Game state monitor error: {ex.Message}");
                Thread.Sleep(1000);
            }
        }
    }

    public void Dispose()
    {
        Stop();
        _memoryReader?.Dispose();
    }
}

/// <summary>
/// Event args for vehicle/track changes
/// </summary>
public class VehicleTrackChangedEventArgs : EventArgs
{
    public string? OldVehicle { get; init; }
    public string? NewVehicle { get; init; }
    public string? OldTrack { get; init; }
    public string? NewTrack { get; init; }
    public bool VehicleChanged { get; init; }
    public bool TrackChanged { get; init; }
}
