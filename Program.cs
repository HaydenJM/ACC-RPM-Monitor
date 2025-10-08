using ACCRPMMonitor;

// Initialize configuration manager
var configManager = new ConfigManager();

// Show vehicle selection menu
ConfigUI.ShowVehicleSelectionMenu(configManager);

// Load configuration for selected vehicle (creates default if not found)
var config = configManager.LoadConfig();

// Show configuration menu
ConfigUI.ShowConfigMenu(config, configManager);

// Initialize audio engine
using var audioEngine = new AudioEngine();

// Initialize ACC shared memory
using var accMemory = new ACCSharedMemorySimple();

Console.Clear();
Console.WriteLine("=== ACC RPM Monitor - Running ===");
Console.WriteLine($"Vehicle: {configManager.CurrentVehicleName}");
Console.WriteLine("Press ESC to exit\n");

// Try to connect to ACC
bool wasConnected = false;
int readFailCount = 0;
Console.WriteLine("Waiting for Assetto Corsa Competizione...");

while (true)
{
    // Check for exit key
    if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)
        break;

    // Try to connect if not already connected
    if (!accMemory.IsConnected)
    {
        if (accMemory.Connect())
        {
            Console.Clear();
            Console.WriteLine("=== ACC RPM Monitor - Running ===");
            Console.WriteLine($"Vehicle: {configManager.CurrentVehicleName}");
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
                Console.WriteLine("Press ESC to exit\n");
                Console.WriteLine("Connection lost. Waiting for ACC...");
                wasConnected = false;
                audioEngine.Stop();
            }
            Thread.Sleep(1000);
            continue;
        }
    }

    // Read telemetry data
    var gearRpmData = accMemory.ReadGearAndRPM();
    var status = accMemory.ReadStatus();

    if (gearRpmData == null || status == null)
    {
        // Read failed - only dispose after multiple failures
        readFailCount++;
        Console.SetCursorPosition(0, 5);
        Console.WriteLine($"Read failures: {readFailCount}/10                                          ");
        if (accMemory.LastError != null)
        {
            Console.WriteLine($"Error: {accMemory.LastError}                                              ");
        }

        if (readFailCount > 10)
        {
            Console.WriteLine("Multiple read failures. Reconnecting...                                  ");
            accMemory.Dispose();
            readFailCount = 0;
        }
        Thread.Sleep(100);
        continue;
    }

    readFailCount = 0; // Reset fail counter on successful read

    var (currentGear, currentRPM) = gearRpmData.Value;

    // Only provide feedback when actually driving (not in menus or replay)
    bool isDriving = status == 2; // AC_LIVE

    if (!isDriving)
    {
        audioEngine.Stop();
        Console.SetCursorPosition(0, 5);
        Console.WriteLine($"ACC Status:   {GetStatusName(status.Value)}                              ");
        Console.WriteLine("Status:       Waiting for session...                                    ");
        Thread.Sleep(100);
        continue;
    }

    // Display basic info
    Console.SetCursorPosition(0, 5);
    Console.WriteLine($"ACC Status:   {GetStatusName(status.Value)}                              ");
    Console.WriteLine();

    // Ignore neutral and reverse
    if (currentGear <= 1)
    {
        audioEngine.Stop();
        Console.WriteLine($"Current Gear: N/R                                                       ");
        Console.WriteLine($"Current RPM:  {currentRPM}                                              ");
        Console.WriteLine($"Status:       Neutral/Reverse                                           ");
        Thread.Sleep(50);
        continue;
    }

    // Get threshold for current gear (ACC gear 2 = first gear, so subtract 1)
    int displayGear = currentGear - 1;
    int threshold = config.GetRPMForGear(displayGear);

    // Update audio based on current RPM
    audioEngine.UpdateRPM(currentRPM, threshold, displayGear);

    // Display current status
    Console.WriteLine($"Current Gear: {displayGear}                                                ");
    Console.WriteLine($"Current RPM:  {currentRPM}                                                 ");
    Console.WriteLine($"Threshold:    {threshold} RPM                                              ");

    int rpmFromThreshold = currentRPM - threshold;
    if (rpmFromThreshold >= -100)
    {
        Console.WriteLine($"Status:       SHIFT UP! ({rpmFromThreshold + 100} RPM over beep threshold)         ");
    }
    else if (rpmFromThreshold >= -300)
    {
        // Calculate frequency based on current gear (same logic as AudioEngine)
        float baseFreq = displayGear <= 2 ? 500f : 500f + (displayGear - 2) * 100f;
        float frequency = baseFreq + ((currentRPM - (threshold - 300)) / 2f);
        Console.WriteLine($"Status:       Warning ({Math.Abs(rpmFromThreshold)} RPM from threshold) - {frequency:F0}Hz    ");
    }
    else
    {
        Console.WriteLine($"Status:       Normal ({Math.Abs(rpmFromThreshold)} RPM from threshold)              ");
    }

    Thread.Sleep(50); // Update at ~20Hz
}

audioEngine.Stop();
Console.Clear();
Console.WriteLine("\nExiting...");

// Helper method for diagnostics
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
