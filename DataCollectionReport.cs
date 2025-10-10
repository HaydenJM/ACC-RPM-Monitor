using System.Text.Json;

namespace ACCRPMMonitor;

// Detailed report of a data collection session for auto configuration
public class DataCollectionReport
{
    public DateTime SessionStart { get; set; }
    public DateTime SessionEnd { get; set; }
    public string VehicleName { get; set; } = "";
    public int TotalDataPoints { get; set; }
    public List<GearAnalysis> GearAnalyses { get; set; } = new();
    public bool OverallSuccess { get; set; }
    public List<string> Recommendations { get; set; } = new();
    public string SessionSummary { get; set; } = "";

    // Analysis for a specific gear
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

    // Saves the report to a JSON file
    public void SaveToFile(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            string timestamp = SessionStart.ToString("yyyyMMdd_HHmmss");
            string filename = $"DataCollectionReport_{VehicleName}_{timestamp}.json";
            string filepath = Path.Combine(directory, filename);

            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(filepath, json);

            Console.WriteLine($"Data collection report saved: {filepath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving report: {ex.Message}");
        }
    }

    // Saves a human-readable text version alongside the JSON
    public void SaveHumanReadableReport(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            string timestamp = SessionStart.ToString("yyyyMMdd_HHmmss");
            string filename = $"DataCollectionReport_{VehicleName}_{timestamp}.txt";
            string filepath = Path.Combine(directory, filename);

            using var writer = new StreamWriter(filepath);

            writer.WriteLine("=".PadRight(80, '='));
            writer.WriteLine("AUTO CONFIGURATION DATA COLLECTION REPORT");
            writer.WriteLine("=".PadRight(80, '='));
            writer.WriteLine();
            writer.WriteLine($"Vehicle:         {VehicleName}");
            writer.WriteLine($"Session Start:   {SessionStart:yyyy-MM-dd HH:mm:ss}");
            writer.WriteLine($"Session End:     {SessionEnd:yyyy-MM-dd HH:mm:ss}");
            writer.WriteLine($"Duration:        {(SessionEnd - SessionStart).TotalMinutes:F1} minutes");
            writer.WriteLine($"Total Data Pts:  {TotalDataPoints}");
            writer.WriteLine($"Overall Success: {(OverallSuccess ? "YES" : "NO")}");
            writer.WriteLine();
            writer.WriteLine(SessionSummary);
            writer.WriteLine();
            writer.WriteLine("=".PadRight(80, '='));
            writer.WriteLine("PER-GEAR ANALYSIS");
            writer.WriteLine("=".PadRight(80, '='));
            writer.WriteLine();

            foreach (var analysis in GearAnalyses.OrderBy(g => g.Gear))
            {
                writer.WriteLine($"GEAR {analysis.Gear}:");
                writer.WriteLine($"  Status:                 {(analysis.PassedConfidenceThreshold ? "PASSED ✓" : "FAILED ✗")}");
                writer.WriteLine($"  Total Data Points:      {analysis.TotalDataPoints}");
                writer.WriteLine($"  Full Throttle Points:   {analysis.FullThrottleDataPoints}");
                writer.WriteLine($"  RPM Range:              {analysis.MinRPM} - {analysis.MaxRPM} RPM");
                writer.WriteLine($"  Speed Range:            {analysis.MinSpeed:F1} - {analysis.MaxSpeed:F1} km/h");

                if (analysis.OptimalShiftRPM.HasValue)
                {
                    writer.WriteLine($"  Optimal Shift Point:    {analysis.OptimalShiftRPM} RPM");
                }
                else
                {
                    writer.WriteLine($"  Optimal Shift Point:    NOT DETECTED");
                }

                writer.WriteLine($"  Confidence Score:       {analysis.ConfidenceScore:F2} ({analysis.ConfidenceScore * 100:F0}%)");
                writer.WriteLine($"  Confidence Reason:      {analysis.ConfidenceReason}");
                writer.WriteLine();

                if (analysis.RPMDistribution.Count > 0)
                {
                    writer.WriteLine("  RPM Distribution (full throttle samples):");
                    foreach (var kvp in analysis.RPMDistribution.OrderBy(x => x.Key))
                    {
                        string bar = new string('█', Math.Min(kvp.Value / 5, 40));
                        writer.WriteLine($"    {kvp.Key,5} RPM: {bar} ({kvp.Value} samples)");
                    }
                    writer.WriteLine();
                }
            }

            if (Recommendations.Count > 0)
            {
                writer.WriteLine("=".PadRight(80, '='));
                writer.WriteLine("RECOMMENDATIONS");
                writer.WriteLine("=".PadRight(80, '='));
                writer.WriteLine();
                foreach (var rec in Recommendations)
                {
                    writer.WriteLine($"• {rec}");
                }
                writer.WriteLine();
            }

            writer.WriteLine("=".PadRight(80, '='));
            writer.WriteLine("HOW CONFIDENCE SCORES ARE CALCULATED");
            writer.WriteLine("=".PadRight(80, '='));
            writer.WriteLine();
            writer.WriteLine("Confidence scores are based on the number of full-throttle data points");
            writer.WriteLine("collected for each gear:");
            writer.WriteLine();
            writer.WriteLine("  < 50 points:   0.00 (No confidence - insufficient data)");
            writer.WriteLine("  50-99 points:  0.50 (Low confidence - minimal data)");
            writer.WriteLine("  100-199 pts:   0.75 (Medium confidence - moderate data)");
            writer.WriteLine("  200+ points:   1.00 (High confidence - abundant data)");
            writer.WriteLine();
            writer.WriteLine("The optimal shift point is determined by finding the RPM where the car");
            writer.WriteLine("achieves 99% of its maximum speed in that gear. This represents the point");
            writer.WriteLine("where the power curve has peaked and it's time to shift up.");
            writer.WriteLine();
            writer.WriteLine("For a successful auto configuration, we require:");
            writer.WriteLine("  • Gears 1-6 must have shift points detected");
            writer.WriteLine("  • Each gear must have confidence score >= 0.50");
            writer.WriteLine("  • At least 3 gears total with valid data");
            writer.WriteLine();

            Console.WriteLine($"Human-readable report saved: {filepath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving human-readable report: {ex.Message}");
        }
    }
}
