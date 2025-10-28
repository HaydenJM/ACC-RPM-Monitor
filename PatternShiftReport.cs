using System.Text;
using System.Text.Json;

namespace ACCRPMMonitor;

/// <summary>
/// Generates detailed reports and visualizations for shift pattern analysis.
/// </summary>
public class PatternShiftReport
{
    private readonly string _baseDataPath;

    public PatternShiftReport()
    {
        // Use ./data directory next to application
        string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
        _baseDataPath = Path.Combine(appDirectory, "data");
        Directory.CreateDirectory(_baseDataPath);
    }

    /// <summary>
    /// Saves a comprehensive shift pattern analysis report.
    /// </summary>
    public string SaveShiftPatternReport(ShiftPatternReport report, LearningReport learningReport, string vehicleName)
    {
        // Create vehicle-specific directory
        string vehicleReportsPath = Path.Combine(_baseDataPath, vehicleName);
        Directory.CreateDirectory(vehicleReportsPath);

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string baseFileName = $"shift_analysis_{timestamp}";

        // Save JSON version
        string jsonPath = Path.Combine(vehicleReportsPath, $"{baseFileName}.json");
        SaveJsonReport(report, learningReport, jsonPath);

        // Save human-readable text version
        string textPath = Path.Combine(vehicleReportsPath, $"{baseFileName}.txt");
        SaveTextReport(report, learningReport, textPath);

        return textPath;
    }

