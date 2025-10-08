namespace ACCRPMMonitor;

// Analyzes telemetry data to find optimal shift points for each gear
public class OptimalShiftAnalyzer
{
    private readonly List<TelemetryDataPoint> _dataPoints = new();
    private const float FullThrottleThreshold = 0.95f;
    private const int MinDataPointsPerGear = 50; // Need enough data to be confident

    // Adds a telemetry data point during data collection
    public void AddDataPoint(int rpm, float throttle, float speed, int gear)
    {
        _dataPoints.Add(new TelemetryDataPoint
        {
            RPM = rpm,
            Throttle = throttle,
            Speed = speed,
            Gear = gear,
            Timestamp = DateTime.Now
        });
    }

    // Finds the optimal upshift RPM for a specific gear
    public int? CalculateOptimalUpshiftRPM(int gear)
    {
        // Get all full-throttle data points for this gear
        var gearData = _dataPoints
            .Where(p => p.Gear == gear && p.Throttle >= FullThrottleThreshold)
            .ToList();

        if (gearData.Count < MinDataPointsPerGear)
            return null; // Not enough data

        // Find the maximum speed achieved in this gear
        float maxSpeed = gearData.Max(p => p.Speed);

        // Find the lowest RPM that achieved at least 99% of max speed
        // This represents the optimal shift point - don't shift before power peaks
        var optimalPoint = gearData
            .Where(p => p.Speed >= maxSpeed * 0.99f)
            .OrderBy(p => p.RPM)
            .FirstOrDefault();

        return optimalPoint?.RPM;
    }

    // Calculates optimal downshift RPM for a gear
    public int? CalculateOptimalDownshiftRPM(int fromGear, int toGear)
    {
        if (toGear >= fromGear)
            return null;

        // Get upshift point for the lower gear
        var upshiftRPM = CalculateOptimalUpshiftRPM(toGear);
        if (upshiftRPM == null)
            return null;

        // Downshift target should be about 70% of the upshift point
        // This keeps you in the power band after downshifting
        return (int)(upshiftRPM * 0.7f);
    }

    // Analyzes all collected data and generates optimal config
    public OptimalShiftConfig? GenerateOptimalConfig()
    {
        var config = new OptimalShiftConfig
        {
            LastUpdated = DateTime.Now,
            TotalDataPoints = _dataPoints.Count
        };

        // Calculate optimal shift points for each gear
        for (int gear = 1; gear <= 8; gear++)
        {
            var upshiftRPM = CalculateOptimalUpshiftRPM(gear);
            if (upshiftRPM.HasValue)
            {
                config.OptimalUpshiftRPM[gear] = upshiftRPM.Value;

                // Calculate confidence based on data quantity
                var gearDataCount = _dataPoints.Count(p => p.Gear == gear && p.Throttle >= FullThrottleThreshold);
                config.DataConfidence[gear] = CalculateConfidence(gearDataCount);
            }
        }

        // Only return if we have data for at least 3 gears
        if (config.OptimalUpshiftRPM.Count < 3)
            return null;

        return config;
    }

    // Calculates confidence level based on number of data points
    private float CalculateConfidence(int dataPoints)
    {
        if (dataPoints < MinDataPointsPerGear)
            return 0f;
        if (dataPoints < 100)
            return 0.5f;
        if (dataPoints < 200)
            return 0.75f;
        return 1.0f; // High confidence
    }

    // Returns averaged/smoothed data per gear for visualization
    public List<TelemetryDataPoint> GetSmoothedDataForGear(int gear, int bucketSize = 100)
    {
        var gearData = _dataPoints
            .Where(p => p.Gear == gear && p.Throttle >= FullThrottleThreshold)
            .GroupBy(p => (p.RPM / bucketSize) * bucketSize)
            .Select(g => new TelemetryDataPoint
            {
                RPM = g.Key,
                Speed = g.Average(p => p.Speed),
                Throttle = 1.0f,
                Gear = gear,
                Timestamp = g.First().Timestamp
            })
            .OrderBy(p => p.RPM)
            .ToList();

        return gearData;
    }

    public void Clear()
    {
        _dataPoints.Clear();
    }

    public int GetDataPointCount() => _dataPoints.Count;
}

// Represents a single telemetry data point
public class TelemetryDataPoint
{
    public int RPM { get; set; }
    public float Throttle { get; set; }
    public float Speed { get; set; }
    public int Gear { get; set; }
    public DateTime Timestamp { get; set; }
}

// Stores the optimal shift configuration generated from analysis
public class OptimalShiftConfig
{
    public Dictionary<int, int> OptimalUpshiftRPM { get; set; } = new();
    public Dictionary<int, float> DataConfidence { get; set; } = new();
    public DateTime LastUpdated { get; set; }
    public int TotalDataPoints { get; set; }

    // Converts to GearRPMConfig for use in the main application
    public GearRPMConfig ToGearRPMConfig()
    {
        var config = new GearRPMConfig();
        foreach (var kvp in OptimalUpshiftRPM)
        {
            config.SetRPMForGear(kvp.Key, kvp.Value);
        }
        return config;
    }
}
