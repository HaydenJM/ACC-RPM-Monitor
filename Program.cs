using ACCRPMMonitor;
using System.Runtime.InteropServices;

try
{
// Set console window size to fixed dimensions (82x60)
// Buffer size matches window size to prevent scrolling and resizing
try
{
    const int width = 82;
    const int height = 60;

    Console.SetWindowSize(width, height);
    Console.SetBufferSize(width, height);
}
catch (Exception)
{
    // Ignore errors if console size cannot be set (e.g., when running in some terminals)
}

// Initialize vehicle detector to read car and track info from ACC
var vehicleDetector = new VehicleDetector();

// Try to detect current vehicle and track from ACC
string? detectedVehicle = null;
string? detectedTrack = null;

Console.WriteLine("Detecting vehicle and track from ACC...");
if (vehicleDetector.Connect())
{
    detectedVehicle = vehicleDetector.GetCarModel();
    detectedTrack = vehicleDetector.GetTrackName();

    if (!string.IsNullOrEmpty(detectedVehicle))
        Console.WriteLine($"Detected vehicle: {detectedVehicle}");

    if (!string.IsNullOrEmpty(detectedTrack))
        Console.WriteLine($"Detected track: {detectedTrack}");

    vehicleDetector.Dispose();
}

// Initialize config manager with detected vehicle and track
var configManager = new ConfigManager(
    vehicleName: detectedVehicle ?? "default",
    trackName: detectedTrack ?? "default"
);

// Initialize telemetry server (can be toggled from menu)
TelemetryServer? telemetryServer = null;

// Initialize game state monitor - detects pause events for seamless config switching
var gameStateMonitor = new GameStateMonitor();
bool vehicleChanged = false;
string? newVehicleName = null;
bool trackChanged = false;
string? newTrackName = null;

// Pause-based vehicle/track detection (most reliable method)
gameStateMonitor.VehicleOrTrackChanged += (sender, e) =>
{
    if (e.VehicleChanged && e.NewVehicle != null)
    {
        vehicleChanged = true;
        newVehicleName = e.NewVehicle;
        Console.WriteLine($"\n[PAUSE DETECTED] Vehicle changed: {e.OldVehicle} → {e.NewVehicle}");
        Console.WriteLine("Config will update on next menu...");
    }

    if (e.TrackChanged && e.NewTrack != null)
    {
        trackChanged = true;
        newTrackName = e.NewTrack;
        configManager.SetTrack(e.NewTrack);
        Console.WriteLine($"\n[PAUSE DETECTED] Track changed: {e.OldTrack} → {e.NewTrack}");
        Console.WriteLine($"Updated directory: {configManager.GetVehicleDataDirectory()}");
    }
};

// Start game state monitoring
gameStateMonitor.Start();

// Also use continuous monitor as fallback (for when not pausing)
var vehicleMonitor = new VehicleChangeMonitor();

vehicleMonitor.VehicleChanged += (sender, e) =>
{
    vehicleChanged = true;
    newVehicleName = e.NewVehicle;
    Console.SetCursorPosition(0, Console.CursorTop);
    Console.WriteLine($"\nVEHICLE CHANGED: {e.NewVehicle}");
    Console.WriteLine("Returning to menu...");
};

vehicleMonitor.TrackChanged += (sender, e) =>
{
    trackChanged = true;
    newTrackName = e.NewTrack;
    configManager.SetTrack(e.NewTrack);
    Console.SetCursorPosition(0, Console.CursorTop);
    Console.WriteLine($"\nTRACK CHANGED: {e.NewTrack}");
    Console.WriteLine($"Directory: {configManager.GetVehicleDataDirectory()}");
};

// Start continuous monitoring
if (!string.IsNullOrEmpty(detectedVehicle))
{
    vehicleMonitor.Start(detectedVehicle, detectedTrack);
}

// Check if detected vehicle has a config
if (!string.IsNullOrEmpty(detectedVehicle))
{
    configManager.SetVehicle(detectedVehicle);

    var availableVehicles = configManager.GetAvailableVehicles();
    if (!availableVehicles.Contains(detectedVehicle))
    {
        Console.WriteLine($"\nDetected vehicle '{detectedVehicle}' has no configuration yet.");
        Console.WriteLine();
        Console.Write("Would you like to create a configuration for this vehicle? (Y/N): ");

        var response = Console.ReadKey().KeyChar;
        Console.WriteLine();

        if (response == 'Y' || response == 'y')
        {
            Console.Write("Enter track name: ");
            string? trackName = Console.ReadLine()?.Trim();

            if (!string.IsNullOrWhiteSpace(trackName))
            {
                // Clean up any invalid filename characters
                foreach (char c in Path.GetInvalidFileNameChars())
                {
                    trackName = trackName.Replace(c, '_');
                }

                configManager.SetTrack(trackName);
                Console.WriteLine($"\nCreating configuration for '{detectedVehicle}' at '{trackName}'...");

                var defaultConfig = ShiftPointConfig.CreateDefault();
                configManager.SaveConfig(defaultConfig);

                Console.WriteLine("Configuration created successfully!");
                Console.WriteLine("\nYou can now:");
                Console.WriteLine("  1. Create Auto Configuration (recommended) - Learn optimal shift points");
                Console.WriteLine("  2. Edit Manual Configuration - Set custom shift points");
                Console.WriteLine();
                Console.WriteLine("Press any key to continue to main menu...");
                Console.ReadKey();
            }
        }
        else
        {
            Console.WriteLine("\nContinuing with current vehicle. You can create a configuration later");
            Console.WriteLine("from the main menu.");
            Thread.Sleep(2000);
        }
    }
}

// If no vehicles exist, create a default one
if (configManager.GetAvailableVehicles().Count == 0)
{
    var defaultConfig = ShiftPointConfig.CreateDefault();
    configManager.SaveConfig(defaultConfig);
}

// Check if direct launch mode was specified via command-line
// Main application loop
bool exitApp = false;
while (!exitApp)
{
    // Check if vehicle changed - if so, go to vehicle selection
    if (vehicleChanged)
    {
        vehicleChanged = false;
        if (!string.IsNullOrEmpty(newVehicleName))
        {
            configManager.SetVehicle(newVehicleName);
            // Update monitor with new vehicle
            vehicleMonitor.Stop();
            vehicleMonitor.Start(newVehicleName, configManager.CurrentTrackName);
        }
        ConfigUI.ShowVehicleSelectionMenu(configManager);
        continue;
    }

    var menuChoice = ConfigUI.ShowMainMenu(configManager, telemetryServer != null && telemetryServer.IsRunning);

    switch (menuChoice)
    {
        case MainMenuChoice.CreateAutoConfig:
            AutoConfigurationFlow.Run(configManager);
            break;

        case MainMenuChoice.CreateManualConfig:
            configManager.SetMode(ConfigMode.Manual);
            var manualConfig = configManager.LoadConfig();
            ConfigUI.ShowConfigMenu(manualConfig, configManager);
            break;

        case MainMenuChoice.SelectAndUseConfig:
            ConfigUI.ShowModeSelectionMenu(configManager);
            var config = configManager.LoadConfig();
            if (configManager.CurrentMode == ConfigMode.Manual)
            {
                // Allow quick edits before starting
                ConfigUI.ShowConfigMenu(config, configManager);
                config = configManager.LoadConfig(); // Reload in case changes were made
            }
            RunMonitor(configManager, config, telemetryServer);
            break;

        case MainMenuChoice.ToggleTelemetry:
            telemetryServer = ToggleTelemetryServer(telemetryServer);
            break;

        case MainMenuChoice.ChangeVehicle:
            ConfigUI.ShowVehicleSelectionMenu(configManager);
            // Update vehicle monitor with new selection
            vehicleMonitor.Stop();
            vehicleMonitor.Start(configManager.CurrentVehicleName, configManager.CurrentTrackName);
            break;

        case MainMenuChoice.OpenConfigFolder:
            OpenConfigFolder();
            break;

        case MainMenuChoice.Help:
            ConfigUI.ShowHelpMenu();
            break;

        case MainMenuChoice.Exit:
            exitApp = true;
            break;
    }
}

// Clean up
gameStateMonitor?.Dispose();
vehicleMonitor?.Dispose();
telemetryServer?.Dispose();

Console.Clear();
Console.WriteLine("Goodbye!");
}
catch (Exception ex)
{
    Console.Clear();
    Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║                    APPLICATION ERROR                           ║");
    Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
    Console.WriteLine();
    Console.WriteLine($"Error: {ex.Message}");
    Console.WriteLine();
    Console.WriteLine("Stack Trace:");
    Console.WriteLine(ex.StackTrace);
    Console.WriteLine();
    Console.WriteLine("Troubleshooting:");
    Console.WriteLine("  1. Make sure you extracted all files from the archive");
    Console.WriteLine("  2. Check that you have the .NET 8.0 runtime installed");
    Console.WriteLine("  3. Make sure ACC is not running when first loading configs");
    Console.WriteLine("  4. Try running from a folder without special characters in path");
    Console.WriteLine();
    Console.WriteLine("Press any key to exit...");
    Console.ReadKey();
}

