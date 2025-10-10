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

        case MainMenuChoice.Exit:
            exitApp = true;
            break;
    }
}

Console.Clear();
Console.WriteLine("Goodbye!");

// Monitor mode - the actual RPM monitoring
static void RunMonitor(ConfigManager configManager, GearRPMConfig config)
{
    // Initialize dynamic audio engine
    using var audioEngine = new DynamicAudioEngine();

    // Initialize ACC shared memory
    using var accMemory = new ACCSharedMemorySimple();

    Console.Clear();
    Console.WriteLine("=== ACC RPM Monitor - Running ===");
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
