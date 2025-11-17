using System;
using System.Threading;

namespace ACCRPMMonitor;

/// <summary>
/// Background thread that monitors for vehicle and track changes in ACC.
/// Provides immediate detection when the user switches vehicles or tracks.
/// </summary>
public class VehicleChangeMonitor : IDisposable
{
    private Thread? _monitorThread;
    private volatile bool _isRunning;
    private readonly VehicleDetector _detector;
    private string? _currentVehicle;
    private string? _currentTrack;

    public event EventHandler<VehicleChangedEventArgs>? VehicleChanged;
    public event EventHandler<TrackChangedEventArgs>? TrackChanged;

    public VehicleChangeMonitor()
    {
        _detector = new VehicleDetector();
    }

    /// <summary>
    /// Start monitoring for vehicle and track changes
    /// </summary>
    /// <param name="initialVehicle">The vehicle to monitor against</param>
    /// <param name="initialTrack">The track to monitor against</param>
    public void Start(string initialVehicle, string? initialTrack = null)
    {
        if (_isRunning)
            return;

        _currentVehicle = initialVehicle;
        _currentTrack = initialTrack;
        _isRunning = true;

        _monitorThread = new Thread(MonitorLoop)
        {
            IsBackground = true,
            Name = "VehicleChangeMonitor"
        };
        _monitorThread.Start();
    }

    /// <summary>
    /// Stop monitoring
    /// </summary>
    public void Stop()
    {
        _isRunning = false;
        _monitorThread?.Join(1000); // Wait up to 1 second for clean shutdown
    }

    private void MonitorLoop()
    {
        // Try to connect to ACC
        if (!_detector.Connect())
        {
            // Can't connect initially, will retry in loop
        }

        while (_isRunning)
        {
            try
            {
                // Try to connect if not already connected
                if (!_detector.Connect())
                {
                    Thread.Sleep(500);
                    continue;
                }

                // Check current vehicle
                string? detectedVehicle = _detector.GetCarModel();

                // If we detected a vehicle and it's different from current
                if (detectedVehicle != null &&
                    _currentVehicle != null &&
                    detectedVehicle != _currentVehicle)
                {
                    // Vehicle changed! Raise event
                    VehicleChanged?.Invoke(this, new VehicleChangedEventArgs
                    {
                        OldVehicle = _currentVehicle,
                        NewVehicle = detectedVehicle
                    });

                    // Update current vehicle
                    _currentVehicle = detectedVehicle;
                }

                // Check current track
                string? detectedTrack = _detector.GetTrackName();

                // If we detected a track and it's different from current
                if (detectedTrack != null &&
                    _currentTrack != null &&
                    detectedTrack != _currentTrack)
                {
                    // Track changed! Raise event
                    TrackChanged?.Invoke(this, new TrackChangedEventArgs
                    {
                        OldTrack = _currentTrack,
                        NewTrack = detectedTrack
                    });

                    // Update current track
                    _currentTrack = detectedTrack;
                }
                else if (detectedTrack != null && _currentTrack == null)
                {
                    // First time detecting track
                    _currentTrack = detectedTrack;
                }

                // Check every 100ms for responsive detection
                Thread.Sleep(100);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Vehicle monitor error: {ex.Message}");
                Thread.Sleep(1000);
            }
        }
    }

    public void Dispose()
    {
        Stop();
        _detector?.Dispose();
    }
}

/// <summary>
/// Event args for vehicle change
/// </summary>
public class VehicleChangedEventArgs : EventArgs
{
    public required string OldVehicle { get; init; }
    public required string NewVehicle { get; init; }
}

/// <summary>
/// Event args for track change
/// </summary>
public class TrackChangedEventArgs : EventArgs
{
    public required string OldTrack { get; init; }
    public required string NewTrack { get; init; }
}