// Opens the data folder in File Explorer
static void OpenConfigFolder()
{
    // Use ./data directory next to the application
    string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
    string dataPath = Path.Combine(appDirectory, "data");

    // Ensure the directory exists
    Directory.CreateDirectory(dataPath);

    try
    {
        // Open the folder in File Explorer
        System.Diagnostics.Process.Start("explorer.exe", dataPath);

        Console.Clear();
        Console.WriteLine("=== Open Data Folder ===\n");
        Console.WriteLine($"Opening: {dataPath}\n");
        Console.WriteLine("Press any key to continue...");
        Console.ReadKey();
    }
    catch (Exception ex)
    {
        Console.Clear();
        Console.WriteLine("=== Open Data Folder ===\n");
        Console.WriteLine($"Error opening folder: {ex.Message}\n");
        Console.WriteLine($"Path: {dataPath}\n");
        Console.WriteLine("Press any key to continue...");
        Console.ReadKey();
    }
}

// Monitor mode - the actual RPM monitoring
static void RunMonitor(ConfigManager configManager, ShiftPointConfig config, TelemetryServer? telemetryServer)
{
    // Check if user wants adaptive mode
    Console.Clear();
    Console.WriteLine("=== ACC RPM Monitor ===");
    if (telemetryServer != null && telemetryServer.IsRunning)
    {
        Console.WriteLine("Telemetry: ✓ RUNNING (http://localhost:8501)");
    }
    Console.WriteLine();
    Console.WriteLine("Select monitoring mode:");
    Console.WriteLine("  1. Standard Mode - Use fixed shift points");
    Console.WriteLine("  2. Adaptive Mode - Continuously learn and update shift points");
    Console.WriteLine("  3. Performance Learning Mode - Machine learning-based shift optimization using lap time correlation");
    Console.WriteLine();
    Console.Write("Choice (1-3): ");

    var choice = Console.ReadLine()?.Trim();

    if (choice == "3")
    {
        RunPerformanceLearningMonitor(configManager, config, telemetryServer);
    }
    else if (choice == "2")
    {
        RunAdaptiveMonitor(configManager, config, telemetryServer);
    }
    else
    {
        RunStandardMonitor(configManager, config, telemetryServer);
    }
}

