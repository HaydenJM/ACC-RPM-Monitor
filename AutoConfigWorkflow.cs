namespace ACCRPMMonitor;

// Handles the auto configuration data collection workflow
public static class AutoConfigWorkflow
{
    public static void Run(ConfigManager configManager)
    {
        Console.Clear();
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine("AUTO CONFIGURATION - DATA COLLECTION");
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine();
        Console.WriteLine($"Vehicle: {configManager.CurrentVehicleName}");
        Console.WriteLine();
        Console.WriteLine("Instructions:");
        Console.WriteLine("  1. Load Monza or Paul Ricard in Assetto Corsa Competizione");
        Console.WriteLine("  2. Start a practice or hotlap session");
        Console.WriteLine("  3. Press F1 to START data collection");
        Console.WriteLine("  4. Drive a hotlap, making sure to redline gears 1-5 under full throttle");
        Console.WriteLine("  5. Press F1 to STOP data collection when done");
        Console.WriteLine("  6. The app will analyze the data and show results");
        Console.WriteLine();
        Console.WriteLine("Press any key to begin...");
        Console.ReadKey();

        var shiftAnalyzer = new OptimalShiftAnalyzer();
        using var accMemory = new ACCSharedMemorySimple();

        Console.Clear();
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine("AUTO CONFIGURATION - DATA COLLECTION");
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine($"Vehicle: {configManager.CurrentVehicleName}");
        Console.WriteLine();
        Console.WriteLine("Press F1 to START/STOP collection | Press ESC to abort\n");

        bool collectingData = false;
        bool wasConnected = false;
        int readFailCount = 0;

        Console.WriteLine("Waiting for Assetto Corsa Competizione...");

        while (true)
        {
            // Check for controls
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true).Key;
                if (key == ConsoleKey.Escape)
                {
                    Console.WriteLine("\nData collection aborted by user.");
                    Thread.Sleep(2000);
                    return;
                }
                else if (key == ConsoleKey.F1)
                {
                    if (!collectingData)
                    {
                        // Start collection
                        collectingData = true;
                        shiftAnalyzer.Clear(); // Clear any previous data
                        Console.SetCursorPosition(0, 7);
                        Console.WriteLine("Data Collection: ACTIVE - Drive your hotlap now!                    ");
                    }
                    else
                    {
                        // Stop collection and analyze
                        collectingData = false;
                        Console.SetCursorPosition(0, 7);
                        Console.WriteLine("Data Collection: STOPPED - Analyzing data...                        ");
                        Thread.Sleep(1000);

                        // Analyze and show results
                        bool success = AnalyzeAndSaveData(shiftAnalyzer, configManager);

                        if (success)
                        {
                            Console.WriteLine("\nAuto configuration created successfully!");
                            Console.WriteLine("Press any key to return to main menu...");
                            Console.ReadKey();
                            return;
                        }
                        else
                        {
                            Console.WriteLine("\nWould you like to collect more data? (Y/N): ");
                            var response = Console.ReadKey(true).Key;
                            if (response == ConsoleKey.Y)
                            {
                                // Continue collecting - don't clear existing data
                                Console.Clear();
                                Console.WriteLine("=".PadRight(80, '='));
                                Console.WriteLine("AUTO CONFIGURATION - DATA COLLECTION (CONTINUED)");
                                Console.WriteLine("=".PadRight(80, '='));
                                Console.WriteLine($"Vehicle: {configManager.CurrentVehicleName}");
                                Console.WriteLine();
                                Console.WriteLine("Press F1 to START/STOP collection | Press ESC to abort\n");
                                Console.WriteLine("Waiting for ACC...");
                            }
                            else
                            {
                                return;
                            }
                        }
                    }
                }
            }

            // Try connecting to ACC
            if (!accMemory.IsConnected)
            {
                if (accMemory.Connect())
                {
                    Console.SetCursorPosition(0, 7);
                    Console.WriteLine("ACC Status: Connected                                                ");
                    wasConnected = true;
                    readFailCount = 0;
                }
                else
                {
                    if (wasConnected)
                    {
                        Console.SetCursorPosition(0, 7);
                        Console.WriteLine("ACC Status: Connection lost. Waiting...                              ");
                        wasConnected = false;
                    }
                    Thread.Sleep(1000);
                    continue;
                }
            }

            // Read telemetry
            var gearRpmData = accMemory.ReadGearAndRPM();
            var status = accMemory.ReadStatus();

