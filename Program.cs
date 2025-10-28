using ACCRPMMonitor;
using System.Runtime.InteropServices;

// Check for CLI arguments
var cliArgs = Environment.GetCommandLineArgs();
bool isHeadless = cliArgs.Contains("--headless");
bool isViewer = cliArgs.Contains("--viewer");
string? serverAddress = null;

// Extract server address for viewer mode
if (isViewer && cliArgs.Length > 0)
{
    for (int i = 0; i < cliArgs.Length - 1; i++)
    {
        if (cliArgs[i] == "--server" && i + 1 < cliArgs.Length)
        {
            serverAddress = cliArgs[i + 1];
            break;
        }
    }
    serverAddress ??= "localhost";
}

// Run headless or viewer mode if specified
if (isHeadless)
{
    RunHeadlessMode();
    return;
}

if (isViewer)
{
    RunViewerMode(serverAddress ?? "localhost");
    return;
}

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

// Initialize config manager and vehicle detector
var configManager = new ConfigMan();
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
            AutoConfigFlow.Run(configManager);
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

        case MainMenuChoice.Help:
            ConfigUI.ShowHelpMenu();
            break;

        case MainMenuChoice.Exit:
            exitApp = true;
            break;
    }
}

Console.Clear();
Console.WriteLine("Goodbye!");

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
static void RunMonitor(ConfigMan configManager, GearRPMConfig config)
{
    // Check if user wants adaptive mode
    Console.Clear();
    Console.WriteLine("=== ACC RPM Monitor ===");
    Console.WriteLine();
    Console.WriteLine("Select monitoring mode:");
    Console.WriteLine("  1. Standard Mode - Use fixed shift points");
    Console.WriteLine("  2. Adaptive Mode - Continuously learn and update shift points");
    Console.WriteLine("  3. Performance Learning Mode - Machine learning-based shift optimization using lap time correlation");
    Console.WriteLine();
    Console.Write("Choice (1-3): ");

    var choice = Console.ReadKey();

    if (choice.KeyChar == '3')
    {
        RunPerformanceLearningMonitor(configManager, config);
    }
    else if (choice.KeyChar == '2')
    {
        RunAdaptiveMonitor(configManager, config);
    }
    else
    {
        RunStandardMonitor(configManager, config);
    }
}

// Standard monitor mode with fixed shift points
static void RunStandardMonitor(ConfigMan configManager, GearRPMConfig config)
{
    // Initialize dynamic audio engine
    using var audioEngine = new DynAudioEng();

    // Initialize ACC shared memory
    using var accMemory = new ACCSharedMemorySimple();

    // Initialize gear recommendation engine if auto-config available
    GearRecommendationEngine? gearRecommendation = null;
    if (config.IsAutoGenerated && config.AccelerationCurves != null && config.AccelerationCurves.Count > 0)
    {
        gearRecommendation = new GearRecommendationEngine(config.AccelerationCurves, config.GearRatios);
    }

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
        var telemetryData = accMemory.ReadFullTelemetry();
        var status = accMemory.ReadStatus();

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

        Thread.Sleep(50); // ~20Hz update rate
    }

    audioEngine.Stop();
}

