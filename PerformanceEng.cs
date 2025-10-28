namespace ACCRPMMonitor;

/// <summary>
/// Machine learning engine that continuously optimizes shift points based on actual lap performance.
/// Uses a weighted scoring system that balances theoretical acceleration curves with real-world results.
/// </summary>
public class PerformanceEng
{
    private readonly PatternShift _shiftAnalyzer;
    private readonly OptimalShift _accelerationAnalyzer;

    // Learning parameters
    private const float AccelerationWeight = 0.4f; // Weight for physics-based acceleration analysis
    private const float PerformanceWeight = 0.6f; // Weight for actual lap performance
    private const int MinLapsForLearning = 3; // Minimum laps before adjusting from performance
    private const int MinShiftsPerGear = 5; // Minimum shifts per gear to learn from

    // Adaptive learning rate (starts conservative, increases with data confidence)
    private float _learningRate = 0.2f;

    public PerformanceEng(PatternShift shiftAnalyzer, OptimalShift accelerationAnalyzer)
    {
        _shiftAnalyzer = shiftAnalyzer;
        _accelerationAnalyzer = accelerationAnalyzer;
    }

    /// <summary>
    /// Generates optimal shift points by combining acceleration-based analysis with performance-based learning.
    /// </summary>
    public Dictionary<int, int> GenerateOptimalShiftPoints()
    {
        var optimalPoints = new Dictionary<int, int>();

        // Get physics-based optimal points (from acceleration analysis)
        var physicsBasedPoints = GetPhysicsBasedShiftPoints();

        // Get performance-based optimal points (from lap time correlation)
        var performanceBasedPoints = _shiftAnalyzer.AnalyzeOptimalShiftPoints(MinLapsForLearning);

        // Calculate learning rate based on data confidence
        UpdateLearningRate();

        // Combine both sources using weighted average
        for (int gear = 1; gear <= 6; gear++)
        {
            bool hasPhysicsData = physicsBasedPoints.ContainsKey(gear);
            bool hasPerformanceData = performanceBasedPoints.ContainsKey(gear);

            if (hasPhysicsData && hasPerformanceData)
            {
                // Both sources available - blend them
                int physicsRPM = physicsBasedPoints[gear];
                int performanceRPM = performanceBasedPoints[gear];

                // Use weighted average with adaptive learning
                float adjustedPerfWeight = PerformanceWeight * (1.0f + _learningRate);
                float adjustedPhysWeight = AccelerationWeight * (1.0f - _learningRate * 0.5f);
                float totalWeight = adjustedPerfWeight + adjustedPhysWeight;

                int blendedRPM = (int)((physicsRPM * adjustedPhysWeight + performanceRPM * adjustedPerfWeight) / totalWeight);

                optimalPoints[gear] = blendedRPM;
            }
            else if (hasPhysicsData)
            {
                // Only physics data available
                optimalPoints[gear] = physicsBasedPoints[gear];
            }
            else if (hasPerformanceData)
            {
                // Only performance data available
                optimalPoints[gear] = performanceBasedPoints[gear];
            }
            // If neither available, no recommendation for this gear
        }

        return optimalPoints;
    }

    /// <summary>
    /// Gets shift points from the acceleration-based analyzer.
    /// </summary>
    private Dictionary<int, int> GetPhysicsBasedShiftPoints()
    {
        var points = new Dictionary<int, int>();

        for (int gear = 1; gear <= 6; gear++)
        {
            var optimalRPM = _accelerationAnalyzer.CalculateOptimalUpshiftRPM(gear);
            if (optimalRPM.HasValue)
            {
                points[gear] = optimalRPM.Value;
            }
        }

        return points;
    }

    /// <summary>
    /// Updates the learning rate based on data confidence (more data = trust performance more).
    /// </summary>
    private void UpdateLearningRate()
    {
        int validLaps = _shiftAnalyzer.GetValidLaps();

        // Learning rate increases with more laps (caps at 0.8)
        // 3 laps = 0.2, 5 laps = 0.35, 10 laps = 0.6, 20+ laps = 0.8
        _learningRate = Math.Min(0.8f, 0.2f + (validLaps / 25f));
    }