    /// <summary>
    /// Saves JSON report for programmatic analysis.
    /// </summary>
    private void SaveJsonReport(ShiftPatternReport shiftReport, LearningReport learningReport, string path)
    {
        var combinedReport = new
        {
            ShiftPatternAnalysis = shiftReport,
            LearningAnalysis = learningReport
        };

        string json = JsonSerializer.Serialize(combinedReport, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(path, json);
    }

    /// <summary>
    /// Saves human-readable text report.
    /// </summary>
    private void SaveTextReport(ShiftPatternReport shiftReport, LearningReport learningReport, string path)
    {
        var sb = new StringBuilder();

        sb.AppendLine("═══════════════════════════════════════════════════════════════════");
        sb.AppendLine("               SHIFT PATTERN PERFORMANCE ANALYSIS");
        sb.AppendLine("═══════════════════════════════════════════════════════════════════");
        sb.AppendLine();
        sb.AppendLine($"Generated: {shiftReport.GeneratedAt:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        // Session Summary
        sb.AppendLine("SESSION SUMMARY");
        sb.AppendLine("───────────────────────────────────────────────────────────────────");
        sb.AppendLine($"Total Laps Completed:    {shiftReport.TotalLaps}");
        sb.AppendLine($"Valid Laps Analyzed:     {shiftReport.ValidLaps}");
        sb.AppendLine($"Total Shifts Recorded:   {shiftReport.TotalShifts}");
        sb.AppendLine($"Best Lap Time:           {shiftReport.FormatLapTime(shiftReport.BestLapTime)}");
        sb.AppendLine($"Average Lap Time:        {shiftReport.FormatLapTime(shiftReport.AverageLapTime)}");
        sb.AppendLine($"Off-Track Events:        {shiftReport.TotalOffTrackEvents}");
        sb.AppendLine($"Learning Rate:           {learningReport.LearningRate:P0}");
        sb.AppendLine();

        // Learning Analysis Overview
        sb.AppendLine("LEARNING ANALYSIS");
        sb.AppendLine("───────────────────────────────────────────────────────────────────");
        sb.AppendLine();
        sb.AppendLine("This report combines two data sources:");
        sb.AppendLine("  • Physics-based: Calculated from acceleration curves");
        sb.AppendLine("  • Performance-based: Learned from your actual lap times");
        sb.AppendLine();
        sb.AppendLine($"Current learning rate: {learningReport.LearningRate:P0}");
        sb.AppendLine($"  ({(learningReport.LearningRate < 0.3 ? "Conservative - building confidence" : learningReport.LearningRate < 0.6 ? "Moderate - good data quality" : "Aggressive - high confidence in performance data")})");
        sb.AppendLine();

        // Per-Gear Analysis
        sb.AppendLine("PER-GEAR SHIFT POINT ANALYSIS");
        sb.AppendLine("═══════════════════════════════════════════════════════════════════");

        foreach (var gearLearning in learningReport.GearReports.OrderBy(g => g.Gear))
        {
            sb.AppendLine();
            sb.AppendLine($"GEAR {gearLearning.Gear}");
            sb.AppendLine("───────────────────────────────────────────────────────────────────");

            // Shift point comparison
            sb.AppendLine("Shift Point Recommendations:");
            sb.AppendLine($"  Physics-Based:     {(gearLearning.PhysicsBasedRPM.HasValue ? $"{gearLearning.PhysicsBasedRPM} RPM" : "N/A")}");
            sb.AppendLine($"  Performance-Based: {(gearLearning.PerformanceBasedRPM.HasValue ? $"{gearLearning.PerformanceBasedRPM} RPM" : "N/A")}");
            sb.AppendLine($"  Blended (Optimal): {(gearLearning.BlendedRPM.HasValue ? $"{gearLearning.BlendedRPM} RPM" : "N/A")}");
            sb.AppendLine();
            sb.AppendLine($"Interpretation: {gearLearning.Interpretation}");

            // Detailed shift statistics from shift pattern report
            var gearShiftReport = shiftReport.GearReports.FirstOrDefault(g => g.Gear == gearLearning.Gear);
            if (gearShiftReport != null)
            {
                sb.AppendLine();
                sb.AppendLine("Shift Statistics:");
                sb.AppendLine($"  Total Shifts:      {gearShiftReport.TotalShifts}");
                sb.AppendLine($"  RPM Range:         {gearShiftReport.MinShiftRPM} - {gearShiftReport.MaxShiftRPM}");
                sb.AppendLine($"  Average Shift RPM: {gearShiftReport.AvgShiftRPM}");

                // RPM bucket performance breakdown
                if (gearShiftReport.RPMBuckets.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("Performance by Shift RPM Range:");
                    sb.AppendLine($"  {"RPM Range",-15} {"Shifts",-10} {"Avg Lap Time",-15} {"Off-Track",-12} {"Score",-10}");
                    sb.AppendLine($"  {new string('-', 15)} {new string('-', 10)} {new string('-', 15)} {new string('-', 12)} {new string('-', 10)}");

                    foreach (var bucket in gearShiftReport.RPMBuckets.OrderBy(b => b.RPM))
                    {
                        string rpmRange = $"{bucket.RPM}-{bucket.RPM + 200}";
                        string lapTime = shiftReport.FormatLapTime(bucket.AvgLapTime);
                        string offTrack = $"{bucket.AvgOffTrackTime:F2}s";
                        string score = $"{bucket.PerformanceScore:F0}";

                        // Mark the best performing RPM range
                        string marker = bucket.RPM == gearShiftReport.OptimalRPM ? " ✓" : "";

                        sb.AppendLine($"  {rpmRange,-15} {bucket.ShiftCount,-10} {lapTime,-15} {offTrack,-12} {score,-10}{marker}");
                    }

                    sb.AppendLine();
                    sb.AppendLine($"  ✓ Best performance at {gearShiftReport.OptimalRPM}-{gearShiftReport.OptimalRPM + 200} RPM");
                }
            }
        }

        // Recommendations
        sb.AppendLine();
        sb.AppendLine("═══════════════════════════════════════════════════════════════════");
        sb.AppendLine("RECOMMENDATIONS");
        sb.AppendLine("═══════════════════════════════════════════════════════════════════");
        sb.AppendLine();

        if (shiftReport.ValidLaps < 5)
        {
            sb.AppendLine("⚠ Complete more laps for better recommendations (need at least 5 valid laps)");
            sb.AppendLine();
        }

        foreach (var gearLearning in learningReport.GearReports.OrderBy(g => g.Gear))
        {
            if (gearLearning.BlendedRPM.HasValue)
            {
                string adjustment = "";
                if (Math.Abs(gearLearning.Difference) >= 100)
                {
                    adjustment = gearLearning.Difference > 0
                        ? $" (shift {gearLearning.Difference} RPM later)"
                        : $" (shift {-gearLearning.Difference} RPM earlier)";
                }

                sb.AppendLine($"Gear {gearLearning.Gear}: Use {gearLearning.BlendedRPM} RPM{adjustment}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("═══════════════════════════════════════════════════════════════════");
        sb.AppendLine("End of Report");
        sb.AppendLine("═══════════════════════════════════════════════════════════════════");

        File.WriteAllText(path, sb.ToString());
    }

    /// <summary>
    /// Generates a console-friendly summary for display during runtime.
    /// </summary>
    public string GenerateConsoleSummary(LearningReport learningReport)
    {
        var sb = new StringBuilder();

        sb.AppendLine("╔══════════════════════════════════════════════════════════════╗");
        sb.AppendLine("║           SHIFT PATTERN LEARNING SUMMARY                     ║");
        sb.AppendLine("╚══════════════════════════════════════════════════════════════╝");
        sb.AppendLine();
        sb.AppendLine($"Valid Laps: {learningReport.ValidLaps}  |  Learning Rate: {learningReport.LearningRate:P0}");
        sb.AppendLine();

        foreach (var gear in learningReport.GearReports.Where(g => g.BlendedRPM.HasValue).OrderBy(g => g.Gear))
        {
            string physicsStr = gear.PhysicsBasedRPM?.ToString() ?? "N/A";
            string perfStr = gear.PerformanceBasedRPM?.ToString() ?? "N/A";
            string blendedStr = gear.BlendedRPM?.ToString() ?? "N/A";

            sb.AppendLine($"Gear {gear.Gear}: Physics: {physicsStr,5} | Performance: {perfStr,5} | Optimal: {blendedStr,5} RPM");
        }

        return sb.ToString();
    }

    public string GetReportsPath() => _baseDataPath;
}
