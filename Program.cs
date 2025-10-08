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

// Let user pick which vehicle config to use
ConfigUI.ShowVehicleSelectionMenu(configManager);

// Let user choose manual or auto configuration mode
ConfigUI.ShowModeSelectionMenu(configManager);

// Load the config based on selected mode
var config = configManager.LoadConfig();

// Let user edit RPM thresholds if in manual mode
ConfigUI.ShowConfigMenu(config, configManager);

// Initialize dynamic audio engine and optimal shift analyzer
using var audioEngine = new DynamicAudioEngine();
var shiftAnalyzer = new OptimalShiftAnalyzer();

// Initialize ACC shared memory
using var accMemory = new ACCSharedMemorySimple();

Console.Clear();
Console.WriteLine("=== ACC RPM Monitor - Running ===");
Console.WriteLine($"Vehicle: {configManager.CurrentVehicleName}");
Console.WriteLine($"Mode: {configManager.CurrentMode}");
Console.WriteLine("Press ESC to exit | F1 to toggle data collection\n");

// Main loop state
bool wasConnected = false;
int readFailCount = 0;
bool collectingData = configManager.CurrentMode == ConfigMode.Auto; // Auto-collect in auto mode
int dataCollectionSaveInterval = 500; // Save auto config every 500 points
int lastSaveCount = 0;

Console.WriteLine("Waiting for Assetto Corsa Competizione...");

while (true)
{
    // Check for exit and controls
    if (Console.KeyAvailable)
    {
        var key = Console.ReadKey(true).Key;
        if (key == ConsoleKey.Escape)
            break;
        else if (key == ConsoleKey.F1)
        {
            collectingData = !collectingData;
            Console.SetCursorPosition(0, 7);
            Console.WriteLine($"Data collection: {(collectingData ? "ON " : "OFF")}                      ");
        }
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
            Console.WriteLine("Press ESC to exit | F1 to toggle data collection\n");
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
                Console.WriteLine("Press ESC to exit | F1 to toggle data collection\n");
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
        Console.WriteLine($"Data collection: {(collectingData ? "ON" : "OFF")}                       ");
        Console.WriteLine("Status:       Waiting for session...                                    ");
        Thread.Sleep(100);
        continue;
    }

    // Display status
    Console.SetCursorPosition(0, 7);
    Console.WriteLine($"ACC Status:   {GetStatusName(status.Value)}                              ");
    Console.WriteLine($"Data collection: {(collectingData ? "ON" : "OFF")} | Points: {shiftAnalyzer.GetDataPointCount()}    ");
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

    // Collect data for auto-learning if enabled
    if (collectingData && isDriving)
    {
        // Need to read full physics for speed/throttle
        // For now, estimate throttle from RPM change rate (simplified)
        float estimatedThrottle = audioEngine.GetCurrentRPMRate() > 100 ? 1.0f : 0.5f;

        // In a real implementation, we'd read actual throttle from physics memory
        // For now, only collect data when RPMs are increasing (likely full throttle)
        if (audioEngine.GetCurrentRPMRate() > 200)
        {
            shiftAnalyzer.AddDataPoint(currentRPM, estimatedThrottle, currentRPM / 100f, displayGear);

            // Periodically save auto config
            if (shiftAnalyzer.GetDataPointCount() - lastSaveCount >= dataCollectionSaveInterval)
            {
                var optimalConfig = shiftAnalyzer.GenerateOptimalConfig();
                if (optimalConfig != null)
                {
                    var autoConfig = GearRPMConfig.FromOptimalConfig(optimalConfig);
                    configManager.SaveAutoConfig(autoConfig);

                    // If in auto mode, reload the config
                    if (configManager.CurrentMode == ConfigMode.Auto)
                    {
                        config = configManager.LoadConfig();
                        threshold = config.GetRPMForGear(displayGear);
                    }

                    lastSaveCount = shiftAnalyzer.GetDataPointCount();
                }
            }
        }
    }

    // Update audio with dynamic warning timing based on RPM
    audioEngine.UpdateRPM(currentRPM, threshold, displayGear);

    // Display current telemetry
    Console.WriteLine($"Current Gear: {displayGear}                                                ");
    Console.WriteLine($"Current RPM:  {currentRPM}                                                 ");
    Console.WriteLine($"Threshold:    {threshold} RPM                                              ");
    Console.WriteLine($"RPM Rate:     {audioEngine.GetCurrentRPMRate():F0} RPM/sec                 ");
    Console.WriteLine($"Warning Dist: {audioEngine.GetCurrentWarningDistance()} RPM                ");

    int rpmFromThreshold = currentRPM - threshold;
    if (rpmFromThreshold >= -100)
    {
        Console.WriteLine($"Status:       SHIFT UP! ({rpmFromThreshold + 100} RPM over beep threshold)         ");
    }
    else if (rpmFromThreshold >= -audioEngine.GetCurrentWarningDistance())
    {
        // Calculate frequency (matches DynamicAudioEngine logic)
        float baseFreq = 500f + (displayGear - 1) * 100f;
        float maxFreq = baseFreq + 100f;
        int warningDist = audioEngine.GetCurrentWarningDistance();
        float progress = (float)(currentRPM - (threshold - warningDist)) / (warningDist - 100);
        float frequency = baseFreq + (maxFreq - baseFreq) * Math.Clamp(progress, 0f, 1f);
        Console.WriteLine($"Status:       Warning ({Math.Abs(rpmFromThreshold)} RPM from threshold) - {frequency:F0}Hz    ");
    }
    else
    {
        Console.WriteLine($"Status:       Normal ({Math.Abs(rpmFromThreshold)} RPM from threshold)              ");
    }

    Thread.Sleep(50); // ~20Hz update rate
}

audioEngine.Stop();
Console.Clear();
Console.WriteLine("\nExiting...");

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