    /// <summary>
    /// Generates a comprehensive report comparing physics-based vs performance-based shift points.
    /// </summary>
    public LearningReport GenerateLearningReport()
    {
        var report = new LearningReport
        {
            GeneratedAt = DateTime.Now,
            LearningRate = _learningRate,
            TotalLaps = _shiftAnalyzer.GetTotalLaps(),
            ValidLaps = _shiftAnalyzer.GetValidLaps(),
            TotalShifts = _shiftAnalyzer.GetTotalShifts()
        };

        var physicsPoints = GetPhysicsBasedShiftPoints();
        var performancePoints = _shiftAnalyzer.AnalyzeOptimalShiftPoints(MinLapsForLearning);
        var blendedPoints = GenerateOptimalShiftPoints();

        for (int gear = 1; gear <= 6; gear++)
        {
            var gearReport = new GearLearningReport
            {
                Gear = gear,
                PhysicsBasedRPM = physicsPoints.ContainsKey(gear) ? physicsPoints[gear] : (int?)null,
                PerformanceBasedRPM = performancePoints.ContainsKey(gear) ? performancePoints[gear] : (int?)null,
                BlendedRPM = blendedPoints.ContainsKey(gear) ? blendedPoints[gear] : (int?)null
            };

            // Calculate difference between physics and performance
            if (gearReport.PhysicsBasedRPM.HasValue && gearReport.PerformanceBasedRPM.HasValue)
            {
                gearReport.Difference = gearReport.PerformanceBasedRPM.Value - gearReport.PhysicsBasedRPM.Value;

                // Determine recommendation
                if (Math.Abs(gearReport.Difference) < 100)
                {
                    gearReport.Interpretation = "Physics and performance agree - high confidence";
                }
                else if (gearReport.Difference > 0)
                {
                    gearReport.Interpretation = $"Performance suggests shifting {gearReport.Difference} RPM later";
                }
                else
                {
                    gearReport.Interpretation = $"Performance suggests shifting {-gearReport.Difference} RPM earlier";
                }
            }
            else if (gearReport.PhysicsBasedRPM.HasValue)
            {
                gearReport.Interpretation = "Using physics-based calculation (need more lap data)";
            }
            else if (gearReport.PerformanceBasedRPM.HasValue)
            {
                gearReport.Interpretation = "Using performance-based calculation (need more telemetry data)";
            }
            else
            {
                gearReport.Interpretation = "Insufficient data";
            }

            report.GearReports.Add(gearReport);
        }

        return report;
    }

    /// <summary>
    /// Gets a real-time recommendation for whether the current shift point is optimal.
    /// </summary>
    public ShiftPointRecommendation GetRecommendationForGear(int gear, int currentThreshold)
    {
        var optimalPoints = GenerateOptimalShiftPoints();

        if (!optimalPoints.ContainsKey(gear))
        {
            return new ShiftPointRecommendation
            {
                Gear = gear,
                CurrentRPM = currentThreshold,
                HasRecommendation = false,
                Message = "Insufficient data for recommendation"
            };
        }

        int recommendedRPM = optimalPoints[gear];
        int difference = currentThreshold - recommendedRPM;

        var recommendation = new ShiftPointRecommendation
        {
            Gear = gear,
            CurrentRPM = currentThreshold,
            RecommendedRPM = recommendedRPM,
            Difference = difference,
            HasRecommendation = true,
            Confidence = _learningRate
        };

        if (Math.Abs(difference) < 100)
        {
            recommendation.Message = "✓ Current shift point is optimal";
        }
        else if (difference > 0)
        {
            recommendation.Message = $"↓ Try shifting {difference} RPM earlier for better performance";
        }
        else
        {
            recommendation.Message = $"↑ Try shifting {-difference} RPM later for better performance";
        }

        return recommendation;
    }

    public float GetLearningRate() => _learningRate;
    public int GetDataQuality() => _shiftAnalyzer.GetValidLaps();
}

// Data models for learning engine

public class LearningReport
{
    public DateTime GeneratedAt { get; set; }
    public float LearningRate { get; set; }
    public int TotalLaps { get; set; }
    public int ValidLaps { get; set; }
    public int TotalShifts { get; set; }
    public List<GearLearningReport> GearReports { get; set; } = new();
}

public class GearLearningReport
{
    public int Gear { get; set; }
    public int? PhysicsBasedRPM { get; set; }
    public int? PerformanceBasedRPM { get; set; }
    public int? BlendedRPM { get; set; }
    public int Difference { get; set; } // Performance - Physics
    public string Interpretation { get; set; } = string.Empty;
}

public class ShiftPointRecommendation
{
    public int Gear { get; set; }
    public int CurrentRPM { get; set; }
    public int RecommendedRPM { get; set; }
    public int Difference { get; set; }
    public bool HasRecommendation { get; set; }
    public float Confidence { get; set; } // 0.0 to 1.0
    public string Message { get; set; } = string.Empty;
}
