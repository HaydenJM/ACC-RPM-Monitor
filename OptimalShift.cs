namespace ACCRPMMonitor;

// Analyzes telemetry data to find optimal shift points for each gear
public class OptimalShift
{
    private readonly List<TelemetryDataPoint> _dataPoints = new();
    private const float FullThrottleThreshold = 0.85f; // Lowered from 0.95f - 85% throttle is more realistic
    private const int MinDataPointsPerGear = 30; // Lowered from 50 - need less data to be confident
    private const float MinConfidenceThreshold = 0.50f; // Minimum acceptable confidence
    private DateTime _sessionStart = DateTime.Now;

    private int _lastRPM = 0;
    private DateTime _lastDataPointTime = DateTime.MinValue;

    // Adds a telemetry data point during data collection
    public void AddDataPoint(int rpm, float throttle, float speed, int gear)
    {
        // Filter out invalid data: ignore if speed is very low or throttle is too low
        // Speed > 5 km/h filters out standing starts
        // Speed 49-51 km/h filters out pit limiter activation
        // Throttle >= 85% ensures we're accelerating hard
        if (speed <= 5f || (speed >= 49f && speed <= 51f) || throttle < FullThrottleThreshold)
        {
            _lastRPM = rpm;
            _lastDataPointTime = DateTime.Now;
            return;
        }

        // ADDITIONAL FILTER: Only collect data when RPMs are actually RISING
        // This prevents collecting corner data where throttle is high but you're maintaining RPM
        var now = DateTime.Now;
        if (_lastRPM != 0 && _lastDataPointTime != DateTime.MinValue)
        {
            var timeDelta = (now - _lastDataPointTime).TotalSeconds;
            if (timeDelta > 0.01 && timeDelta < 1.0) // Valid time window
            {
                var rpmRate = (rpm - _lastRPM) / timeDelta;

                // Only collect data when RPMs are rising at least 100 RPM/sec
                // This filters out corner maintenance throttle
                if (rpmRate < 100)
                {
                    _lastRPM = rpm;
                    _lastDataPointTime = now;
                    return;
                }
            }
        }

        _dataPoints.Add(new TelemetryDataPoint
        {
            RPM = rpm,
            Throttle = throttle,
            Speed = speed,
            Gear = gear,
            Timestamp = now
        });

        _lastRPM = rpm;
        _lastDataPointTime = now;
    }

    // Finds the optimal upshift RPM for a specific gear based on acceleration optimization
    public int? CalculateOptimalUpshiftRPM(int gear)
    {
        // Get all full-throttle data points for this gear and the next gear
        var currentGearData = _dataPoints
            .Where(p => p.Gear == gear && p.Throttle >= FullThrottleThreshold)
            .OrderBy(p => p.Timestamp)
            .ToList();

        var nextGearData = _dataPoints
            .Where(p => p.Gear == gear + 1 && p.Throttle >= FullThrottleThreshold)
            .OrderBy(p => p.Timestamp)
            .ToList();

        if (currentGearData.Count < MinDataPointsPerGear)
            return null; // Not enough data for current gear

        // Calculate acceleration rates for current gear at different RPM levels
        var currentGearAccel = CalculateAccelerationByRPM(currentGearData);

        if (currentGearAccel.Count == 0)
            return null;

        // If we don't have next gear data, fall back to max speed method
        if (nextGearData.Count < MinDataPointsPerGear)
        {
            return CalculateOptimalUpshiftRPM_MaxSpeedFallback(currentGearData);
        }

        // Calculate acceleration rates for next gear
        var nextGearAccel = CalculateAccelerationByRPM(nextGearData);

        if (nextGearAccel.Count == 0)
        {
            return CalculateOptimalUpshiftRPM_MaxSpeedFallback(currentGearData);
        }

        // Estimate gear ratio between current and next gear
        float gearRatio = EstimateGearRatio(currentGearData, nextGearData);

        if (gearRatio <= 0)
        {
            return CalculateOptimalUpshiftRPM_MaxSpeedFallback(currentGearData);
        }

        // Find the RPM where staying in current gear gives worse acceleration than shifting
        int? optimalShiftPoint = FindAccelerationCrossoverPoint(
            currentGearAccel,
            nextGearAccel,
            gearRatio
        );

        if (optimalShiftPoint.HasValue)
        {
            return optimalShiftPoint;
        }

        // Fallback to max speed method if acceleration-based method fails
        return CalculateOptimalUpshiftRPM_MaxSpeedFallback(currentGearData);
    }