// Standard monitor mode with fixed shift points
static void RunStandardMonitor(ConfigManager configManager, ShiftPointConfig config, TelemetryServer? telemetryServer)
{
    // Initialize dynamic audio engine
    using var audioEngine = new AudioEngine();

    // Initialize ACC shared memory
    using var accMemory = new SharedMemoryReader();

    // Start telemetry server if provided
    if (telemetryServer != null && !telemetryServer.IsRunning)
    {
        if (telemetryServer.Start(accMemory))
        {
            Console.WriteLine("✓ Telemetry window opened");
            Thread.Sleep(1000);
        }
    }

    // Initialize vehicle detector for real-time vehicle changes
    using var vehicleDetector = new VehicleDetector();
    vehicleDetector.Connect();
    string currentVehicleName = configManager.CurrentVehicleName;

    // Initialize gear recommendation engine if auto-config available
    GearRecommendationEngine? gearRecommendation = null;
    if (config.IsAutoGenerated && config.AccelerationCurves != null && config.AccelerationCurves.Count > 0)
    {
        gearRecommendation = new GearRecommendationEngine(config.AccelerationCurves, config.GearRatios);
    }

    // Initialize monitoring data collector
    var dataCollector = new MonitoringDataCollector(
        configManager.CurrentVehicleName,
        configManager.CurrentTrackName,
        "Standard"
    );

    Console.Clear();
    Console.WriteLine("=== ACC RPM Monitor - Standard Mode ===");
    Console.WriteLine($"Vehicle: {configManager.CurrentVehicleName}");
    Console.WriteLine($"Mode: {configManager.CurrentMode}");
    if (telemetryServer != null && telemetryServer.IsRunning)
    {
        Console.WriteLine($"Telemetry: Streaming to http://localhost:{telemetryServer.Port}/telemetry");
    }
    Console.WriteLine("Press ESC to exit\n");

    // Main loop state
    bool wasConnected = false;
    int readFailCount = 0;

    // Tire pressure tracking for lap-before/lap-after display
    WheelAndTireData? lapStartTirePressure = null;
    WheelAndTireData? lapEndTirePressure = null;
    int lastCompletedLaps = 0;
    bool hasDisplayedLapPressures = false;

    // Track initial vehicle to detect changes during session
    string? initialVehicle = null;

    Console.WriteLine("Waiting for Assetto Corsa Competizione...");

    while (true)
    {
        // Check for vehicle change
        string? detectedVehicle = vehicleDetector.GetCarModel();

        // Set initial vehicle on first detection
        if (detectedVehicle != null && initialVehicle == null)
            initialVehicle = detectedVehicle;

        // Only exit if vehicle CHANGES during the session
        if (initialVehicle != null && detectedVehicle != null && detectedVehicle != initialVehicle)
        {
            Console.WriteLine($"\nVehicle changed: {detectedVehicle}");
            Console.WriteLine("Returning to main menu...");
            Thread.Sleep(2000);
            break;
        }

        // Check for exit
        if (Console.KeyAvailable)
        {
            var key = Console.ReadKey(true).Key;
            if (key == ConsoleKey.Escape)
                break;
        }

        // Try connecting to ACC if not already connected
        if (!accMemory.IsConnected)
        {
            if (accMemory.Connect())
            {
                Console.Clear();
                Console.WriteLine("=== ACC RPM Monitor - Running ===");
                Console.WriteLine($"Vehicle: {configManager.CurrentVehicleName}");
                Console.WriteLine($"Mode: {configManager.CurrentMode}");
                if (telemetryServer != null && telemetryServer.IsRunning)
                {
                    Console.WriteLine($"Telemetry: Streaming to Streamlit (http://localhost:{telemetryServer.Port})");
                }
                Console.WriteLine("Press ESC to exit\n");
                Console.WriteLine("Connected to ACC!");
                Console.WriteLine("Reading telemetry data...\n");
                wasConnected = true;
                readFailCount = 0;
            }
            else
            {
                if (wasConnected)
                {
                    Console.Clear();
                    Console.WriteLine("=== ACC RPM Monitor - Running ===");
                    Console.WriteLine($"Vehicle: {configManager.CurrentVehicleName}");
                    Console.WriteLine($"Mode: {configManager.CurrentMode}");
                    Console.WriteLine("Press ESC to exit\n");
                    Console.WriteLine("Connection lost. Waiting for ACC...");
                    wasConnected = false;
                    audioEngine.Stop();
                }
                Thread.Sleep(1000);
                continue;
            }
        }

        // Read telemetry
        var telemetryData = accMemory.ReadFullTelemetry();
        var status = accMemory.ReadStatus();
        var tireData = accMemory.ReadWheelAndTireData(); // Read tire pressures
        var lapTiming = accMemory.ReadLapTiming(); // Read lap timing for lap completion detection

        // Handle read failures
        if (telemetryData == null || status == null)
        {
            readFailCount++;
            Console.SetCursorPosition(0, 7);
            Console.WriteLine($"Read failures: {readFailCount}/10                                          ");
            if (accMemory.LastError != null)
            {
                Console.WriteLine($"Error: {accMemory.LastError}                                              ");
            }

            // Reconnect after too many failures
            if (readFailCount > 10)
            {
                Console.WriteLine("Multiple read failures. Reconnecting...                                  ");
                accMemory.Dispose();
                readFailCount = 0;
            }
            Thread.Sleep(100);
            continue;
        }

        readFailCount = 0;

        var (currentGear, currentRPM, throttle, speed) = telemetryData.Value;

        // Update telemetry server if enabled
        if (telemetryServer != null)
        {
            if (tireData != null)
            {
                // Read fuel from physics struct
                var physics = accMemory.ReadPhysicsStruct();
                float fuel = physics?.Fuel ?? 0f;
                telemetryServer.UpdateTelemetry(tireData, currentRPM, currentGear - 1, speed, fuel);

                // Update lap comparison data
                if (lapTiming != null)
                {
                    telemetryServer.UpdateLapData(lapTiming.CompletedLaps, tireData);
                }
            }
            else
            {
                // Show "No data" state when tireData is null
                telemetryServer.ShowNoData();
            }
        }

        // Detect lap completion and capture tire pressures
        if (lapTiming != null && lapTiming.CompletedLaps > lastCompletedLaps)
        {
            // Lap was completed - save the end tire pressure from previous frame
            if (tireData != null && lapEndTirePressure == null)
            {
                lapEndTirePressure = tireData;
            }

            // Prepare for next lap - current tire data becomes start of next lap
            if (tireData != null)
            {
                lapStartTirePressure = tireData;
            }
            hasDisplayedLapPressures = false;
            lastCompletedLaps = lapTiming.CompletedLaps;
        }
        else if (lastCompletedLaps == 0 && lapTiming != null && lapTiming.CompletedLaps >= 0)
        {
            // Initialize at start of session
            if (tireData != null)
            {
                lapStartTirePressure = tireData;
            }
            lastCompletedLaps = lapTiming.CompletedLaps;
        }

        // Only provide audio feedback when actually driving (not in menus/replay)
        bool isDriving = status == 2; // AC_LIVE

        if (!isDriving)
        {
            audioEngine.Stop();
            Console.SetCursorPosition(0, 7);
            Console.WriteLine($"ACC Status:   {GetStatusName(status.Value)}                              ");
            Console.WriteLine("Status:       Waiting for session...                                    ");
            Thread.Sleep(100);
            continue;
        }

        // Display status
        Console.SetCursorPosition(0, 7);
        Console.WriteLine($"ACC Status:   {GetStatusName(status.Value)}                              ");
        Console.WriteLine();

        // Ignore neutral and reverse (gear 0 and 1 in ACC)
        if (currentGear <= 1)
        {
            audioEngine.Stop();
            Console.WriteLine($"Current Gear: N/R                                                       ");
            Console.WriteLine($"Current RPM:  {currentRPM}                                              ");
            Console.WriteLine($"Status:       Neutral/Reverse                                           ");
            Thread.Sleep(50);
            continue;
        }

        // ACC uses gear 2 as first gear, so subtract 1 for display
        int displayGear = currentGear - 1;

        // Update monitoring data collector
        dataCollector.Update(displayGear, currentRPM, speed, tireData);

        int threshold = config.GetRPMForGear(displayGear);

        // Update audio with dynamic beeping timing based on RPM rate
        audioEngine.UpdateRPM(currentRPM, threshold, displayGear);

        // Display current telemetry
        Console.WriteLine($"Current Gear: {displayGear}                                                ");
        Console.WriteLine($"Current RPM:  {currentRPM}                                                 ");
        Console.WriteLine($"Threshold:    {threshold} RPM                                              ");
        Console.WriteLine($"RPM Rate:     {audioEngine.GetCurrentRPMRate():F0} RPM/sec                 ");
        Console.WriteLine($"Beep Dist:    {audioEngine.GetCurrentWarningDistance()} RPM                ");

        // Display gear recommendation if available
        if (gearRecommendation != null)
        {
            int? optimalGear = gearRecommendation.GetOptimalGearForSpeed(speed, throttle);
            if (optimalGear.HasValue)
            {
                string gearDisplay = optimalGear.Value == displayGear
                    ? $"✓ {displayGear} (optimal for sustained power)"
                    : $"{optimalGear.Value} (for sustained power, currently in {displayGear})";
                Console.WriteLine($"Optimal Gear: {gearDisplay}                                    ");
            }
            else
            {
                Console.WriteLine($"Optimal Gear: Not available (speed too low or no data)                     ");
            }
        }

        int rpmFromThreshold = currentRPM - threshold;
        int beepDist = audioEngine.GetCurrentWarningDistance();

        if (rpmFromThreshold >= -beepDist)
        {
            if (rpmFromThreshold >= 0)
            {
                Console.WriteLine($"Status:       SHIFT UP! ({rpmFromThreshold} RPM over threshold)                  ");
            }
            else
            {
                Console.WriteLine($"Status:       BEEPING ({Math.Abs(rpmFromThreshold)} RPM from threshold)           ");
            }
        }
        else
        {
            Console.WriteLine($"Status:       Normal ({Math.Abs(rpmFromThreshold)} RPM from threshold)              ");
        }

        // Show tire pressures before and after lap
        if ((lapStartTirePressure != null || lapEndTirePressure != null) && !hasDisplayedLapPressures && lapTiming != null)
        {
            Console.WriteLine();
            Console.WriteLine("─── TIRE PRESSURES (PSI) ──────────────────────────────────────");
            if (lapStartTirePressure != null)
            {
                Console.WriteLine($"Start of Lap {lapTiming.CompletedLaps}:");
                Console.WriteLine($"  FL: {lapStartTirePressure.WheelPressureFL:F2}  FR: {lapStartTirePressure.WheelPressureFR:F2}  RL: {lapStartTirePressure.WheelPressureRL:F2}  RR: {lapStartTirePressure.WheelPressureRR:F2}");
                Console.WriteLine($"  Avg: {lapStartTirePressure.AverageWheelPressure:F2} PSI");
            }
            if (lapEndTirePressure != null)
            {
                Console.WriteLine($"End of Lap {lapTiming.CompletedLaps - 1}:");
                Console.WriteLine($"  FL: {lapEndTirePressure.WheelPressureFL:F2}  FR: {lapEndTirePressure.WheelPressureFR:F2}  RL: {lapEndTirePressure.WheelPressureRL:F2}  RR: {lapEndTirePressure.WheelPressureRR:F2}");
                Console.WriteLine($"  Avg: {lapEndTirePressure.AverageWheelPressure:F2} PSI");
                if (lapStartTirePressure != null)
                {
                    float pressureDelta = lapStartTirePressure.AverageWheelPressure - lapEndTirePressure.AverageWheelPressure;
                    Console.WriteLine($"  Change: {(pressureDelta >= 0 ? "+" : "")}{pressureDelta:F2} PSI");
                }
            }
            hasDisplayedLapPressures = true;
        }
        // Display current tire pressures
        else if (tireData != null)
        {
            Console.WriteLine();
            Console.WriteLine($"Tire Pressures: FL: {tireData.WheelPressureFL:F2}  FR: {tireData.WheelPressureFR:F2}  RL: {tireData.WheelPressureRL:F2}  RR: {tireData.WheelPressureRR:F2} PSI");
            Console.WriteLine($"Avg: {tireData.AverageWheelPressure:F2} PSI (Front: {tireData.AverageFrontPressure:F2}  Rear: {tireData.AverageRearPressure:F2})");
        }

        Thread.Sleep(50); // ~20Hz update rate
    }

    audioEngine.Stop();

    // Generate and save monitoring session report
    Console.Clear();
    Console.WriteLine("Generating monitoring session report...");
    var report = dataCollector.GenerateReport();
    var reportGen = new ReportGen(configManager);
    string reportPath = reportGen.SaveMonitoringSessionReport(report);
    Console.WriteLine($"Report saved to: {reportPath}");
    Console.WriteLine("\nPress any key to continue...");
    Console.ReadKey();
}