// Adaptive monitor mode - continuously learns and updates shift points
static void RunAdaptiveMonitor(ConfigMan configManager, GearRPMConfig config)
{
    // Initialize dynamic audio engine
    using var audioEngine = new DynAudioEng();

    // Initialize ACC shared memory
    using var accMemory = new ACCSharedMemorySimple();

    // Initialize shift analyzer for continuous learning
    var shiftAnalyzer = new OptimalShift();

    // Initialize gear recommendation engine if auto-config available
    GearRecommendationEngine? gearRecommendation = null;
    if (config.IsAutoGenerated && config.AccelerationCurves != null && config.AccelerationCurves.Count > 0)
    {
        gearRecommendation = new GearRecommendationEngine(config.AccelerationCurves, config.GearRatios);
    }

    Console.Clear();
    Console.WriteLine("=== ACC RPM Monitor - Adaptive Mode ===");
    Console.WriteLine($"Vehicle: {configManager.CurrentVehicleName}");
    Console.WriteLine($"Mode: {configManager.CurrentMode} (Adaptive)");
    Console.WriteLine("Press ESC to exit | Press F2 to save learned config\n");

    // Audio profile selection
    Console.WriteLine("Select audio profile:");
    Console.WriteLine("  1. Normal (responsive tones)");
    Console.WriteLine("  2. Endurance (low-fatigue for long sessions)");
    Console.Write("Choice: ");
    var profileChoice = Console.ReadLine();
    var audioProfile = profileChoice == "2" ? DynAudioEng.AudioProfile.Endurance : DynAudioEng.AudioProfile.Normal;
    audioEngine.SetAudioProfile(audioProfile);
    audioEngine.SetMode(DynAudioEng.AudioMode.PerformanceLearning); // Use performance learning audio for adaptive mode
    Console.WriteLine();

    // Main loop state
    bool wasConnected = false;
    int readFailCount = 0;
    DateTime lastUpdate = DateTime.Now;
    const int UpdateIntervalSeconds = 15; // Update shift points every 15 seconds

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

// Performance Learning monitor mode - AI-driven shift optimization based on lap times
static void RunPerformanceLearningMonitor(ConfigMan configManager, GearRPMConfig config)
{
    // Initialize all required engines
    using var audioEngine = new DynAudioEng();
    audioEngine.SetMode(DynAudioEng.AudioMode.PerformanceLearning); // Use pitch-based guidance

    using var accMemory = new ACCSharedMemorySimple();

    // Audio profile will be selected after mode description

    var shiftAnalyzer = new OptimalShift(); // For physics-based analysis
    var shiftPatternAnalyzer = new PatternShift(); // For shift detection
    var learningEngine = new PerformanceEng(shiftPatternAnalyzer, shiftAnalyzer);
    var reportGenerator = new PatternShiftReport();

    // Initialize gear recommendation engine if available
    GearRecommendationEngine? gearRecommendation = null;
    if (config.IsAutoGenerated && config.AccelerationCurves != null && config.AccelerationCurves.Count > 0)
    {
        gearRecommendation = new GearRecommendationEngine(config.AccelerationCurves, config.GearRatios);
    }

    Console.Clear();
    Console.WriteLine("=== ACC RPM Monitor - Performance Learning Mode ===");
    Console.WriteLine($"Vehicle: {configManager.CurrentVehicleName}");
    Console.WriteLine("This mode uses machine learning to optimize shift points based on lap performance.");
    Console.WriteLine("The system builds confidence through statistical analysis of lap times vs shift patterns.");
    Console.WriteLine();
    Console.WriteLine("Controls:");
    Console.WriteLine("  ESC - Return to main menu (prompts to save)");
    Console.WriteLine("  F2  - Save current learned configuration");
    Console.WriteLine("  F3  - Generate performance report");
    Console.WriteLine();

    // Audio profile selection
    Console.WriteLine("Select audio profile:");
    Console.WriteLine("  1. Normal (responsive tones)");
    Console.WriteLine("  2. Endurance (low-fatigue for long sessions)");
    Console.Write("Choice: ");
    var profileChoice = Console.ReadLine();
    var audioProfile = profileChoice == "2" ? DynAudioEng.AudioProfile.Endurance : DynAudioEng.AudioProfile.Normal;
    audioEngine.SetAudioProfile(audioProfile);
    Console.WriteLine();

    // Main loop state
    bool wasConnected = false;
    int readFailCount = 0;
    DateTime lastUpdate = DateTime.Now;
    DateTime lastLearnUpdate = DateTime.Now;
    const int LearnIntervalSeconds = 15; // Update learned shift points every 15 seconds

    // Off-track detection state
    float lastLocalY = 0;
    const float OffTrackThreshold = 0.5f; // Threshold for detecting off-track

    Console.Clear();
    Console.WriteLine("=== ACC RPM Monitor - Performance Learning Mode ===");
    Console.WriteLine($"Vehicle: {configManager.CurrentVehicleName}");
    Console.WriteLine("Waiting for Assetto Corsa Competizione...\n");

    while (true)
    {
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
                string reportPath = reportGenerator.SaveShiftPatternReport(shiftReport, learningReport, configManager.CurrentVehicleName);

                Console.SetCursorPosition(0, 20);
                Console.WriteLine($"✓ Report saved to: {Path.GetFileName(reportPath)}                           ");
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

        // Detect off-track (simplified: using vertical position change)
        bool isOffTrack = false;
        if (position != null)
        {
            if (lastLocalY != 0)
            {
                float yDelta = Math.Abs(position.LocalY - lastLocalY);
                isOffTrack = yDelta > OffTrackThreshold; // Large vertical change suggests off-track
            }
            lastLocalY = position.LocalY;
        }

        // Convert gear for display (ACC uses gear 2 as first gear)
        int displayGear = currentGear - 1;

        // Update shift pattern analyzer
        if (currentGear >= 1 && lapTiming != null && position != null && displayGear >= 1)
        {
            shiftPatternAnalyzer.Update(
                displayGear,
                currentRPM,
                throttle,
                speed,
                position.NormalizedPosition,
                lapTiming,
                isOffTrack
            );

            // Also feed data to acceleration analyzer for physics-based learning
            shiftAnalyzer.AddDataPoint(currentRPM, throttle, speed, displayGear);
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

    if (shiftPatternAnalyzer.GetValidLaps() >= 2)
    {
        Console.WriteLine("Generating performance analysis report...");
        var shiftReport = shiftPatternAnalyzer.GeneratePerformanceReport();
        var learningReport = learningEngine.GenerateLearningReport();
        string reportPath = reportGenerator.SaveShiftPatternReport(shiftReport, learningReport, configManager.CurrentVehicleName);

        Console.WriteLine();
        Console.WriteLine(reportGenerator.GenerateConsoleSummary(learningReport));
        Console.WriteLine();
        Console.WriteLine($"Detailed report saved to:");
        Console.WriteLine($"  {reportPath}");
        Console.WriteLine();

        Console.Write("Save learned shift points to configuration? (Y/N): ");
        var saveChoice = Console.ReadKey();
        if (saveChoice.KeyChar == 'Y' || saveChoice.KeyChar == 'y')
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

// Headless mode: runs monitoring without GUI, exposes telemetry via IPC
static void RunHeadlessMode()
{
    Console.WriteLine("Starting headless mode...");
    Console.WriteLine("Telemetry available via:");
    Console.WriteLine("  - Named pipes: ACCRPMMonitor_Telemetry (local only)");
    Console.WriteLine("  - UDP: localhost:7777 (network capable)");
    Console.WriteLine();

    var configManager = new ConfigMan();
    var accMemory = new ACCSharedMemorySimple();
    var ipcServer = new IPCServer();
    ipcServer.Start();

    if (!accMemory.Connect())
    {
        Console.WriteLine("Error: Could not connect to ACC shared memory.");
        Console.WriteLine("Make sure ACC is running.");
        return;
    }

    var gearConfig = configManager.LoadConfig();
    var shiftAnalyzer = new PatternShift();
    var optimalShift = new OptimalShift();
    var performanceEngine = new PerformanceEng(shiftAnalyzer, optimalShift);
    var audioEngine = new DynAudioEng();

    Console.WriteLine("Connected to ACC. Publishing telemetry...");
    Console.WriteLine("Press Ctrl+C to stop.");

    try
    {
        while (true)
        {
            var telemetry = accMemory.ReadFullTelemetry();
            var lapTiming = accMemory.ReadLapTiming();
            var status = accMemory.ReadStatus();

            if (telemetry.HasValue && lapTiming != null && status.HasValue)
            {
                var (gear, rpm, throttle, speed) = telemetry.Value;

                // Create telemetry data for IPC
                var data = new TelemetryData
                {
                    Gear = gear,
                    RPM = rpm,
                    Throttle = throttle,
                    Speed = speed,
                    CurrentLapTime = lapTiming.CurrentLapTime,
                    LastLapTime = lapTiming.LastLapTime,
                    BestLapTime = lapTiming.BestLapTime,
                    CompletedLaps = lapTiming.CompletedLaps,
                    SessionStatus = status.Value,
                    IsValidLap = lapTiming.IsCurrentLapValid,
                    RecommendedShiftRPM = gearConfig.GetRPMForGear(gear)
                };

                // Publish to all connected clients
                ipcServer.PublishTelemetry(data);

                // Update audio
                if (status.Value == 2) // LIVE session
                {
                    audioEngine.UpdateRPM(rpm, data.RecommendedShiftRPM, gear);
                }
            }

            Thread.Sleep(50); // 20 Hz update rate
        }
    }
    catch (OperationCanceledException)
    {
        // Graceful shutdown on Ctrl+C
    }
    finally
    {
        ipcServer.Stop();
        accMemory.Dispose();
        audioEngine.Dispose();
        Console.WriteLine("Headless mode stopped.");
    }
}

// Viewer mode: connects to headless instance and displays telemetry
static void RunViewerMode(string serverAddress)
{
    Console.WriteLine($"Connecting to headless server at {serverAddress}...");

    var ipcClient = new IPCClient(serverAddress);
    var lastUpdate = DateTime.Now;

    ipcClient.TelemetryReceived += (sender, telemetry) =>
    {
        // Clear and redraw display every update
        if ((DateTime.Now - lastUpdate).TotalMilliseconds >= 100)
        {
            Console.Clear();
            Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║           ACC RPM MONITOR - VIEWER MODE                        ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
            Console.WriteLine();
            Console.WriteLine($"Gear:            {telemetry.Gear}                                           ");
            Console.WriteLine($"RPM:             {telemetry.RPM}                                    ");
            Console.WriteLine($"Throttle:        {telemetry.Throttle:P1}                                       ");
            Console.WriteLine($"Speed:           {telemetry.Speed:F1} km/h                                     ");
            Console.WriteLine();
            Console.WriteLine("─── LAP TIMING ─────────────────────────────────────────────────");
            Console.WriteLine($"Current Lap:     {telemetry.CurrentLapTime}                             ");
            Console.WriteLine($"Last Lap:        {telemetry.LastLapTime}                             ");
            Console.WriteLine($"Best Lap:        {telemetry.BestLapTime}                                ");
            Console.WriteLine($"Completed:       {telemetry.CompletedLaps} laps                                    ");
            Console.WriteLine();
            Console.WriteLine("─── STATUS ────────────────────────────────────────────────────────");
            Console.WriteLine($"Session:         {GetStatusName(telemetry.SessionStatus)}                                        ");
            Console.WriteLine($"Shift Point:     {telemetry.RecommendedShiftRPM} RPM                              ");
            Console.WriteLine($"Lap Valid:       {(telemetry.IsValidLap ? "Yes" : "No")}                                          ");
            Console.WriteLine($"Audio Profile:   {telemetry.AudioProfile}                                   ");
            Console.WriteLine();
            Console.WriteLine("Press Ctrl+C to disconnect.");

            lastUpdate = DateTime.Now;
        }
    };

    ipcClient.Connect();

    try
    {
        // Keep viewer running until user presses Ctrl+C
        while (ipcClient.IsConnected)
        {
            Thread.Sleep(100);
        }
    }
    catch (OperationCanceledException)
    {
        // Graceful shutdown
    }
    finally
    {
        ipcClient.Disconnect();
        Console.WriteLine("\nDisconnected from server.");
    }
}
