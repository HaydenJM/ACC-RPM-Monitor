using ACCRPMMonitor;

// Initialize config manager and vehicle detector
var configManager = new ConfigManager();
var vehicleDetector = new VehicleDetector();

// Try to detect current vehicle from ACC
Console.WriteLine("Detecting vehicle from ACC...");
if (vehicleDetector.Connect())
{
    var detectedVehicle = vehicleDetector.GetCarModel();
    if (!string.IsNullOrEmpty(detectedVehicle))
    {
        Console.WriteLine($"Detected vehicle: {detectedVehicle}");
        configManager.SetVehicle(detectedVehicle);
    }
    vehicleDetector.Dispose();
}

// If no vehicles exist, create a default one
if (configManager.GetAvailableVehicles().Count == 0)
{
    var defaultConfig = GearRPMConfig.CreateDefault();
    configManager.SaveConfig(defaultConfig);
}

// Main application loop
bool exitApp = false;
while (!exitApp)
{
    var menuChoice = ConfigUI.ShowMainMenu(configManager);

    switch (menuChoice)
    {
        case MainMenuChoice.CreateAutoConfig:
            AutoConfigWorkflow.Run(configManager);
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
            RunMonitor(configManager, config);
            break;

        case MainMenuChoice.ChangeVehicle:
            ConfigUI.ShowVehicleSelectionMenu(configManager);
            break;

        case MainMenuChoice.OpenConfigFolder:
            OpenConfigFolder();
            break;

        case MainMenuChoice.Exit:
            exitApp = true;
            break;
    }
}

Console.Clear();
Console.WriteLine("Goodbye!");

// Opens the config folder in File Explorer
static void OpenConfigFolder()
{
    string configPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ACCRPMMonitor"
    );

    // Ensure the directory exists
    Directory.CreateDirectory(configPath);

    try
    {
        // Open the folder in File Explorer
        System.Diagnostics.Process.Start("explorer.exe", configPath);

        Console.Clear();
        Console.WriteLine("=== Open Config Folder ===\n");
        Console.WriteLine($"Opening: {configPath}\n");
        Console.WriteLine("Press any key to continue...");
        Console.ReadKey();
    }
    catch (Exception ex)
    {
        Console.Clear();
        Console.WriteLine("=== Open Config Folder ===\n");
        Console.WriteLine($"Error opening folder: {ex.Message}\n");
        Console.WriteLine($"Path: {configPath}\n");
        Console.WriteLine("Press any key to continue...");
        Console.ReadKey();
    }
}

// Monitor mode - the actual RPM monitoring
static void RunMonitor(ConfigManager configManager, GearRPMConfig config)
{
    // Check if user wants adaptive mode
    Console.Clear();
    Console.WriteLine("=== ACC RPM Monitor ===");
    Console.WriteLine();
    Console.WriteLine("Select monitoring mode:");
    Console.WriteLine("  1. Standard Mode - Use fixed shift points");
    Console.WriteLine("  2. Adaptive Mode - Continuously learn and update shift points");
    Console.WriteLine();
    Console.Write("Choice (1 or 2): ");

    var choice = Console.ReadKey();
    bool adaptiveMode = choice.KeyChar == '2';

    if (adaptiveMode)
    {
        RunAdaptiveMonitor(configManager, config);
    }
    else
    {
        RunStandardMonitor(configManager, config);
    }
}

// Standard monitor mode with fixed shift points
static void RunStandardMonitor(ConfigManager configManager, GearRPMConfig config)
{
    // Initialize dynamic audio engine
    using var audioEngine = new DynamicAudioEngine();

    // Initialize ACC shared memory
    using var accMemory = new ACCSharedMemorySimple();

    Console.Clear();
    Console.WriteLine("=== ACC RPM Monitor - Standard Mode ===");
    Console.WriteLine($"Vehicle: {configManager.CurrentVehicleName}");
    Console.WriteLine($"Mode: {configManager.CurrentMode}");
    Console.WriteLine("Press ESC to exit\n");

    // Main loop state
    bool wasConnected = false;
    int readFailCount = 0;

    Console.WriteLine("Waiting for Assetto Corsa Competizione...");

    while (true)
    {
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
        var gearRpmData = accMemory.ReadGearAndRPM();
        var status = accMemory.ReadStatus();

        // Handle read failures
        if (gearRpmData == null || status == null)
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

        var (currentGear, currentRPM) = gearRpmData.Value;

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
        int threshold = config.GetRPMForGear(displayGear);

        // Update audio with dynamic beeping timing based on RPM rate
        audioEngine.UpdateRPM(currentRPM, threshold, displayGear);

        // Display current telemetry
        Console.WriteLine($"Current Gear: {displayGear}                                                ");
        Console.WriteLine($"Current RPM:  {currentRPM}                                                 ");
        Console.WriteLine($"Threshold:    {threshold} RPM                                              ");
        Console.WriteLine($"RPM Rate:     {audioEngine.GetCurrentRPMRate():F0} RPM/sec                 ");
        Console.WriteLine($"Beep Dist:    {audioEngine.GetCurrentWarningDistance()} RPM                ");

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

        Thread.Sleep(50); // ~20Hz update rate
    }

    audioEngine.Stop();
}

