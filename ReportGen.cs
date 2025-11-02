using System.Text;
using System.Text.Json;

namespace ACCRPMMonitor;

/// <summary>
/// Unified report generation system for all data collection and performance analysis reports.
/// Handles both auto-configuration data reports and performance learning shift pattern reports.
/// v3.7.1: Consolidated from separate AutoConfigReport and PerformanceReport classes.
/// </summary>
public class ReportGen
{
    private readonly string _baseDataPath;
    private readonly ConfigManager _configManager;

    public ReportGen(ConfigManager configManager)
    {
        _configManager = configManager;
        // Use ./data directory next to application
        string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
        _baseDataPath = Path.Combine(appDirectory, "data");
        Directory.CreateDirectory(_baseDataPath);
    }

    #region Auto-Configuration Data Reports

    /// <summary>
    /// Saves auto-configuration data collection report (used during Create/Regen Auto Configuration).
    /// Structure: data/{car}/{track}/auto_config_report_{timestamp}.{txt|json}
    /// </summary>
    public void SaveAutoConfigReport(DataReport report)
    {
        string vehicleTrackDir = _configManager.GetVehicleDataDirectory();
        Directory.CreateDirectory(vehicleTrackDir);

        string timestamp = report.SessionStart.ToString("yyyyMMdd_HHmmss");
        string baseFileName = $"auto_config_report_{timestamp}";

        // Save JSON version
        string jsonPath = Path.Combine(vehicleTrackDir, $"{baseFileName}.json");
        var options = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(report, options);
        File.WriteAllText(jsonPath, json);

        // Save human-readable text version
        string textPath = Path.Combine(vehicleTrackDir, $"{baseFileName}.txt");
        SaveAutoConfigTextReport(report, textPath);

        Console.WriteLine($"Auto-configuration report saved: {textPath}");
    }

    private void SaveAutoConfigTextReport(DataReport report, string path)
    {
        using var writer = new StreamWriter(path);

        writer.WriteLine("=".PadRight(80, '='));
        writer.WriteLine("AUTO CONFIGURATION DATA COLLECTION REPORT");
        writer.WriteLine("=".PadRight(80, '='));
        writer.WriteLine();
        writer.WriteLine($"Vehicle:         {report.VehicleName}");
        writer.WriteLine($"Session Start:   {report.SessionStart:yyyy-MM-dd HH:mm:ss}");
        writer.WriteLine($"Session End:     {report.SessionEnd:yyyy-MM-dd HH:mm:ss}");
        writer.WriteLine($"Duration:        {(report.SessionEnd - report.SessionStart).TotalMinutes:F1} minutes");
        writer.WriteLine($"Total Data Pts:  {report.TotalDataPoints}");
        writer.WriteLine($"Overall Success: {(report.OverallSuccess ? "YES" : "NO")}");
        writer.WriteLine();
        writer.WriteLine(report.SessionSummary);
        writer.WriteLine();
        writer.WriteLine("=".PadRight(80, '='));
        writer.WriteLine("PER-GEAR ANALYSIS");
        writer.WriteLine("=".PadRight(80, '='));
        writer.WriteLine();

        foreach (var analysis in report.GearAnalyses.OrderBy(g => g.Gear))
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

        if (report.Recommendations.Count > 0)
        {
            writer.WriteLine("=".PadRight(80, '='));
            writer.WriteLine("RECOMMENDATIONS");
            writer.WriteLine("=".PadRight(80, '='));
            writer.WriteLine();
            foreach (var rec in report.Recommendations)
            {
                writer.WriteLine($"• {rec}");
            }
            writer.WriteLine();
        }

        writer.WriteLine("=".PadRight(80, '='));
        writer.WriteLine("End of Report");
        writer.WriteLine("=".PadRight(80, '='));
    }

    #endregion

    #region Performance Learning Reports

    /// <summary>
    /// Saves a comprehensive shift pattern analysis report (used during Performance Learning Mode).
    /// Structure: data/{car}/{track}/performance_report_{timestamp}.{txt|json}
    /// </summary>
    public string SavePerformanceReport(ShiftPatternReport shiftReport, LearningReport learningReport, string vehicleName)
    {
        string vehicleTrackDir = _configManager.GetVehicleDataDirectory();
        Directory.CreateDirectory(vehicleTrackDir);

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string baseFileName = $"performance_report_{timestamp}";

        // Save JSON version
        string jsonPath = Path.Combine(vehicleTrackDir, $"{baseFileName}.json");
        SavePerformanceJsonReport(shiftReport, learningReport, jsonPath);

        // Save human-readable text version
        string textPath = Path.Combine(vehicleTrackDir, $"{baseFileName}.txt");
        SavePerformanceTextReport(shiftReport, learningReport, textPath);

        return textPath;
    }

    private void SavePerformanceJsonReport(ShiftPatternReport shiftReport, LearningReport learningReport, string path)
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

