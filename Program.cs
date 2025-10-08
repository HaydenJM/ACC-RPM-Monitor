using ACCRPMMonitor;

// Initialize config manager and load vehicle setup
var configManager = new ConfigManager();

// Let user pick which vehicle config to use
ConfigUI.ShowVehicleSelectionMenu(configManager);

// Load the config (creates default if needed)
var config = configManager.LoadConfig();

// Let user edit RPM thresholds if they want
ConfigUI.ShowConfigMenu(config, configManager);

// Initialize audio and memory reader
using var audioEngine = new AudioEngine();
using var accMemory = new ACCSharedMemorySimple();

Console.Clear();
Console.WriteLine("=== ACC RPM Monitor - Running ===");
Console.WriteLine($"Vehicle: {configManager.CurrentVehicleName}");
Console.WriteLine("Press ESC to exit\n");

// Main loop - wait for ACC and read telemetry
bool wasConnected = false;
int readFailCount = 0;
Console.WriteLine("Waiting for Assetto Corsa Competizione...");

while (true)
{
    // Check for exit
    if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)
        break;

    // Try connecting to ACC if not already connected
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

    // Read telemetry
    var gearRpmData = accMemory.ReadGearAndRPM();
    var status = accMemory.ReadStatus();

    // Handle read failures
    if (gearRpmData == null || status == null)
    {
        readFailCount++;
        Console.SetCursorPosition(0, 5);
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
        Console.SetCursorPosition(0, 5);
        Console.WriteLine($"ACC Status:   {GetStatusName(status.Value)}                              ");
        Console.WriteLine("Status:       Waiting for session...                                    ");
        Thread.Sleep(100);
        continue;
    }

    // Display status
    Console.SetCursorPosition(0, 5);
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

    // Update audio based on RPM
    audioEngine.UpdateRPM(currentRPM, threshold, displayGear);

    // Display current telemetry
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
        // Calculate frequency (matches AudioEngine logic)
        float baseFreq = 500f + (displayGear - 1) * 100f;
        float frequency = baseFreq + ((currentRPM - (threshold - 300)) / 2f);
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