    // Fallback method: finds shift point based on maximum speed achieved
    // For cars that accelerate well to redline, shift near the top of the rev range
    private int? CalculateOptimalUpshiftRPM_MaxSpeedFallback(List<TelemetryDataPoint> gearData)
    {
        if (gearData.Count == 0)
            return null;

        // Find the maximum RPM and speed achieved in this gear
        int maxRPM = gearData.Max(p => p.RPM);
        float maxSpeed = gearData.Max(p => p.Speed);

        // Check if acceleration continues strongly near redline
        // by looking at speed achieved in the top 10% of RPM range
        var topRPMData = gearData.Where(p => p.RPM >= maxRPM * 0.90f).ToList();

        if (topRPMData.Count > 0)
        {
            float topRPMAvgSpeed = topRPMData.Average(p => p.Speed);

            // If speed near redline is close to max speed, the car accelerates well to redline
            if (topRPMAvgSpeed >= maxSpeed * 0.95f)
            {
                // Shift at 98% of max RPM observed (near redline)
                return (int)(maxRPM * 0.98f);
            }
        }

        // Otherwise, find where acceleration starts to drop off
        // This is the highest RPM that still achieves at least 99% of max speed
        var optimalPoint = gearData
            .Where(p => p.Speed >= maxSpeed * 0.99f)
            .OrderByDescending(p => p.RPM)  // Changed to OrderByDescending - use HIGHEST RPM, not lowest
            .FirstOrDefault();

        return optimalPoint?.RPM;
    }