// Adaptive monitor mode - continuously learns and updates shift points
static void RunAdaptiveMonitor(ConfigManager configManager, GearRPMConfig config)
{
    // Initialize dynamic audio engine
    using var audioEngine = new DynamicAudioEngine();

    // Initialize ACC shared memory
    using var accMemory = new ACCSharedMemorySimple();

    // Initialize shift analyzer for continuous learning
    var shiftAnalyzer = new OptimalShiftAnalyzer();

    Console.Clear();
    Console.WriteLine("=== ACC RPM Monitor - Adaptive Mode ===");
    Console.WriteLine($"Vehicle: {configManager.CurrentVehicleName}");
    Console.WriteLine($"Mode: {configManager.CurrentMode} (Adaptive)");
    Console.WriteLine("Press ESC to exit | Press F2 to save learned config\n");

    // Main loop state
    bool wasConnected = false;
    int readFailCount = 0;
    DateTime lastUpdate = DateTime.Now;
    const int UpdateIntervalSeconds = 30; // Update shift points every 30 seconds

    Console.WriteLine("Waiting for Assetto Corsa Competizione...");

    while (true)
    {
        // Check for exit or save
        if (Console.KeyAvailable)
        {
            var key = Console.ReadKey(true).Key;
            if (key == ConsoleKey.Escape)
                break;
            else if (key == ConsoleKey.F2)
            {
                // Save the learned configuration
                var optimalConfig = shiftAnalyzer.GenerateOptimalConfig();
                if (optimalConfig != null)
                {
                    var adaptiveConfig = GearRPMConfig.FromOptimalConfig(optimalConfig);
                    configManager.SaveAutoConfig(adaptiveConfig);
                    Console.SetCursorPosition(0, 15);
                    Console.WriteLine("Learned configuration saved!                                            ");
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

        // Continuously collect data for gears 1-5 when at full throttle
        if (displayGear >= 1 && displayGear <= 5)
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
        Console.WriteLine($"Current Gear: {displayGear} (ACC Gear: {currentGear})                      ");
        Console.WriteLine($"Current RPM:  {currentRPM}                                                 ");
        Console.WriteLine($"Threshold:    {threshold} RPM                                              ");
        Console.WriteLine($"Throttle:     {throttle * 100:F1}%                                         ");
        Console.WriteLine($"Speed:        {speed:F1} km/h                                              ");

        // Show per-gear data collection progress
        string gearDataStatus = $"G1:{shiftAnalyzer.GetDataPointCountForGear(1)} " +
                               $"G2:{shiftAnalyzer.GetDataPointCountForGear(2)} " +
                               $"G3:{shiftAnalyzer.GetDataPointCountForGear(3)} " +
                               $"G4:{shiftAnalyzer.GetDataPointCountForGear(4)} " +
                               $"G5:{shiftAnalyzer.GetDataPointCountForGear(5)}";
        Console.WriteLine($"Data Points:  {gearDataStatus} (Total: {shiftAnalyzer.GetDataPointCount()}) ");
        Console.WriteLine($"RPM Rate:     {audioEngine.GetCurrentRPMRate():F0} RPM/sec                 ");
        Console.WriteLine($"Beep Dist:    {audioEngine.GetCurrentWarningDistance()} RPM                ");

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
            bool isCollecting = (throttle >= 0.85f && speed > 5f && displayGear >= 1 && displayGear <= 5);
            string dataStatus;

            if (isCollecting)
            {
                dataStatus = "✓ Collecting data";
            }
            else if (displayGear < 1 || displayGear > 5)
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

        Thread.Sleep(50); // ~20Hz update rate
    }

    audioEngine.Stop();

    // Ask if user wants to save learned configuration
    Console.Clear();
    Console.WriteLine("=== Adaptive Mode Session Ended ===");
    Console.WriteLine();
    Console.WriteLine($"Total data points collected: {shiftAnalyzer.GetDataPointCount()}");
    Console.WriteLine();
    Console.Write("Save learned configuration? (Y/N): ");
    var saveChoice = Console.ReadKey();
    if (saveChoice.KeyChar == 'Y' || saveChoice.KeyChar == 'y')
    {
        var optimalConfig = shiftAnalyzer.GenerateOptimalConfig();
        if (optimalConfig != null)
        {
            var adaptiveConfig = GearRPMConfig.FromOptimalConfig(optimalConfig);
            configManager.SaveAutoConfig(adaptiveConfig);
            Console.WriteLine("\n\nConfiguration saved successfully!");
        }
        else
        {
            Console.WriteLine("\n\nNot enough data collected to generate configuration.");
        }
        Thread.Sleep(2000);
    }
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
