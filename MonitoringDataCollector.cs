namespace ACCRPMMonitor;

/// <summary>
/// Collects telemetry data during a monitoring session for later report generation.
/// Samples data at regular intervals to avoid excessive memory usage.
/// </summary>
public class MonitoringDataCollector
{
    private readonly string _vehicleName;
    private readonly string _trackName;
    private readonly string _monitoringMode;
    private readonly DateTime _sessionStart;
    private DateTime _lastSampleTime;
    private DateTime _lastGearTime;
    private int _lastGear = -1;

    // Sample interval to avoid collecting too much data (every 500ms)
    private readonly TimeSpan _sampleInterval = TimeSpan.FromMilliseconds(500);

    // Data collections
    private readonly List<TirePressureSnapshot> _tirePressureData = new();
    private readonly List<TireTemperatureSnapshot> _tireTemperatureData = new();
    private readonly List<RPMSnapshot> _rpmData = new();
    private readonly List<GearSnapshot> _gearData = new();
    private readonly Dictionary<int, int> _gearTimeDistribution = new(); // Gear -> Total time in ms
    private readonly Dictionary<int, List<float>> _accelerationByGear = new();

    public MonitoringDataCollector(string vehicleName, string trackName, string monitoringMode)
    {
        _vehicleName = vehicleName;
        _trackName = trackName;
        _monitoringMode = monitoringMode;
        _sessionStart = DateTime.Now;
        _lastSampleTime = _sessionStart;
        _lastGearTime = _sessionStart;
    }

    /// <summary>
    /// Updates the collector with current telemetry data.
    /// Call this every frame during monitoring.
    /// </summary>
    public void Update(int currentGear, int currentRPM, float speed, WheelAndTireData? tireData)
    {
        var now = DateTime.Now;

        // Track gear time distribution
        if (_lastGear >= 0 && currentGear >= 0)
        {
            var timeInGear = (int)(now - _lastGearTime).TotalMilliseconds;
            if (!_gearTimeDistribution.ContainsKey(_lastGear))
                _gearTimeDistribution[_lastGear] = 0;
            _gearTimeDistribution[_lastGear] += timeInGear;
        }

        if (currentGear != _lastGear)
        {
            _lastGear = currentGear;
            _lastGearTime = now;

            // Record gear change
            _gearData.Add(new GearSnapshot
            {
                Timestamp = now,
                Gear = currentGear
            });
        }

        // Sample data at intervals
        if (now - _lastSampleTime < _sampleInterval)
            return;

        _lastSampleTime = now;

        // Collect tire pressure data
        if (tireData != null)
        {
            _tirePressureData.Add(new TirePressureSnapshot
            {
                Timestamp = now,
                FL = tireData.WheelPressureFL,
                FR = tireData.WheelPressureFR,
                RL = tireData.WheelPressureRL,
                RR = tireData.WheelPressureRR,
                Average = tireData.AverageWheelPressure
            });

            _tireTemperatureData.Add(new TireTemperatureSnapshot
            {
                Timestamp = now,
                FL = tireData.TyreCoreTempFL,
                FR = tireData.TyreCoreTempFR,
                RL = tireData.TyreCoreTempRL,
                RR = tireData.TyreCoreTempRR,
                Average = (tireData.TyreCoreTempFL + tireData.TyreCoreTempFR +
                          tireData.TyreCoreTempRL + tireData.TyreCoreTempRR) / 4f
            });
        }

        // Collect RPM data
        _rpmData.Add(new RPMSnapshot
        {
            Timestamp = now,
            RPM = currentRPM,
            Gear = currentGear
        });

        // Collect acceleration data by gear (using speed as proxy)
        if (currentGear > 0) // Ignore neutral/reverse
        {
            if (!_accelerationByGear.ContainsKey(currentGear))
                _accelerationByGear[currentGear] = new List<float>();

            _accelerationByGear[currentGear].Add(speed);
        }
    }

    /// <summary>
    /// Generates the final monitoring session report.
    /// </summary>
    public MonitoringSessionReport GenerateReport()
    {
        var report = new MonitoringSessionReport
        {
            SessionStart = _sessionStart,
            SessionEnd = DateTime.Now,
            VehicleName = _vehicleName,
            TrackName = _trackName,
            MonitoringMode = _monitoringMode,
            TotalDataPoints = _rpmData.Count,

            TirePressureData = _tirePressureData,
            TireTemperatureData = _tireTemperatureData,
            RPMData = _rpmData,
            GearData = _gearData,
            GearTimeDistribution = _gearTimeDistribution,
            AccelerationByGear = _accelerationByGear
        };

        // Calculate tire pressure stats
        if (_tirePressureData.Count > 0)
        {
            var allPressures = _tirePressureData.SelectMany(t => new[] { t.FL, t.FR, t.RL, t.RR }).Where(p => p > 0);
            if (allPressures.Any())
            {
                report.AvgTirePressure = allPressures.Average();
                report.MinTirePressure = allPressures.Min();
                report.MaxTirePressure = allPressures.Max();
            }
        }

        // Calculate tire temperature stats
        if (_tireTemperatureData.Count > 0)
        {
            var allTemps = _tireTemperatureData.SelectMany(t => new[] { t.FL, t.FR, t.RL, t.RR }).Where(temp => temp > 0);
            if (allTemps.Any())
            {
                report.AvgTireTemp = allTemps.Average();
                report.MinTireTemp = allTemps.Min();
                report.MaxTireTemp = allTemps.Max();
            }
        }

        // Calculate acceleration stats by gear
        foreach (var kvp in _accelerationByGear)
        {
            if (kvp.Value.Count > 0)
            {
                report.GearAccelerationStats[kvp.Key] = new AccelerationStats
                {
                    Gear = kvp.Key,
                    MinSpeed = kvp.Value.Min(),
                    MaxSpeed = kvp.Value.Max(),
                    AvgSpeed = kvp.Value.Average(),
                    SampleCount = kvp.Value.Count
                };
            }
        }

        return report;
    }
}
