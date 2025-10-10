namespace ACCRPMMonitor;

// Analyzes telemetry data to find optimal shift points for each gear
public class OptimalShiftAnalyzer
{
    private readonly List<TelemetryDataPoint> _dataPoints = new();
    private const float FullThrottleThreshold = 0.95f;
    private const int MinDataPointsPerGear = 50; // Need enough data to be confident
    private const float MinConfidenceThreshold = 0.50f; // Minimum acceptable confidence
    private DateTime _sessionStart = DateTime.Now;

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

    // Calculates confidence level based on number of data points with explanation
    private (float score, string reason) CalculateConfidenceWithReason(int dataPoints)
    {
        if (dataPoints < MinDataPointsPerGear)
            return (0f, $"Insufficient data: only {dataPoints} points (need at least {MinDataPointsPerGear})");
        if (dataPoints < 100)
            return (0.5f, $"Low confidence: {dataPoints} points (minimal data collected)");
        if (dataPoints < 200)
            return (0.75f, $"Medium confidence: {dataPoints} points (moderate data collected)");
        return (1.0f, $"High confidence: {dataPoints} points (abundant data collected)");
    }

    // Calculates confidence level based on number of data points
    private float CalculateConfidence(int dataPoints)
    {
        return CalculateConfidenceWithReason(dataPoints).score;
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
        _sessionStart = DateTime.Now;
    }

    public int GetDataPointCount() => _dataPoints.Count;

    // Generates a detailed data collection report for gears 1-6
    public DataCollectionReport GenerateDetailedReport(string vehicleName)
    {
        var report = new DataCollectionReport
        {
            SessionStart = _sessionStart,
            SessionEnd = DateTime.Now,
            VehicleName = vehicleName,
            TotalDataPoints = _dataPoints.Count
        };

        var gearAnalyses = new List<DataCollectionReport.GearAnalysis>();
        int successfulGears = 0;

        // Analyze gears 1-6 specifically
        for (int gear = 1; gear <= 6; gear++)
        {
            var analysis = AnalyzeGear(gear);
            gearAnalyses.Add(analysis);
            if (analysis.PassedConfidenceThreshold && analysis.OptimalShiftRPM.HasValue)
            {
                successfulGears++;
            }
        }

        report.GearAnalyses = gearAnalyses;

        // Determine overall success: need all gears 1-6 with sufficient confidence
        report.OverallSuccess = successfulGears == 6;

        // Generate summary
        if (report.OverallSuccess)
        {
            report.SessionSummary = $"SUCCESS: All 6 gears have optimal shift points detected with sufficient confidence.";
        }
        else
        {
            int failedGears = 6 - successfulGears;
            report.SessionSummary = $"INCOMPLETE: {successfulGears}/6 gears successfully analyzed. {failedGears} gear(s) need more data.";
        }

        // Generate recommendations
        foreach (var analysis in gearAnalyses.Where(a => !a.PassedConfidenceThreshold || !a.OptimalShiftRPM.HasValue))
        {
            if (!analysis.OptimalShiftRPM.HasValue)
            {
                report.Recommendations.Add($"Gear {analysis.Gear}: Could not detect optimal shift point. Make sure to reach near redline in this gear during full throttle.");
            }
            else if (analysis.ConfidenceScore < MinConfidenceThreshold)
            {
                report.Recommendations.Add($"Gear {analysis.Gear}: Need more full-throttle data points (currently {analysis.FullThrottleDataPoints}, need at least {MinDataPointsPerGear}).");
            }
        }

        if (!report.OverallSuccess)
        {
            report.Recommendations.Add("Perform another hotlap focusing on the gears that failed, making sure to redline each gear under full throttle.");
        }

        return report;
    }

    // Analyzes a specific gear in detail
    private DataCollectionReport.GearAnalysis AnalyzeGear(int gear)
    {
        var allGearData = _dataPoints.Where(p => p.Gear == gear).ToList();
        var fullThrottleData = allGearData.Where(p => p.Throttle >= FullThrottleThreshold).ToList();

        var analysis = new DataCollectionReport.GearAnalysis
        {
            Gear = gear,
            TotalDataPoints = allGearData.Count,
            FullThrottleDataPoints = fullThrottleData.Count
        };

        if (fullThrottleData.Count > 0)
        {
            analysis.MinRPM = fullThrottleData.Min(p => p.RPM);
            analysis.MaxRPM = fullThrottleData.Max(p => p.RPM);
            analysis.MinSpeed = fullThrottleData.Min(p => p.Speed);
            analysis.MaxSpeed = fullThrottleData.Max(p => p.Speed);

            // Calculate RPM distribution (100 RPM buckets)
            var distribution = fullThrottleData
                .GroupBy(p => (p.RPM / 100) * 100)
                .ToDictionary(g => g.Key, g => g.Count());
            analysis.RPMDistribution = distribution;
        }

        // Calculate optimal shift point
        analysis.OptimalShiftRPM = CalculateOptimalUpshiftRPM(gear);

        // Calculate confidence with reason
        var (score, reason) = CalculateConfidenceWithReason(fullThrottleData.Count);
        analysis.ConfidenceScore = score;
        analysis.ConfidenceReason = reason;
        analysis.PassedConfidenceThreshold = score >= MinConfidenceThreshold && analysis.OptimalShiftRPM.HasValue;

        return analysis;
    }
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