            if (gearRpmData == null || status == null)
            {
                readFailCount++;
                if (readFailCount > 10)
                {
                    accMemory.Dispose();
                    readFailCount = 0;
                }
                Thread.Sleep(100);
                continue;
            }

            readFailCount = 0;
            var (currentGear, currentRPM) = gearRpmData.Value;
            bool isDriving = status == 2; // AC_LIVE

            // Display status
            Console.SetCursorPosition(0, 7);
            Console.WriteLine($"ACC Status:       {GetStatusName(status.Value)}                          ");
            Console.WriteLine($"Data Collection:  {(collectingData ? "ACTIVE  " : "STOPPED ")} | Points: {shiftAnalyzer.GetDataPointCount()}    ");
            Console.WriteLine();

            if (!isDriving || currentGear <= 1)
            {
                Console.WriteLine($"Current Gear:     {(currentGear <= 1 ? "N/R" : (currentGear - 1).ToString())}                ");
                Console.WriteLine($"Current RPM:      {currentRPM}                                        ");
                Thread.Sleep(50);
                continue;
            }

            int displayGear = currentGear - 1;

            // Collect data if enabled and driving (only gears 1-5)
            if (collectingData && isDriving && currentGear >= 2 && displayGear < 6)
            {
                // Estimate throttle from RPM rate (simplified - ideally read from physics memory)
                // For now, assume full throttle if RPM is increasing quickly
                float estimatedThrottle = 1.0f; // Simplified assumption
                float estimatedSpeed = currentRPM / 100f; // Simplified - use actual speed if available

                shiftAnalyzer.AddDataPoint(currentRPM, estimatedThrottle, estimatedSpeed, displayGear);
            }

            // Display current telemetry
            Console.WriteLine($"Current Gear:     {displayGear}                                          ");
            Console.WriteLine($"Current RPM:      {currentRPM}                                           ");
            Console.WriteLine($"Status:           {(collectingData ? "Collecting data..." : "Press F1 to start")}              ");

            Thread.Sleep(50);
        }
    }

    private static bool AnalyzeAndSaveData(OptimalShiftAnalyzer analyzer, ConfigManager configManager)
    {
        Console.Clear();
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine("DATA ANALYSIS RESULTS");
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine();

        // Generate detailed report
        var report = analyzer.GenerateDetailedReport(configManager.CurrentVehicleName);

        // Save reports
        string reportsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ACCRPMMonitor",
            "reports"
        );
        report.SaveToFile(reportsDir);
        report.SaveHumanReadableReport(reportsDir);

        // Display summary
        Console.WriteLine(report.SessionSummary);
        Console.WriteLine();
        Console.WriteLine($"Total data points collected: {report.TotalDataPoints}");
        Console.WriteLine();
        Console.WriteLine("Per-Gear Results:");
        Console.WriteLine();

        foreach (var gear in report.GearAnalyses.OrderBy(g => g.Gear))
        {
            string status = gear.PassedConfidenceThreshold ? "✓ PASS" : "✗ FAIL";
            string shiftPoint = gear.OptimalShiftRPM.HasValue ? $"{gear.OptimalShiftRPM} RPM" : "NOT DETECTED";

            Console.WriteLine($"  Gear {gear.Gear}: {status,-8} | Shift at: {shiftPoint,-15} | Confidence: {gear.ConfidenceScore:F2}");
        }

        Console.WriteLine();

        if (report.OverallSuccess)
        {
            Console.WriteLine("SUCCESS! Auto configuration will be saved.");

            // Save the auto config
            var optimalConfig = analyzer.GenerateOptimalConfig();
            if (optimalConfig != null)
            {
                var autoConfig = GearRPMConfig.FromOptimalConfig(optimalConfig);
                configManager.SaveAutoConfig(autoConfig);
            }

            return true;
        }
        else
        {
            Console.WriteLine("Data collection incomplete. Recommendations:");
            foreach (var rec in report.Recommendations)
            {
                Console.WriteLine($"  • {rec}");
            }
            Console.WriteLine();
            Console.WriteLine($"Detailed reports saved to: {reportsDir}");
            Console.WriteLine();

            return false;
        }
    }

    private static string GetStatusName(int status)
    {
        return status switch
        {
            0 => "OFF",
            1 => "REPLAY",
            2 => "LIVE",
            3 => "PAUSE",
            _ => $"UNKNOWN ({status})"
        };
    }
}
