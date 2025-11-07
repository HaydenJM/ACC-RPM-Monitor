using System;
using System.Threading;

namespace ACCRPMMonitor;

/// <summary>
/// Background thread that monitors for vehicle changes in ACC.
/// Provides immediate detection when the user switches vehicles.
/// </summary>
public class VehicleChangeMonitor : IDisposable
{
    private Thread? _monitorThread;
    private volatile bool _isRunning;
    private readonly VehicleDetector _detector;
    private string? _currentVehicle;

    public event EventHandler<VehicleChangedEventArgs>? VehicleChanged;

    public VehicleChangeMonitor()
    {
        _detector = new VehicleDetector();
    }

    /// <summary>
    /// Start monitoring for vehicle changes
    /// </summary>
    /// <param name="initialVehicle">The vehicle to monitor against</param>
    public void Start(string initialVehicle)
    {
        if (_isRunning)
            return;

        _currentVehicle = initialVehicle;
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