// Adaptive monitor mode - continuously learns and updates shift points
static void RunAdaptiveMonitor(ConfigManager configManager, ShiftPointConfig config, TelemetryServer? telemetryServer)
{
    // Initialize dynamic audio engine
    using var audioEngine = new AudioEngine();

    // Initialize ACC shared memory
    using var accMemory = new SharedMemoryReader();

    // Start telemetry server if provided
    if (telemetryServer != null && !telemetryServer.IsRunning)
    {
        if (telemetryServer.Start(accMemory))
        {
            Console.WriteLine("✓ Telemetry window opened");
            Thread.Sleep(1000);
        }
    }

    // Initialize vehicle detector for real-time vehicle changes
    using var vehicleDetector = new VehicleDetector();
    vehicleDetector.Connect();
    string currentVehicleName = configManager.CurrentVehicleName;

    // Initialize shift analyzer for continuous learning
    var shiftAnalyzer = new OptimalShift();

    // Initialize gear recommendation engine if auto-config available
    GearRecommendationEngine? gearRecommendation = null;
    if (config.IsAutoGenerated && config.AccelerationCurves != null && config.AccelerationCurves.Count > 0)
    {
        gearRecommendation = new GearRecommendationEngine(config.AccelerationCurves, config.GearRatios);
    }

    // Initialize monitoring data collector
    var dataCollector = new MonitoringDataCollector(
        configManager.CurrentVehicleName,
        configManager.CurrentTrackName,
        "Adaptive"
    );

    Console.Clear();
    Console.WriteLine("=== ACC RPM Monitor - Adaptive Mode ===");
    Console.WriteLine($"Vehicle: {configManager.CurrentVehicleName}");
    Console.WriteLine($"Mode: {configManager.CurrentMode} (Adaptive)");
    if (telemetryServer != null && telemetryServer.IsRunning)
    {
        Console.WriteLine($"Telemetry: Streaming to Streamlit (http://localhost:{telemetryServer.Port})");
    }
    Console.WriteLine("Press ESC to exit | Press F2 to save learned config\n");

    // Audio profile selection
    var audioProfile = SelectAudioProfileWithPreview(audioEngine, AudioEngine.AudioMode.Standard);
    audioEngine.SetAudioProfile(audioProfile);
    // Use default audio mode (standard beeping) for adaptive mode
    Console.WriteLine();

    // Main loop state
    bool wasConnected = false;
    int readFailCount = 0;
    DateTime lastUpdate = DateTime.Now;
    const int UpdateIntervalSeconds = 15; // Update shift points every 15 seconds

    // Tire pressure tracking for lap-before/lap-after display
    WheelAndTireData? lapStartTirePressure = null;
    WheelAndTireData? lapEndTirePressure = null;
    int lastCompletedLaps = 0;
    bool hasDisplayedLapPressures = false;

    // Track initial vehicle to detect changes during session
    string? initialVehicle = null;

    Console.WriteLine("Waiting for Assetto Corsa Competizione...");

    while (true)
    {
        // Check for vehicle change
        string? detectedVehicle = vehicleDetector.GetCarModel();

        // Set initial vehicle on first detection
        if (detectedVehicle != null && initialVehicle == null)
            initialVehicle = detectedVehicle;

        // Only exit if vehicle CHANGES during the session
        if (initialVehicle != null && detectedVehicle != null && detectedVehicle != initialVehicle)
        {
            Console.WriteLine($"\nVehicle changed: {detectedVehicle}");
            Console.WriteLine("Returning to main menu...");
            Thread.Sleep(2000);
            break;
        }

        // Check for exit or save
        if (Console.KeyAvailable)
        {
            var key = Console.ReadKey(true).Key;
            if (key == ConsoleKey.Escape)
                break;
            else if (key == ConsoleKey.F2)
            {
                // Save the learned configuration and generate report
                var optimalConfig = shiftAnalyzer.GenerateOptimalConfig();
                if (optimalConfig != null)
                {
                    var adaptiveConfig = ShiftPointConfig.FromOptimalConfig(optimalConfig);
                    configManager.SaveAutoConfig(adaptiveConfig);

                    // Generate and save detailed report
                    var report = shiftAnalyzer.GenerateDetailedReport(configManager.CurrentVehicleName, adaptiveConfig.MaxGear);
                    var reportGen = new ReportGen(configManager);
                    reportGen.SaveAutoConfigReport(report);

                    Console.SetCursorPosition(0, 15);
                    Console.WriteLine("Learned configuration and report saved!                                 ");
                    Thread.Sleep(1000);
                }
            }
        }

        // Try connecting to ACC if not already connected
        if (!accMemory.IsConnected)
        {
            if (accMemory.Connect())
            {
                Console.Clear();
                Console.WriteLine("=== ACC RPM Monitor - Adaptive Mode ===");
                Console.WriteLine($"Vehicle: {configManager.CurrentVehicleName}");
                Console.WriteLine($"Mode: {configManager.CurrentMode} (Adaptive)");
                if (telemetryServer != null && telemetryServer.IsRunning)
                {
                    Console.WriteLine($"Telemetry: Streaming to Streamlit (http://localhost:{telemetryServer.Port})");
                }
                Console.WriteLine("Press ESC to exit | Press F2 to save learned config\n");
                Console.WriteLine("Connected to ACC!");
                Console.WriteLine("Learning optimal shift points...\n");
                wasConnected = true;
                readFailCount = 0;
            }
            else
            {
                if (wasConnected)
                {
                    Console.Clear();
                    Console.WriteLine("=== ACC RPM Monitor - Adaptive Mode ===");
                    Console.WriteLine($"Vehicle: {configManager.CurrentVehicleName}");
                    Console.WriteLine($"Mode: {configManager.CurrentMode} (Adaptive)");
                    Console.WriteLine("Press ESC to exit | Press F2 to save learned config\n");
                    Console.WriteLine("Connection lost. Waiting for ACC...");
                    wasConnected = false;
                    audioEngine.Stop();
                }
                Thread.Sleep(1000);
                continue;
            }
        }

        // Read telemetry with full data (throttle and speed)
        var telemetryData = accMemory.ReadFullTelemetry();
        var status = accMemory.ReadStatus();
        var tireData = accMemory.ReadWheelAndTireData(); // Read tire pressures
        var lapTiming = accMemory.ReadLapTiming(); // Read lap timing for lap completion detection

        // Handle read failures
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

        // Detect lap completion and capture tire pressures
        if (lapTiming != null && lapTiming.CompletedLaps > lastCompletedLaps)
        {
            // Lap was completed - save the end tire pressure from previous frame
            if (tireData != null && lapEndTirePressure == null)
            {
                lapEndTirePressure = tireData;
            }

            // Prepare for next lap - current tire data becomes start of next lap
            if (tireData != null)
            {
                lapStartTirePressure = tireData;
            }
            hasDisplayedLapPressures = false;
            lastCompletedLaps = lapTiming.CompletedLaps;
        }
        else if (lastCompletedLaps == 0 && lapTiming != null && lapTiming.CompletedLaps >= 0)
        {
            // Initialize at start of session
            if (tireData != null)
            {
                lapStartTirePressure = tireData;
            }
            lastCompletedLaps = lapTiming.CompletedLaps;
        }

        // Only provide audio feedback when actually driving (not in menus/replay)
        bool isDriving = status == 2; // AC_LIVE

        if (!isDriving)
        {
            audioEngine.Stop();
            Console.SetCursorPosition(0, 7);
            Console.WriteLine($"ACC Status:   {GetStatusName(status.Value)}                              ");
            Console.WriteLine("Status:       Waiting for session...                                    ");
            Thread.Sleep(100);
            continue;
        }

        // Display status
        Console.SetCursorPosition(0, 7);
        Console.WriteLine($"ACC Status:   {GetStatusName(status.Value)}                              ");
        Console.WriteLine();

        // Ignore neutral and reverse (gear 0 and 1 in ACC)
        if (currentGear <= 1)
        {
            audioEngine.Stop();
            Console.WriteLine($"Current Gear: N/R                                                       ");
            Console.WriteLine($"Current RPM:  {currentRPM}                                              ");
            Console.WriteLine($"Status:       Neutral/Reverse                                           ");
            Thread.Sleep(50);
            continue;
        }

        // Update telemetry server if enabled
        if (telemetryServer != null)
        {
            if (tireData != null)
            {
                // Read fuel from physics struct
                var physics = accMemory.ReadPhysicsStruct();
                float fuel = physics?.Fuel ?? 0f;
                telemetryServer.UpdateTelemetry(tireData, currentRPM, currentGear - 1, speed, fuel);

                // Update lap comparison data
                if (lapTiming != null)
                {
                    telemetryServer.UpdateLapData(lapTiming.CompletedLaps, tireData);
                }
            }
            else
            {
                // Show "No data" state when tireData is null
                telemetryServer.ShowNoData();
            }
        }

        // ACC uses gear 2 as first gear, so subtract 1 for display
        int displayGear = currentGear - 1;

        // Update monitoring data collector
        dataCollector.Update(displayGear, currentRPM, speed, tireData);

        // Continuously collect data for gears 1-6 when at full throttle
        if (displayGear >= 1 && displayGear <= 6)
        {
            shiftAnalyzer.AddDataPoint(currentRPM, throttle, speed, displayGear);
        }

        // Periodically update shift points based on collected data
        if ((DateTime.Now - lastUpdate).TotalSeconds >= UpdateIntervalSeconds)
        {
            var optimalConfig = shiftAnalyzer.GenerateOptimalConfig();
            if (optimalConfig != null)
            {
                // Update the config with newly calculated optimal shift points
                foreach (var kvp in optimalConfig.OptimalUpshiftRPM)
                {
                    config.SetRPMForGear(kvp.Key, kvp.Value);
                }
            }
            lastUpdate = DateTime.Now;
        }

        int threshold = config.GetRPMForGear(displayGear);

        // Update audio with dynamic beeping timing based on RPM rate
        audioEngine.UpdateRPM(currentRPM, threshold, displayGear);

        // Display current telemetry
        Console.WriteLine($"Current Gear: {displayGear}                                                ");
        Console.WriteLine($"Current RPM:  {currentRPM}                                                 ");
        Console.WriteLine($"Threshold:    {threshold} RPM                                              ");
        Console.WriteLine($"Throttle:     {throttle * 100:F1}%                                         ");
        Console.WriteLine($"Speed:        {speed:F1} km/h                                              ");

        // Show per-gear data collection progress
        string gearDataStatus = $"G1:{shiftAnalyzer.GetDataPointCountForGear(1)} " +
                               $"G2:{shiftAnalyzer.GetDataPointCountForGear(2)} " +
                               $"G3:{shiftAnalyzer.GetDataPointCountForGear(3)} " +
                               $"G4:{shiftAnalyzer.GetDataPointCountForGear(4)} " +
                               $"G5:{shiftAnalyzer.GetDataPointCountForGear(5)} " +
                               $"G6:{shiftAnalyzer.GetDataPointCountForGear(6)}";
        Console.WriteLine($"Data Points:  {gearDataStatus} (Total: {shiftAnalyzer.GetDataPointCount()}) ");
        Console.WriteLine($"RPM Rate:     {audioEngine.GetCurrentRPMRate():F0} RPM/sec                 ");
        Console.WriteLine($"Beep Dist:    {audioEngine.GetCurrentWarningDistance()} RPM                ");

        // Display gear recommendation if available
        if (gearRecommendation != null)
        {
            int? optimalGear = gearRecommendation.GetOptimalGearForSpeed(speed, throttle);
            if (optimalGear.HasValue)
            {
                string gearDisplay = optimalGear.Value == displayGear
                    ? $"✓ {displayGear} (optimal for sustained power)"
                    : $"{optimalGear.Value} (for sustained power, currently in {displayGear})";
                Console.WriteLine($"Optimal Gear: {gearDisplay}                                    ");
            }
            else
            {
                Console.WriteLine($"Optimal Gear: Not available (speed too low or no data)                     ");
            }
        }

        int rpmFromThreshold = currentRPM - threshold;
        int beepDist = audioEngine.GetCurrentWarningDistance();

        if (rpmFromThreshold >= -beepDist)
        {
            if (rpmFromThreshold >= 0)
            {
                Console.WriteLine($"Status:       SHIFT UP! ({rpmFromThreshold} RPM over threshold)                  ");
            }
            else
            {
                Console.WriteLine($"Status:       BEEPING ({Math.Abs(rpmFromThreshold)} RPM from threshold)           ");
            }
        }
        else
        {
            // Show if we're currently collecting data and why if not
            bool isCollecting = (throttle >= 0.85f && speed > 5f && displayGear >= 1 && displayGear <= 6);
            string dataStatus;

            if (isCollecting)
            {
                dataStatus = "✓ Collecting data";
            }
            else if (displayGear < 1 || displayGear > 6)
            {
                dataStatus = $"Not collecting (gear {displayGear})";
            }
            else if (throttle < 0.85f)
            {
                dataStatus = $"Need 85%+ throttle (currently {throttle*100:F0}%)";
            }
            else if (speed <= 5f)
            {
                dataStatus = $"Speed too low ({speed:F1} km/h)";
            }
            else
            {
                dataStatus = "Not collecting (unknown reason)";
            }

            Console.WriteLine($"Status:       {dataStatus} ({Math.Abs(rpmFromThreshold)} RPM from threshold)          ");
        }

        // Show tire pressures before and after lap
        if ((lapStartTirePressure != null || lapEndTirePressure != null) && !hasDisplayedLapPressures && lapTiming != null)
        {
            Console.WriteLine();
            Console.WriteLine("─── TIRE PRESSURES (PSI) ──────────────────────────────────────");
            if (lapStartTirePressure != null)
            {
                Console.WriteLine($"Start of Lap {lapTiming.CompletedLaps}:");
                Console.WriteLine($"  FL: {lapStartTirePressure.WheelPressureFL:F2}  FR: {lapStartTirePressure.WheelPressureFR:F2}  RL: {lapStartTirePressure.WheelPressureRL:F2}  RR: {lapStartTirePressure.WheelPressureRR:F2}");
                Console.WriteLine($"  Avg: {lapStartTirePressure.AverageWheelPressure:F2} PSI");
            }
            if (lapEndTirePressure != null)
            {
                Console.WriteLine($"End of Lap {lapTiming.CompletedLaps - 1}:");
                Console.WriteLine($"  FL: {lapEndTirePressure.WheelPressureFL:F2}  FR: {lapEndTirePressure.WheelPressureFR:F2}  RL: {lapEndTirePressure.WheelPressureRL:F2}  RR: {lapEndTirePressure.WheelPressureRR:F2}");
                Console.WriteLine($"  Avg: {lapEndTirePressure.AverageWheelPressure:F2} PSI");
                if (lapStartTirePressure != null)
                {
                    float pressureDelta = lapStartTirePressure.AverageWheelPressure - lapEndTirePressure.AverageWheelPressure;
                    Console.WriteLine($"  Change: {(pressureDelta >= 0 ? "+" : "")}{pressureDelta:F2} PSI");
                }
            }
            hasDisplayedLapPressures = true;
        }
        // Display current tire pressures
        else if (tireData != null)
        {
            Console.WriteLine();
            Console.WriteLine($"Tire Pressures: FL: {tireData.WheelPressureFL:F2}  FR: {tireData.WheelPressureFR:F2}  RL: {tireData.WheelPressureRL:F2}  RR: {tireData.WheelPressureRR:F2} PSI");
            Console.WriteLine($"Avg: {tireData.AverageWheelPressure:F2} PSI (Front: {tireData.AverageFrontPressure:F2}  Rear: {tireData.AverageRearPressure:F2})");
        }

        Thread.Sleep(50); // ~20Hz update rate
    }

    audioEngine.Stop();

    // Generate and save monitoring session report
    Console.Clear();
    Console.WriteLine("Generating monitoring session report...");
    var monitoringReport = dataCollector.GenerateReport();
    var monitoringReportGen = new ReportGen(configManager);
    string monitoringReportPath = monitoringReportGen.SaveMonitoringSessionReport(monitoringReport);
    Console.WriteLine($"Monitoring report saved to: {monitoringReportPath}");
    Console.WriteLine();

    // Ask if user wants to save learned configuration
    Console.WriteLine("=== Adaptive Mode Session Ended ===");
    Console.WriteLine();
    Console.WriteLine($"Total data points collected: {shiftAnalyzer.GetDataPointCount()}");
    Console.WriteLine();
    Console.Write("Save learned configuration? (Y/N): ");
    var saveChoice = Console.ReadKey().KeyChar;
    Console.WriteLine();
    if (saveChoice == 'Y' || saveChoice == 'y')
    {
        var optimalConfig = shiftAnalyzer.GenerateOptimalConfig();
        if (optimalConfig != null)
        {
            var adaptiveConfig = ShiftPointConfig.FromOptimalConfig(optimalConfig);
            configManager.SaveAutoConfig(adaptiveConfig);
            Console.WriteLine("\n\nConfiguration saved successfully!");

            // Generate and save detailed report
            Console.WriteLine("Generating adaptive learning report...");
            var report = shiftAnalyzer.GenerateDetailedReport(configManager.CurrentVehicleName, adaptiveConfig.MaxGear);
            var reportGen = new ReportGen(configManager);
            reportGen.SaveAutoConfigReport(report);
            Console.WriteLine("Report saved!");

            // Generate power curve graph (without user shift points since adaptive mode doesn't track them)
            if (adaptiveConfig.IsAutoGenerated && adaptiveConfig.AccelerationCurves.Count > 0)
            {
                Console.WriteLine("Generating power curve graph...");
                string vehicleDir = configManager.GetVehicleDataDirectory();
                string graphPath = PowerCurveGraph.GenerateGraph(adaptiveConfig, configManager.CurrentVehicleName,
                                                               vehicleDir, null);
                Console.WriteLine($"Power curve graph saved to:");
                Console.WriteLine($"  {graphPath}");
            }
        }
        else
        {
            Console.WriteLine("\n\nNot enough data collected to generate configuration.");
        }
        Thread.Sleep(2000);
    }
}