    // Calculates acceleration (speed change over time) grouped by RPM ranges
    private Dictionary<int, float> CalculateAccelerationByRPM(List<TelemetryDataPoint> gearData)
    {
        var accelerationByRPM = new Dictionary<int, List<float>>();
        const int rpmBucketSize = 100; // Group by 100 RPM buckets
        const float minTimeDelta = 0.01f; // Minimum 10ms between samples

        // Calculate acceleration between consecutive points
        for (int i = 1; i < gearData.Count; i++)
        {
            var prev = gearData[i - 1];
            var curr = gearData[i];

            float timeDelta = (float)(curr.Timestamp - prev.Timestamp).TotalSeconds;

            // Skip if time delta is too small or negative (could be from different laps)
            if (timeDelta < minTimeDelta || timeDelta > 1.0f)
                continue;

            float speedDelta = curr.Speed - prev.Speed;

            // Only consider positive acceleration (ignore braking/coasting)
            if (speedDelta <= 0)
                continue;

            float acceleration = speedDelta / timeDelta;

            // Bucket by RPM (use average RPM of the two points)
            int avgRPM = (prev.RPM + curr.RPM) / 2;
            int rpmBucket = (avgRPM / rpmBucketSize) * rpmBucketSize;

            if (!accelerationByRPM.ContainsKey(rpmBucket))
                accelerationByRPM[rpmBucket] = new List<float>();

            accelerationByRPM[rpmBucket].Add(acceleration);
        }

        // Average the acceleration values in each bucket
        return accelerationByRPM
            .Where(kvp => kvp.Value.Count >= 3) // Need at least 3 samples per bucket
            .ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Average()
            );
    }

    // Estimates the gear ratio between current and next gear based on RPM/Speed relationship
    private float EstimateGearRatio(List<TelemetryDataPoint> currentGearData, List<TelemetryDataPoint> nextGearData)
    {
        // Find overlapping speed ranges between the two gears
        float currentMinSpeed = currentGearData.Min(p => p.Speed);
        float currentMaxSpeed = currentGearData.Max(p => p.Speed);
        float nextMinSpeed = nextGearData.Min(p => p.Speed);
        float nextMaxSpeed = nextGearData.Max(p => p.Speed);

        // Find the overlapping speed range
        float overlapMin = Math.Max(currentMinSpeed, nextMinSpeed);
        float overlapMax = Math.Min(currentMaxSpeed, nextMaxSpeed);

        if (overlapMin >= overlapMax)
            return 0; // No overlap

        // Get RPM/Speed ratios in the overlap region
        var currentRatios = currentGearData
            .Where(p => p.Speed >= overlapMin && p.Speed <= overlapMax)
            .Select(p => p.RPM / p.Speed)
            .ToList();

        var nextRatios = nextGearData
            .Where(p => p.Speed >= overlapMin && p.Speed <= overlapMax)
            .Select(p => p.RPM / p.Speed)
            .ToList();

        if (currentRatios.Count == 0 || nextRatios.Count == 0)
            return 0;

        float avgCurrentRatio = currentRatios.Average();
        float avgNextRatio = nextRatios.Average();

        // Gear ratio is how much RPM drops when shifting
        return avgCurrentRatio / avgNextRatio;
    }

    // Finds the RPM where acceleration in next gear would be better than current gear
    private int? FindAccelerationCrossoverPoint(
        Dictionary<int, float> currentGearAccel,
        Dictionary<int, float> nextGearAccel,
        float gearRatio)
    {
        int? bestShiftPoint = null;
        float bestAdvantageMargin = 0;

        // Adaptive threshold: check if current gear pulls strongly to high RPM
        // GT3 cars typically pull well to redline, so default to stricter threshold
        var maxRPM = currentGearAccel.Keys.Max();
        var minRPM = currentGearAccel.Keys.Min();
        var rpmRange = maxRPM - minRPM;

        // Check acceleration behavior in top 20% of RPM range
        var topRPMThreshold = maxRPM - (rpmRange * 0.2f);
        // Optimized for SHORT-TERM acceleration: shift as soon as next gear becomes meaningfully better
        // Lower threshold = earlier shifts = maximize instantaneous acceleration
        const float minimumAdvantageThreshold = 0.03f; // 3% advantage is sufficient for short-term optimization

        // Find the FIRST RPM where next gear provides meaningful advantage
        // This prioritizes short-term acceleration over holding gears to redline
        foreach (var currentRPM in currentGearAccel.Keys.OrderBy(k => k))
        {
            float currentAcceleration = currentGearAccel[currentRPM];

            // Calculate what RPM we'd be at in next gear after shifting
            int nextGearRPM = (int)(currentRPM / gearRatio);

            // Find the closest RPM bucket in next gear data
            var closestNextGearRPM = nextGearAccel.Keys
                .OrderBy(rpm => Math.Abs(rpm - nextGearRPM))
                .FirstOrDefault();

            if (closestNextGearRPM == 0)
                continue;

            // Only consider if we're within reasonable range (±75 RPM)
            if (Math.Abs(closestNextGearRPM - nextGearRPM) > 75)
                continue;

            float nextGearAcceleration = nextGearAccel[closestNextGearRPM];

            // Calculate acceleration advantage as a percentage
            float advantageRatio = (nextGearAcceleration - currentAcceleration) / currentAcceleration;

            // Find the first point where next gear becomes meaningfully better
            // This maximizes short-term acceleration by shifting as soon as it's beneficial
            if (advantageRatio > minimumAdvantageThreshold && advantageRatio > bestAdvantageMargin)
            {
                bestAdvantageMargin = advantageRatio;
                bestShiftPoint = currentRPM;
            }
        }

        return bestShiftPoint;
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

            // Store acceleration curves for visualization
            var gearData = _dataPoints
                .Where(p => p.Gear == gear && p.Throttle >= FullThrottleThreshold)
                .OrderBy(p => p.Timestamp)
                .ToList();

            if (gearData.Count >= MinDataPointsPerGear)
            {
                var accelCurve = CalculateAccelerationByRPM(gearData);
                if (accelCurve.Count > 0)
                {
                    config.AccelerationCurves[gear] = accelCurve;
                }
            }

            // Calculate and store gear ratios
            if (gear < 8)
            {
                var nextGearData = _dataPoints
                    .Where(p => p.Gear == gear + 1 && p.Throttle >= FullThrottleThreshold)
                    .OrderBy(p => p.Timestamp)
                    .ToList();

                if (gearData.Count >= MinDataPointsPerGear && nextGearData.Count >= MinDataPointsPerGear)
                {
                    float gearRatio = EstimateGearRatio(gearData, nextGearData);
                    if (gearRatio > 0)
                    {
                        config.GearRatios[gear] = gearRatio;
                    }
                }
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
        if (dataPoints < 60)
            return (0.6f, $"Acceptable confidence: {dataPoints} points (sufficient data collected)");
        if (dataPoints < 120)
            return (0.8f, $"Good confidence: {dataPoints} points (good data collected)");
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

    // Gets data point count for a specific gear
    public int GetDataPointCountForGear(int gear) =>
        _dataPoints.Count(p => p.Gear == gear && p.Throttle >= FullThrottleThreshold);

    // Generates a detailed data collection report for gears 1-6
    public DataCollectionReport GenerateDetailedReport(string vehicleName)
    {
        var report = new DataReport
        {
            SessionStart = _sessionStart,
            SessionEnd = DateTime.Now,
            VehicleName = vehicleName,
            TotalDataPoints = _dataPoints.Count
        };

        var gearAnalyses = new List<DataReport.GearAnalysis>();
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
    private DataReport.GearAnalysis AnalyzeGear(int gear)
    {
        var allGearData = _dataPoints.Where(p => p.Gear == gear).ToList();
        var fullThrottleData = allGearData.Where(p => p.Throttle >= FullThrottleThreshold).ToList();

        var analysis = new DataReport.GearAnalysis
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
    public Dictionary<int, float> GearRatios { get; set; } = new(); // Gear N -> ratio to gear N+1
    public Dictionary<int, Dictionary<int, float>> AccelerationCurves { get; set; } = new(); // Gear -> (RPM -> acceleration)
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
