namespace ACCRPMMonitor;

// Handles the auto configuration data collection workflow
public static class AutoConfigFlow
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
        Console.WriteLine("  4. RECOMMENDED: Do 3-4 full acceleration runs:");
        Console.WriteLine("     - Start from slow corner or pit exit");
        Console.WriteLine("     - Full throttle through gears 1→2→3→4→5");
        Console.WriteLine("     - Let each gear reach high RPM before shifting");
        Console.WriteLine("     - This captures acceleration curves for optimal shift points");
        Console.WriteLine("  5. ALTERNATIVE: Drive 2-3 fast laps with good straight acceleration");
        Console.WriteLine("  6. Press F1 to STOP data collection when done");
        Console.WriteLine("  7. The app will analyze the data and show results");
        Console.WriteLine();
        Console.WriteLine("Press any key to begin...");
        Console.ReadKey();

        var shiftAnalyzer = new OptimalShift();
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
        bool continueSession = false; // Track if we're continuing after insufficient data

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

                        // Only clear data if starting fresh (not continuing from previous attempt)
                        if (!continueSession)
                        {
                            shiftAnalyzer.Clear();
                        }
                        continueSession = false; // Reset flag after checking

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
                                continueSession = true; // Set flag to preserve data on next F1 press
                                Console.Clear();
                                Console.WriteLine("=".PadRight(80, '='));
                                Console.WriteLine("AUTO CONFIGURATION - DATA COLLECTION (CONTINUED)");
                                Console.WriteLine("=".PadRight(80, '='));
                                Console.WriteLine($"Vehicle: {configManager.CurrentVehicleName}");
                                Console.WriteLine();
                                Console.WriteLine("Press F1 to START/STOP collection | Press ESC to abort\n");
                                Console.WriteLine($"Existing data preserved: {shiftAnalyzer.GetDataPointCount()} points");
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
            var telemetryData = accMemory.ReadFullTelemetry();
            var status = accMemory.ReadStatus();

            if (telemetryData == null || status == null)
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
            var (currentGear, currentRPM, throttle, speed) = telemetryData.Value;
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

            // Collect data if enabled and driving (only gears 1-6)
            if (collectingData && isDriving && displayGear >= 1 && displayGear <= 6)
            {
                // Use actual throttle and speed from telemetry
                // AddDataPoint will filter out invalid data (throttle < 85% or speed <= 5 km/h)
                shiftAnalyzer.AddDataPoint(currentRPM, throttle, speed, displayGear);
            }

            // Display current telemetry with diagnostics
            bool isCollectingNow = (collectingData && throttle >= 0.85f && speed > 5f && displayGear >= 1 && displayGear <= 6);
            string collectStatus;

            if (!collectingData)
            {
                collectStatus = "Press F1 to start";
            }
            else if (isCollectingNow)
            {
                collectStatus = $"✓ Collecting data - Throttle: {throttle*100:F0}%, Speed: {speed:F1} km/h";
            }
            else if (displayGear < 1 || displayGear > 6)
            {
                collectStatus = $"Not collecting (gear {displayGear} - need gears 1-6)";
            }
            else if (throttle < 0.85f)
            {
                collectStatus = $"Need 85%+ throttle (currently {throttle*100:F0}%)";
            }
            else if (speed <= 5f)
            {
                collectStatus = $"Speed too low ({speed:F1} km/h)";
            }
            else
            {
                collectStatus = "Waiting for valid data...";
            }

            Console.WriteLine($"Current Gear:     {displayGear}                                          ");
            Console.WriteLine($"Current RPM:      {currentRPM}                                           ");
            Console.WriteLine($"Throttle:         {throttle * 100:F1}%                                    ");
            Console.WriteLine($"Speed:            {speed:F1} km/h                                         ");

            // Show per-gear data collection progress
            string gearDataStatus = $"G1:{shiftAnalyzer.GetDataPointCountForGear(1)} " +
                                   $"G2:{shiftAnalyzer.GetDataPointCountForGear(2)} " +
                                   $"G3:{shiftAnalyzer.GetDataPointCountForGear(3)} " +
                                   $"G4:{shiftAnalyzer.GetDataPointCountForGear(4)} " +
                                   $"G5:{shiftAnalyzer.GetDataPointCountForGear(5)} " +
                                   $"G6:{shiftAnalyzer.GetDataPointCountForGear(6)}";
            Console.WriteLine($"Data Points:      {gearDataStatus} (Total: {shiftAnalyzer.GetDataPointCount()})");
            Console.WriteLine($"Status:           {collectStatus}                                        ");

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

        // Save reports to vehicle-specific directory
        string reportsDir = configManager.GetVehicleDataDirectory();
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

                // Generate power curve graph
                try
                {
                    Console.WriteLine();
                    Console.WriteLine("Generating power curve graph...");
                    string graphPath = PwrCrvGraphGen.GenerateGraph(
                        autoConfig,
                        configManager.CurrentVehicleName,
                        reportsDir
                    );
                    Console.WriteLine($"Power curve graph saved to: {graphPath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Could not generate power curve graph: {ex.Message}");
                }
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