// Performance Learning monitor mode - AI-driven shift optimization based on lap times
static void RunPerformanceLearningMonitor(ConfigManager configManager, ShiftPointConfig config, TelemetryServer? telemetryServer)
{
    // Initialize all required engines
    using var audioEngine = new AudioEngine();
    audioEngine.SetMode(AudioEngine.AudioMode.PerformanceLearning); // Use pitch-based guidance

    using var accMemory = new SharedMemoryReader();

    // Start telemetry server if provided
    if (telemetryServer != null && !telemetryServer.IsRunning)
    {
        if (telemetryServer.Start(accMemory))
        {
            Console.WriteLine("✓ Telemetry window opened");
            Thread.Sleep(1000);
        }
    }

    // Initialize vehicle detector for real-time vehicle changes
    using var vehicleDetector = new VehicleDetector();
    vehicleDetector.Connect();
    string currentVehicleName = configManager.CurrentVehicleName;

    // Audio profile will be selected after mode description

    var shiftAnalyzer = new OptimalShift(); // For physics-based analysis
    var shiftPatternAnalyzer = new PatternShift(); // For shift detection
    var learningEngine = new PerformanceEng(shiftPatternAnalyzer, shiftAnalyzer);

    // Set max gear to prevent shift points from non-existent gears
    learningEngine.SetMaxGear(config.MaxGear);
    shiftPatternAnalyzer.SetMaxGear(config.MaxGear);

    var reportGenerator = new ReportGen(configManager);

    // Initialize gear recommendation engine if available
    GearRecommendationEngine? gearRecommendation = null;
    if (config.IsAutoGenerated && config.AccelerationCurves != null && config.AccelerationCurves.Count > 0)
    {
        gearRecommendation = new GearRecommendationEngine(config.AccelerationCurves, config.GearRatios);
    }

    // Initialize monitoring data collector
    var dataCollector = new MonitoringDataCollector(
        configManager.CurrentVehicleName,
        configManager.CurrentTrackName,
        "Performance Learning"
    );

    Console.Clear();
    Console.WriteLine("=== ACC RPM Monitor - Performance Learning Mode ===");
    Console.WriteLine($"Vehicle: {configManager.CurrentVehicleName}");
    if (telemetryServer != null && telemetryServer.IsRunning)
    {
        Console.WriteLine($"Telemetry: Streaming to Streamlit (http://localhost:{telemetryServer.Port})");
    }
    Console.WriteLine("This mode uses machine learning to optimize shift points based on lap performance.");
    Console.WriteLine("The system builds confidence through statistical analysis of lap times vs shift patterns.");
    Console.WriteLine();
    Console.WriteLine("Controls:");
    Console.WriteLine("  ESC - Return to main menu (prompts to save)");
    Console.WriteLine("  F2  - Save current learned configuration");
    Console.WriteLine("  F3  - Generate performance report");
    Console.WriteLine();

    // Audio feedback mode selection
    Console.WriteLine("Select audio feedback mode:");
    Console.WriteLine("  1. Performance Learning (real-time pitch guidance during shift approach)");
    Console.WriteLine("  2. Feedback-Based Optimization (silent + post-shift feedback tones)");
    Console.Write("Choice: ");
    var feedbackModeChoice = Console.ReadLine();

    AudioEngine.AudioMode selectedMode = AudioEngine.AudioMode.PerformanceLearning;
    if (feedbackModeChoice == "2")
    {
        selectedMode = AudioEngine.AudioMode.FeedbackOptimization;
        audioEngine.SetMode(AudioEngine.AudioMode.FeedbackOptimization);
    }
    Console.WriteLine();

    // Audio profile selection
    var audioProfile = SelectAudioProfileWithPreview(audioEngine, selectedMode);
    audioEngine.SetAudioProfile(audioProfile);
    Console.WriteLine();

    // Main loop state
    bool wasConnected = false;
    int readFailCount = 0;
    DateTime lastUpdate = DateTime.Now;
    DateTime lastLearnUpdate = DateTime.Now;
    const int LearnIntervalSeconds = 15; // Update learned shift points every 15 seconds

    // Removed off-track detection - now using ACC's IsValidLap flag

    // Tire pressure tracking for lap-before/lap-after display
    WheelAndTireData? lapStartTirePressure = null;
    WheelAndTireData? lapEndTirePressure = null;
    int lastCompletedLaps = 0;
    bool hasDisplayedLapPressures = false;

    // Track initial vehicle to detect changes during session
    string? initialVehicle = null;

    Console.Clear();
    Console.WriteLine("=== ACC RPM Monitor - Performance Learning Mode ===");
    Console.WriteLine($"Vehicle: {configManager.CurrentVehicleName}");
    Console.WriteLine("Waiting for Assetto Corsa Competizione...\n");

    while (true)
    {
        // Check for vehicle change
        string? detectedVehicle = vehicleDetector.GetCarModel();

        // Set initial vehicle on first detection
        if (detectedVehicle != null && initialVehicle == null)
            initialVehicle = detectedVehicle;

        // Only exit if vehicle CHANGES during the session
        if (initialVehicle != null && detectedVehicle != null && detectedVehicle != initialVehicle)
        {
            Console.WriteLine($"\nVehicle changed: {detectedVehicle}");
            Console.WriteLine("Returning to main menu...");
            Thread.Sleep(2000);
            break;
        }

        // Check for commands
        if (Console.KeyAvailable)
        {
            var key = Console.ReadKey(true).Key;
            if (key == ConsoleKey.Escape)
                break;
            else if (key == ConsoleKey.F2)
            {
                // Save learned configuration
                var learnedPoints = learningEngine.GenerateOptimalShiftPoints();
                if (learnedPoints.Count > 0)
                {
                    foreach (var kvp in learnedPoints)
                    {
                        config.SetRPMForGear(kvp.Key, kvp.Value);
                    }
                    configManager.SaveAutoConfig(config);

                    Console.SetCursorPosition(0, 20);
                    Console.WriteLine("✓ Learned configuration saved!                                              ");
                    Thread.Sleep(1500);
                }
            }
            else if (key == ConsoleKey.F3)
            {
                // Generate and save performance report
                var shiftReport = shiftPatternAnalyzer.GeneratePerformanceReport();
                var learningReport = learningEngine.GenerateLearningReport();
                string reportPath = reportGenerator.SavePerformanceReport(shiftReport, learningReport, configManager.CurrentVehicleName);

                Console.SetCursorPosition(0, 20);
                Console.WriteLine($"✓ Report saved to: {Path.GetFileName(reportPath)}                           ");

                // Regenerate power curve graph with user's actual shift points if we have auto-config data
                if (config.IsAutoGenerated && config.AccelerationCurves.Count > 0 && shiftReport.ValidLaps >= 2)
                {
                    try
                    {
                        var userShifts = PowerCurveGraph.ExtractUserShiftPoints(shiftReport);
                        if (userShifts.Count > 0)
                        {
                            string reportsDir = reportGenerator.GetReportsPath();
                            string graphPath = PowerCurveGraph.GenerateGraph(config, configManager.CurrentVehicleName,
                                                                           Path.Combine(reportsDir, configManager.CurrentVehicleName),
                                                                           userShifts);
                            Console.SetCursorPosition(0, 21);
                            Console.WriteLine($"✓ Updated power curve with your shift points                              ");
                        }
                    }
                    catch { /* Silently fail graph generation */ }
                }

                Thread.Sleep(2000);
            }
        }

        // Try connecting to ACC if not already connected
        if (!accMemory.IsConnected)
        {
            if (accMemory.Connect())
            {
                Console.Clear();
                Console.WriteLine("=== ACC RPM Monitor - Performance Learning ===");
                Console.WriteLine($"Vehicle: {configManager.CurrentVehicleName}");
                if (telemetryServer != null && telemetryServer.IsRunning)
                {
                    Console.WriteLine($"Telemetry: Streaming to Streamlit (http://localhost:{telemetryServer.Port})");
                }
                Console.WriteLine("Connected! Learning from your driving...\n");
                wasConnected = true;
                readFailCount = 0;
            }
            else
            {
                if (wasConnected)
                {
                    Console.Clear();
                    Console.WriteLine("=== ACC RPM Monitor - Performance Learning ===");
                    Console.WriteLine($"Vehicle: {configManager.CurrentVehicleName}");
                    Console.WriteLine("Connection lost. Waiting for ACC...\n");
                    wasConnected = false;
                    audioEngine.Stop();
                }
                Thread.Sleep(1000);
                continue;
            }
        }

        // Read comprehensive telemetry
        var telemetryData = accMemory.ReadFullTelemetry();
        var status = accMemory.ReadStatus();
        var lapTiming = accMemory.ReadLapTiming();
        var position = accMemory.ReadPosition();
        var tireData = accMemory.ReadWheelAndTireData();

        // Handle read failures
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
        (int currentGear, int currentRPM, float throttle, float speed) = telemetryData.Value;

        // Update telemetry server if enabled
        if (telemetryServer != null)
        {
            if (tireData != null)
            {
                // Read fuel from physics struct
                var physics = accMemory.ReadPhysicsStruct();
                float fuel = physics?.Fuel ?? 0f;
                telemetryServer.UpdateTelemetry(tireData, currentRPM, currentGear - 1, speed, fuel);

                // Update lap comparison data
                if (lapTiming != null)
                {
                    telemetryServer.UpdateLapData(lapTiming.CompletedLaps, tireData);
                }
            }
            else
            {
                // Show "No data" state when tireData is null
                telemetryServer.ShowNoData();
            }
        }

        // Only provide feedback when actually driving
        bool isDriving = status == 2; // AC_LIVE

        if (!isDriving)
        {
            audioEngine.Stop();
            Console.SetCursorPosition(0, 4);
            Console.WriteLine($"ACC Status:   {GetStatusName(status.Value)}                              ");
            Console.WriteLine("Waiting for session...                                                  ");
            Thread.Sleep(100);
            continue;
        }

        // Convert gear for display (ACC uses gear 2 as first gear)
        int displayGear = currentGear - 1;

        // Update monitoring data collector
        dataCollector.Update(displayGear, currentRPM, speed, tireData);

        // Detect lap completion BEFORE update
        int completedLapsBeforeUpdate = lastCompletedLaps;

        // Update shift pattern analyzer
        if (currentGear >= 1 && lapTiming != null && position != null && displayGear >= 1)
        {
            shiftPatternAnalyzer.Update(
                displayGear,
                currentRPM,
                throttle,
                speed,
                position.NormalizedPosition,
                lapTiming
            );

            // Also feed data to acceleration analyzer for physics-based learning
            shiftAnalyzer.AddDataPoint(currentRPM, throttle, speed, displayGear);
        }

        // Detect lap completion and capture tire pressures
        if (lapTiming != null && lapTiming.CompletedLaps > completedLapsBeforeUpdate)
        {
            // Lap was completed - save the end tire pressure from previous frame
            if (tireData != null && lapEndTirePressure == null)
            {
                lapEndTirePressure = tireData;
            }

            // Prepare for next lap - current tire data becomes start of next lap
            if (tireData != null)
            {
                lapStartTirePressure = tireData;
            }
            hasDisplayedLapPressures = false;
            lastCompletedLaps = lapTiming.CompletedLaps;
        }
        else if (lastCompletedLaps == 0 && lapTiming != null && lapTiming.CompletedLaps >= 0)
        {
            // Initialize at start of session
            if (tireData != null)
            {
                lapStartTirePressure = tireData;
            }
            lastCompletedLaps = lapTiming.CompletedLaps;
        }

        // Periodically update shift points from learning
        if ((DateTime.Now - lastLearnUpdate).TotalSeconds >= LearnIntervalSeconds)
        {
            var learnedPoints = learningEngine.GenerateOptimalShiftPoints();
            foreach (var kvp in learnedPoints)
            {
                config.SetRPMForGear(kvp.Key, kvp.Value);
            }
            lastLearnUpdate = DateTime.Now;
        }

        // Display status
        Console.SetCursorPosition(0, 4);
        Console.WriteLine($"ACC Status:   {GetStatusName(status.Value)}                              ");

        // Ignore neutral and reverse
        if (currentGear <= 1)
        {
            audioEngine.Stop();
            Console.WriteLine($"Current Gear: N/R                                                       ");
            Console.WriteLine($"Current RPM:  {currentRPM}                                              ");
            Thread.Sleep(50);
            continue;
        }
        int threshold = config.GetRPMForGear(displayGear);

        // Get recommendation for current gear
        var recommendation = learningEngine.GetRecommendationForGear(displayGear, threshold);
        if (recommendation.HasRecommendation)
        {
            // Feed recommendation to audio engine for pitch-based guidance
            audioEngine.SetRecommendedShiftRPM(recommendation.RecommendedRPM);
        }

        // Update audio with pitch-based guidance
        audioEngine.UpdateRPM(currentRPM, threshold, displayGear);

        // Display telemetry
        Console.WriteLine();
        Console.WriteLine($"Current Gear:    {displayGear}                                             ");
        Console.WriteLine($"Current RPM:     {currentRPM}                                              ");
        Console.WriteLine($"Shift Threshold: {threshold} RPM                                           ");
        Console.WriteLine($"Throttle:        {throttle * 100:F1}%                                      ");
        Console.WriteLine($"Speed:           {speed:F1} km/h                                           ");

        // Learning status
        Console.WriteLine();
        Console.WriteLine("─── LEARNING STATUS ───────────────────────────────────────────");
        Console.WriteLine($"Total Laps:      {shiftPatternAnalyzer.GetTotalLaps()}                     ");
        Console.WriteLine($"Valid Laps:      {shiftPatternAnalyzer.GetValidLaps()}                     ");
        Console.WriteLine($"Total Shifts:    {shiftPatternAnalyzer.GetTotalShifts()}                   ");
        Console.WriteLine($"Learning Rate:   {learningEngine.GetLearningRate():P0}                     ");
        Console.WriteLine($"Data Quality:    {(learningEngine.GetDataQuality() < 3 ? "Building..." : learningEngine.GetDataQuality() < 5 ? "Good" : "Excellent")}     ");

        // Continuation recommendation at 2 valid laps
        if (shiftPatternAnalyzer.GetValidLaps() >= 2 && shiftPatternAnalyzer.GetValidLaps() < 5)
        {
            Console.WriteLine($"💡 Analysis ready! ({shiftPatternAnalyzer.GetValidLaps()} valid laps) - Continue for more refined shift points");
        }

        // Debug: Show lap timing data
        if (lapTiming != null)
        {
            Console.WriteLine();
            Console.WriteLine("─── LAP DEBUG INFO ────────────────────────────────────────────");
            Console.WriteLine($"Completed Laps:  {lapTiming.CompletedLaps}                              ");
            Console.WriteLine($"Current Time:    {lapTiming.CurrentLapTime}                             ");
            Console.WriteLine($"Last Lap Time:   {lapTiming.LastLapTime} ({lapTiming.LastLapTimeMs}ms)  ");
            Console.WriteLine($"Best Lap Time:   {lapTiming.BestLapTime}                                ");
            Console.WriteLine($"Is Valid Lap:    {lapTiming.IsCurrentLapValid} (current lap in progress)");
        }

        // Show tire pressures before and after lap
        if ((lapStartTirePressure != null || lapEndTirePressure != null) && !hasDisplayedLapPressures && lapTiming != null)
        {
            Console.WriteLine();
            Console.WriteLine("─── TIRE PRESSURES (PSI) ──────────────────────────────────────");
            if (lapStartTirePressure != null)
            {
                Console.WriteLine($"Start of Lap {lapTiming.CompletedLaps}:");
                Console.WriteLine($"  FL: {lapStartTirePressure.WheelPressureFL:F2}  FR: {lapStartTirePressure.WheelPressureFR:F2}  RL: {lapStartTirePressure.WheelPressureRL:F2}  RR: {lapStartTirePressure.WheelPressureRR:F2}");
                Console.WriteLine($"  Avg: {lapStartTirePressure.AverageWheelPressure:F2} PSI");
            }
            if (lapEndTirePressure != null)
            {
                Console.WriteLine($"End of Lap {lapTiming.CompletedLaps - 1}:");
                Console.WriteLine($"  FL: {lapEndTirePressure.WheelPressureFL:F2}  FR: {lapEndTirePressure.WheelPressureFR:F2}  RL: {lapEndTirePressure.WheelPressureRL:F2}  RR: {lapEndTirePressure.WheelPressureRR:F2}");
                Console.WriteLine($"  Avg: {lapEndTirePressure.AverageWheelPressure:F2} PSI");
                if (lapStartTirePressure != null)
                {
                    float pressureDelta = lapStartTirePressure.AverageWheelPressure - lapEndTirePressure.AverageWheelPressure;
                    Console.WriteLine($"  Change: {(pressureDelta >= 0 ? "+" : "")}{pressureDelta:F2} PSI");
                }
            }
            hasDisplayedLapPressures = true;
        }

        // Show recommendation for current gear
        if (recommendation.HasRecommendation)
        {
            Console.WriteLine();
            Console.WriteLine("─── SHIFT POINT RECOMMENDATION ───────────────────────────────");
            Console.WriteLine($"{recommendation.Message}                                                ");
            Console.WriteLine($"Audio Pitch: {(currentRPM > recommendation.RecommendedRPM + 175 ? "HIGH (shift earlier)" : currentRPM < recommendation.RecommendedRPM - 175 ? "LOW (shift later)" : "NORMAL (optimal)")}                        ");
        }

        // Gear recommendation if available
        if (gearRecommendation != null)
        {
            int? optimalGear = gearRecommendation.GetOptimalGearForSpeed(speed, throttle);
            if (optimalGear.HasValue)
            {
                Console.WriteLine();
                string gearDisplay = optimalGear.Value == displayGear
                    ? $"✓ {displayGear} (optimal)"
                    : $"{optimalGear.Value} (suggested)";
                Console.WriteLine($"Optimal Gear:    {gearDisplay}                                         ");
            }
        }

        // Status indicator
        int rpmFromThreshold = currentRPM - threshold;
        Console.WriteLine();
        if (rpmFromThreshold >= 0)
        {
            Console.WriteLine($"Status:          SHIFT UP! ({rpmFromThreshold} RPM over)                ");
        }
        else if (rpmFromThreshold >= -audioEngine.GetCurrentWarningDistance())
        {
            Console.WriteLine($"Status:          Warning ({Math.Abs(rpmFromThreshold)} from threshold)  ");
        }
        else
        {
            Console.WriteLine($"Status:          Normal                                                 ");
        }

        Thread.Sleep(50); // ~20Hz
    }

    audioEngine.Stop();

    // End of session - generate final report
    Console.Clear();
    Console.WriteLine("=== Performance Learning Session Ended ===");
    Console.WriteLine();

    // Generate and save monitoring session report
    Console.WriteLine("Generating monitoring session report...");
    var monitoringReport = dataCollector.GenerateReport();
    string monitoringReportPath = reportGenerator.SaveMonitoringSessionReport(monitoringReport);
    Console.WriteLine($"Monitoring report saved to: {monitoringReportPath}");
    Console.WriteLine();

    if (shiftPatternAnalyzer.GetValidLaps() >= 2)
    {
        Console.WriteLine("Generating performance analysis report...");
        var shiftReport = shiftPatternAnalyzer.GeneratePerformanceReport();
        var learningReport = learningEngine.GenerateLearningReport();
        string reportPath = reportGenerator.SavePerformanceReport(shiftReport, learningReport, configManager.CurrentVehicleName);

        Console.WriteLine();
        Console.WriteLine(reportGenerator.GenerateConsoleSummary(learningReport));
        Console.WriteLine();
        Console.WriteLine($"Detailed report saved to:");
        Console.WriteLine($"  {reportPath}");
        Console.WriteLine();

        // Regenerate power curve graph with user's actual shift points if we have auto-config data
        if (config.IsAutoGenerated && config.AccelerationCurves.Count > 0)
        {
            try
            {
                var userShifts = PowerCurveGraph.ExtractUserShiftPoints(shiftReport);
                if (userShifts.Count > 0)
                {
                    Console.WriteLine("Updating power curve graph with your actual shift points...");
                    string reportsDir = reportGenerator.GetReportsPath();
                    string graphPath = PowerCurveGraph.GenerateGraph(config, configManager.CurrentVehicleName,
                                                                   Path.Combine(reportsDir, configManager.CurrentVehicleName),
                                                                   userShifts);
                    Console.WriteLine($"Updated graph saved to:");
                    Console.WriteLine($"  {graphPath}");
                    Console.WriteLine();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not update power curve graph: {ex.Message}");
                Console.WriteLine();
            }
        }

        Console.Write("Save learned shift points to configuration? (Y/N): ");
        var saveChoice = Console.ReadKey().KeyChar;
        Console.WriteLine();
        if (saveChoice == 'Y' || saveChoice == 'y')
        {
            var learnedPoints = learningEngine.GenerateOptimalShiftPoints();
            foreach (var kvp in learnedPoints)
            {
                config.SetRPMForGear(kvp.Key, kvp.Value);
            }
            configManager.SaveAutoConfig(config);
            Console.WriteLine("\n\n✓ Configuration saved successfully!");
        }
    }
    else
    {
        Console.WriteLine($"Not enough data collected ({shiftPatternAnalyzer.GetValidLaps()} valid laps).");
        Console.WriteLine("Need at least 2 valid laps for performance analysis.");
    }

    Console.WriteLine();
    Console.WriteLine("Press any key to return to main menu...");
    Console.ReadKey();
}


// Helper to show ACC status in readable format
static string GetStatusName(int status)
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

// Toggle telemetry window on/off
static TelemetryServer? ToggleTelemetryServer(TelemetryServer? currentServer)
{
    if (currentServer != null && currentServer.IsRunning)
    {
        // Close the window
        Console.Clear();
        Console.WriteLine("=== Close Telemetry Window ===\n");
        Console.WriteLine("Closing telemetry window...");
        currentServer.Stop();
        currentServer.Dispose();
        Console.WriteLine("✓ Telemetry window closed");
        Console.WriteLine("\nPress any key to return to main menu...");
        Console.ReadKey();
        return null;
    }
    else
    {
        // Open the window
        Console.Clear();
        Console.WriteLine("=== Open Telemetry Window ===\n");
        Console.WriteLine("Opening telemetry overlay window...");

        var server = new TelemetryServer();
        var accMemory = new SharedMemoryReader();

        if (server.Start(accMemory))
        {
            Console.WriteLine("✓ Telemetry window opened");
            Console.WriteLine();
            Console.WriteLine("The telemetry overlay is now running.");
            Console.WriteLine("• Window is transparent and overlays on top of ACC");
            Console.WriteLine("• Click and drag to reposition the window");
            Console.WriteLine("• Data will update automatically when monitoring starts");
            Console.WriteLine("• Close from this menu (option [4]) when done");
            Console.WriteLine();
            Console.WriteLine("Note: The window will display live data when you start monitoring (option [3]).");
            Console.WriteLine("\nPress any key to return to main menu...");
            Console.ReadKey();
            return server;
        }
        else
        {
            Console.WriteLine("✗ Failed to open telemetry window");
            Console.WriteLine("\nPress any key to return to main menu...");
            Console.ReadKey();
            return null;
        }
    }
}

// Audio preview selection helper
static AudioEngine.AudioProfile SelectAudioProfileWithPreview(AudioEngine audioEngine, AudioEngine.AudioMode mode)
{
    while (true)
    {
        Console.WriteLine("Select audio profile:");
        Console.WriteLine("  1. Normal (responsive tones)");
        Console.WriteLine("  2. Endurance (low-fatigue for long sessions)");
        Console.WriteLine();
        Console.WriteLine("  [P] Preview Normal profile tones");
        Console.WriteLine("  [E] Preview Endurance profile tones");
        Console.WriteLine();
        Console.Write("Choice (1/2/P/E): ");

        var input = Console.ReadLine()?.Trim().ToUpper();

        if (input == "1")
        {
            return AudioEngine.AudioProfile.Normal;
        }
        else if (input == "2")
        {
            return AudioEngine.AudioProfile.Endurance;
        }
        else if (input == "P")
        {
            Console.WriteLine();
            Console.WriteLine("Playing Normal profile preview...");
            Console.WriteLine("  (Too Early → Optimal → Too Late)");
            Console.WriteLine();
            audioEngine.PlayTonePreview(mode, AudioEngine.AudioProfile.Normal);
            Console.WriteLine("Preview complete.");
            Console.WriteLine();
        }
        else if (input == "E")
        {
            Console.WriteLine();
            Console.WriteLine("Playing Endurance profile preview...");
            Console.WriteLine("  (Too Early → Optimal → Too Late)");
            Console.WriteLine();
            audioEngine.PlayTonePreview(mode, AudioEngine.AudioProfile.Endurance);
            Console.WriteLine("Preview complete.");
            Console.WriteLine();
        }
    }
}

// Handle direct launch from command-line arguments


