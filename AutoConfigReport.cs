namespace ACCRPMMonitor;

/// <summary>
/// Detailed report of a data collection session for auto configuration.
/// Used by OptimalShift analyzer during auto-configuration mode.
/// v3.7.1: Renamed from AutoConfigReport to DataReport for clarity.
/// </summary>
public class DataReport
{
    public DateTime SessionStart { get; set; }
    public DateTime SessionEnd { get; set; }
    public string VehicleName { get; set; } = "";
    public int TotalDataPoints { get; set; }
    public List<GearAnalysis> GearAnalyses { get; set; } = new();
    public bool OverallSuccess { get; set; }
    public List<string> Recommendations { get; set; } = new();
    public string SessionSummary { get; set; } = "";

    /// <summary>
    /// Analysis for a specific gear during data collection.
    /// </summary>
    public class GearAnalysis
    {
        public int Gear { get; set; }
        public int TotalDataPoints { get; set; }
        public int FullThrottleDataPoints { get; set; }
        public int MinRPM { get; set; }
        public int MaxRPM { get; set; }
        public float MinSpeed { get; set; }
        public float MaxSpeed { get; set; }
        public int? OptimalShiftRPM { get; set; }
        public float ConfidenceScore { get; set; }
        public string ConfidenceReason { get; set; } = "";
        public bool PassedConfidenceThreshold { get; set; }
        public Dictionary<int, int> RPMDistribution { get; set; } = new(); // RPM bucket -> count
    }
}