    private void SavePerformanceTextReport(ShiftPatternReport shiftReport, LearningReport learningReport, string path)
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

    #endregion

    #region Monitoring Session Data Reports

    /// <summary>
    /// Saves a monitoring session telemetry data report.
    /// Generated automatically when any monitoring mode stops.
    /// Structure: data/{car}/{track}/monitoring_session_{timestamp}.{txt|json}
    /// </summary>
    public string SaveMonitoringSessionReport(MonitoringSessionReport report)
    {
        string vehicleTrackDir = _configManager.GetVehicleDataDirectory();
        Directory.CreateDirectory(vehicleTrackDir);

        string timestamp = report.SessionStart.ToString("yyyyMMdd_HHmmss");
        string baseFileName = $"monitoring_session_{timestamp}";

        // Save JSON version
        string jsonPath = Path.Combine(vehicleTrackDir, $"{baseFileName}.json");
        var options = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(report, options);
        File.WriteAllText(jsonPath, json);

        // Save human-readable text version
        string textPath = Path.Combine(vehicleTrackDir, $"{baseFileName}.txt");
        SaveMonitoringSessionTextReport(report, textPath);

        return textPath;
    }

    private void SaveMonitoringSessionTextReport(MonitoringSessionReport report, string path)
    {
        using var writer = new StreamWriter(path);

        writer.WriteLine("=".PadRight(80, '='));
        writer.WriteLine("MONITORING SESSION TELEMETRY DATA REPORT");
        writer.WriteLine("=".PadRight(80, '='));
        writer.WriteLine();
        writer.WriteLine($"Vehicle:         {report.VehicleName}");
        writer.WriteLine($"Track:           {report.TrackName}");
        writer.WriteLine($"Monitoring Mode: {report.MonitoringMode}");
        writer.WriteLine($"Session Start:   {report.SessionStart:yyyy-MM-dd HH:mm:ss}");
        writer.WriteLine($"Session End:     {report.SessionEnd:yyyy-MM-dd HH:mm:ss}");
        writer.WriteLine($"Duration:        {(report.SessionEnd - report.SessionStart).TotalMinutes:F1} minutes");
        writer.WriteLine($"Data Points:     {report.TotalDataPoints}");
        writer.WriteLine();

        // Tire Pressure Summary
        writer.WriteLine("=".PadRight(80, '='));
        writer.WriteLine("TIRE PRESSURE ANALYSIS (PSI)");
        writer.WriteLine("=".PadRight(80, '='));
        writer.WriteLine();
        writer.WriteLine($"Average:         {report.AvgTirePressure:F2} PSI");
        writer.WriteLine($"Minimum:         {report.MinTirePressure:F2} PSI");
        writer.WriteLine($"Maximum:         {report.MaxTirePressure:F2} PSI");
        writer.WriteLine($"Range:           {(report.MaxTirePressure - report.MinTirePressure):F2} PSI");
        writer.WriteLine($"Snapshots:       {report.TirePressureData.Count}");
        writer.WriteLine();

        if (report.TirePressureData.Count > 0)
        {
            writer.WriteLine("Tire Pressure Over Time:");
            writer.WriteLine($"  {"Time",-20} {"FL",-10} {"FR",-10} {"RL",-10} {"RR",-10} {"Avg",-10}");
            writer.WriteLine($"  {new string('-', 20)} {new string('-', 10)} {new string('-', 10)} {new string('-', 10)} {new string('-', 10)} {new string('-', 10)}");

            // Sample every Nth entry if too many data points (show first, middle, last)
            var sampled = SampleData(report.TirePressureData, 20);
            foreach (var snap in sampled)
            {
                writer.WriteLine($"  {snap.Timestamp:HH:mm:ss.fff}        {snap.FL,6:F2}     {snap.FR,6:F2}     {snap.RL,6:F2}     {snap.RR,6:F2}     {snap.Average,6:F2}");
            }
            writer.WriteLine();
        }

        // Tire Temperature Summary
        writer.WriteLine("=".PadRight(80, '='));
        writer.WriteLine("TIRE TEMPERATURE ANALYSIS (°C)");
        writer.WriteLine("=".PadRight(80, '='));
        writer.WriteLine();
        writer.WriteLine($"Average:         {report.AvgTireTemp:F2}°C");
        writer.WriteLine($"Minimum:         {report.MinTireTemp:F2}°C");
        writer.WriteLine($"Maximum:         {report.MaxTireTemp:F2}°C");
        writer.WriteLine($"Range:           {(report.MaxTireTemp - report.MinTireTemp):F2}°C");
        writer.WriteLine($"Snapshots:       {report.TireTemperatureData.Count}");
        writer.WriteLine();

        if (report.TireTemperatureData.Count > 0)
        {
            writer.WriteLine("Tire Temperature Over Time:");
            writer.WriteLine($"  {"Time",-20} {"FL",-10} {"FR",-10} {"RL",-10} {"RR",-10} {"Avg",-10}");
            writer.WriteLine($"  {new string('-', 20)} {new string('-', 10)} {new string('-', 10)} {new string('-', 10)} {new string('-', 10)} {new string('-', 10)}");

            var sampled = SampleData(report.TireTemperatureData, 20);
            foreach (var snap in sampled)
            {
                writer.WriteLine($"  {snap.Timestamp:HH:mm:ss.fff}        {snap.FL,6:F2}     {snap.FR,6:F2}     {snap.RL,6:F2}     {snap.RR,6:F2}     {snap.Average,6:F2}");
            }
            writer.WriteLine();
        }

        // RPM vs Time Analysis
        writer.WriteLine("=".PadRight(80, '='));
        writer.WriteLine("RPM vs TIME ANALYSIS");
        writer.WriteLine("=".PadRight(80, '='));
        writer.WriteLine();
        if (report.RPMData.Count > 0)
        {
            var minRPM = report.RPMData.Min(r => r.RPM);
            var maxRPM = report.RPMData.Max(r => r.RPM);
            var avgRPM = report.RPMData.Average(r => r.RPM);

            writer.WriteLine($"RPM Range:       {minRPM} - {maxRPM} RPM");
            writer.WriteLine($"Average RPM:     {avgRPM:F0} RPM");
            writer.WriteLine($"Snapshots:       {report.RPMData.Count}");
            writer.WriteLine();

            writer.WriteLine("RPM Over Time (Sample):");
            writer.WriteLine($"  {"Time",-20} {"RPM",-10} {"Gear",-10}");
            writer.WriteLine($"  {new string('-', 20)} {new string('-', 10)} {new string('-', 10)}");

            var sampled = SampleData(report.RPMData, 20);
            foreach (var snap in sampled)
            {
                writer.WriteLine($"  {snap.Timestamp:HH:mm:ss.fff}        {snap.RPM,6}     {snap.Gear,6}");
            }
            writer.WriteLine();
        }

        // Gear vs Time Analysis
        writer.WriteLine("=".PadRight(80, '='));
        writer.WriteLine("GEAR vs TIME ANALYSIS");
        writer.WriteLine("=".PadRight(80, '='));
        writer.WriteLine();
        if (report.GearTimeDistribution.Count > 0)
        {
            writer.WriteLine("Time Spent in Each Gear:");
            writer.WriteLine($"  {"Gear",-10} {"Time (s)",-15} {"Percentage",-15}");
            writer.WriteLine($"  {new string('-', 10)} {new string('-', 15)} {new string('-', 15)}");

            var totalTime = report.GearTimeDistribution.Values.Sum();
            foreach (var kvp in report.GearTimeDistribution.OrderBy(k => k.Key))
            {
                var timeSeconds = kvp.Value / 1000.0;
                var percentage = (kvp.Value / (double)totalTime) * 100;
                writer.WriteLine($"  {kvp.Key,-10} {timeSeconds,12:F2}    {percentage,12:F1}%");
            }
            writer.WriteLine();
        }

        // Acceleration vs Gear Analysis
        writer.WriteLine("=".PadRight(80, '='));
        writer.WriteLine("ACCELERATION vs GEAR ANALYSIS");
        writer.WriteLine("=".PadRight(80, '='));
        writer.WriteLine();
        if (report.GearAccelerationStats.Count > 0)
        {
            writer.WriteLine("Speed Range by Gear:");
            writer.WriteLine($"  {"Gear",-10} {"Min Speed",-15} {"Max Speed",-15} {"Avg Speed",-15} {"Samples",-10}");
            writer.WriteLine($"  {new string('-', 10)} {new string('-', 15)} {new string('-', 15)} {new string('-', 15)} {new string('-', 10)}");

            foreach (var kvp in report.GearAccelerationStats.OrderBy(k => k.Key))
            {
                var stats = kvp.Value;
                writer.WriteLine($"  {stats.Gear,-10} {stats.MinSpeed,12:F1} km/h {stats.MaxSpeed,12:F1} km/h {stats.AvgSpeed,12:F1} km/h {stats.SampleCount,7}");
            }
            writer.WriteLine();
        }

        writer.WriteLine("=".PadRight(80, '='));
        writer.WriteLine("End of Report");
        writer.WriteLine("=".PadRight(80, '='));
    }

    /// <summary>
    /// Samples data evenly from a list to reduce output size.
    /// Returns at most maxSamples items, evenly distributed.
    /// </summary>
    private List<T> SampleData<T>(List<T> data, int maxSamples)
    {
        if (data.Count <= maxSamples)
            return data;

        var result = new List<T>();
        var step = (double)data.Count / maxSamples;

        for (int i = 0; i < maxSamples; i++)
        {
            var index = (int)(i * step);
            if (index < data.Count)
                result.Add(data[index]);
        }

        return result;
    }

    #endregion

    public string GetReportsPath() => _baseDataPath;
}
