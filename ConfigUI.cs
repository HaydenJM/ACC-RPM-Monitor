namespace ACCRPMMonitor;

// Simple console UI for managing vehicle configs and editing RPM thresholds
public static class ConfigUI
{
    // Main menu - entry point for the application
    public static MainMenuChoice ShowMainMenu(ConfigMan configManager)
    {
        while (true)
        {
            Console.Clear();
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine("ACC RPM MONITOR");
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine();
            Console.WriteLine($"Current Vehicle: {configManager.CurrentVehicleName}");
            Console.WriteLine();
            Console.WriteLine("Main Menu:");
            Console.WriteLine();
            Console.WriteLine("  [1] Create Auto Configuration (Data Collection)");
            Console.WriteLine("      Perform a hotlap and let the app learn optimal shift points");
            Console.WriteLine();
            Console.WriteLine("  [2] Create/Edit Manual Configuration");
            Console.WriteLine("      Manually define RPM shift points for each gear");
            Console.WriteLine();
            Console.WriteLine("  [3] Select & Use Configuration (Start Monitoring)");
            Console.WriteLine("      Choose a config and start the RPM monitor");
            Console.WriteLine();
            Console.WriteLine("  [4] Change Vehicle");
            Console.WriteLine("      Switch to a different vehicle");
            Console.WriteLine();
            Console.WriteLine("  [5] Open Data Folder");
            Console.WriteLine("      Open the data folder (configs, reports, graphs) in File Explorer");
            Console.WriteLine();
            Console.WriteLine("  [6] Help");
            Console.WriteLine("      Learn how to use the application");
            Console.WriteLine();
            Console.WriteLine("  [7] Exit");
            Console.WriteLine();
            Console.WriteLine("Press ESC to exit application");
            Console.WriteLine();

            Console.Write("Select option (1-7): ");
            string? input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
                continue;

            if (input.Equals("ESC", StringComparison.OrdinalIgnoreCase))
            {
                return MainMenuChoice.Exit;
            }

            // Check for number keys
            if (int.TryParse(input, out int choice) && choice >= 1 && choice <= 7)
            {
                return (MainMenuChoice)choice;
            }
        }
    }

    // Main vehicle selection menu
    public static void ShowVehicleSelectionMenu(ConfigMan configManager)
    {
        while (true)
        {
            Console.Clear();
            Console.WriteLine("=== ACC RPM Monitor - Vehicle Selection ===\n");

            // Try to detect current vehicle from ACC
            using var vehicleDetector = new VehicleDetector();
            string? detectedVehicle = null;
            if (vehicleDetector.Connect())
            {
                detectedVehicle = vehicleDetector.GetCarModel();
                if (!string.IsNullOrEmpty(detectedVehicle))
                {
                    Console.WriteLine($"Detected from ACC: {detectedVehicle}");
                    Console.WriteLine();
                }
            }

            var vehicles = configManager.GetAvailableVehicles();

            if (vehicles.Count == 0)
            {
                Console.WriteLine("No vehicle configurations found.\n");
            }
            else
            {
                Console.WriteLine("Available Vehicles:");
                for (int i = 0; i < vehicles.Count; i++)
                {
                    string marker = vehicles[i] == configManager.CurrentVehicleName ? " (current)" : "";
                    string detectedMarker = vehicles[i] == detectedVehicle ? " [detected]" : "";
                    Console.WriteLine($"  [{i + 1}] {vehicles[i]}{marker}{detectedMarker}");
                }
                Console.WriteLine();
            }

            Console.WriteLine($"  [A] Auto-select detected vehicle");
            Console.WriteLine($"  [N] Create New Vehicle Configuration");
            if (vehicles.Count > 0)
            {
                Console.WriteLine($"  [D] Delete Vehicle Configuration");
            }
            Console.WriteLine($"  [C] Continue with '{configManager.CurrentVehicleName}'");

            Console.Write("\nSelect option: ");
            string? input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
                continue;

            char inputChar = char.ToUpper(input[0]);

            if (inputChar == 'C')
            {
                return;
            }
            else if (inputChar == 'A' && !string.IsNullOrEmpty(detectedVehicle))
            {
                // Auto-select detected vehicle
                configManager.SetVehicle(detectedVehicle);
                Console.WriteLine($"\nSwitched to detected vehicle: {detectedVehicle}");
                Thread.Sleep(1000);
                return;
            }
            else if (inputChar == 'N')
            {
                CreateNewVehicle(configManager);
            }
            else if (inputChar == 'D' && vehicles.Count > 0)
            {
                DeleteVehicle(configManager, vehicles);
            }
            else if (char.IsDigit(inputChar) && int.TryParse(inputChar.ToString(), out int choice) && choice >= 1 && choice <= vehicles.Count)
            {
                configManager.SetVehicle(vehicles[choice - 1]);
                Console.WriteLine($"\nSwitched to vehicle: {vehicles[choice - 1]}");
                Thread.Sleep(1000);
                return;
            }
        }
    }

    // Configuration mode selection menu
    public static void ShowModeSelectionMenu(ConfigMan configManager)
    {
        while (true)
        {
            Console.Clear();
            Console.WriteLine("=== ACC RPM Monitor - Configuration Mode ===\n");
            Console.WriteLine($"Vehicle: {configManager.CurrentVehicleName}\n");

            Console.WriteLine("Select configuration mode:\n");
            Console.WriteLine("  [1] Manual Configuration");
            Console.WriteLine("      Use custom RPM values that you define");
            Console.WriteLine();

            if (configManager.HasAutoConfig())
            {
                Console.WriteLine("  [2] Auto-Generated Configuration ✓");
                Console.WriteLine("      Use optimal shift points detected from your driving");
            }
            else
            {
                Console.WriteLine("  [2] Auto-Generated Configuration");
                Console.WriteLine("      (No auto config available yet - will be generated during driving)");
            }

            Console.WriteLine();
            Console.WriteLine($"Current mode: {configManager.CurrentMode}");

            Console.Write("\nSelect mode (1-2): ");
            string? input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
                continue;

            if (int.TryParse(input, out int choice))
            {
                if (choice == 1)
                {
                    configManager.SetMode(ConfigMode.Manual);
                    Console.WriteLine("\nManual configuration mode selected.");
                    Thread.Sleep(1000);
                    return;
                }
                else if (choice == 2)
                {
                    configManager.SetMode(ConfigMode.Auto);
                    if (!configManager.HasAutoConfig())
                    {
                        Console.WriteLine("\nAuto mode selected. Optimal shift points will be");
                        Console.WriteLine("automatically detected as you drive.");
                    }
                    else
                    {
                        Console.WriteLine("\nAuto-generated configuration loaded.");
                    }
                    Thread.Sleep(1500);
                    return;
                }
            }
        }
    }

    // Creates a new vehicle config
    private static void CreateNewVehicle(ConfigMan configManager)
    {
        Console.Write("\nEnter vehicle name: ");
        string? vehicleName = Console.ReadLine()?.Trim();

        if (string.IsNullOrWhiteSpace(vehicleName))
        {
            Console.WriteLine("Invalid vehicle name.");
            Thread.Sleep(1500);
            return;
        }

        // Clean up any invalid filename characters
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            vehicleName = vehicleName.Replace(c, '_');
        }

        if (configManager.VehicleExists(vehicleName))
        {
            Console.WriteLine($"Vehicle '{vehicleName}' already exists.");
            Thread.Sleep(1500);
            return;
        }

        configManager.SetVehicle(vehicleName);
        var config = GearRPMConfig.CreateDefault();
        configManager.SaveConfig(config);
        Console.WriteLine($"\nCreated new vehicle configuration: {vehicleName}");
        Thread.Sleep(1500);
    }

    // Displays help and usage information
    public static void ShowHelpMenu()
    {
        while (true)
        {
            Console.Clear();
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine("ACC RPM MONITOR - HELP & USAGE GUIDE");
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine();
            Console.WriteLine("QUICK START");
            Console.WriteLine("-----------");
            Console.WriteLine("1. Create Auto Configuration: Perform a hotlap to learn optimal shift points");
            Console.WriteLine("2. Select & Use Configuration: Choose your config and start monitoring");
            Console.WriteLine("3. Listen to audio feedback while driving");
            Console.WriteLine("4. Press ESC to return to main menu");
            Console.WriteLine();
            Console.WriteLine("WORKFLOW OVERVIEW");
            Console.WriteLine("-----------------");
            Console.WriteLine();
            Console.WriteLine("[1] CREATE AUTO CONFIGURATION");
            Console.WriteLine("    - Best for learning optimal shift points");
            Console.WriteLine("    - Collect data during a hotlap session (Monza or Paul Ricard recommended)");
            Console.WriteLine("    - Press F1 to start data collection");
            Console.WriteLine("    - Drive smoothly at high throttle through gears 1-5");
            Console.WriteLine("    - Press F1 to stop when finished");
            Console.WriteLine("    - Review the detailed report showing confidence for each gear");
            Console.WriteLine("    - Power curve graphs are automatically generated");
            Console.WriteLine();
            Console.WriteLine("[2] CREATE/EDIT MANUAL CONFIGURATION");
            Console.WriteLine("    - Set custom shift points for each gear (1-8)");
            Console.WriteLine("    - Edit any gear individually");
            Console.WriteLine("    - Save your configuration");
            Console.WriteLine();
            Console.WriteLine("[3] SELECT & USE CONFIGURATION (START MONITORING)");
            Console.WriteLine("    - Choose between Manual or Auto configuration");
            Console.WriteLine("    - Select monitoring mode:");
            Console.WriteLine("      • Standard: Fixed shift points with progressive beeping");
            Console.WriteLine("      • Adaptive: Continuously learns every 15 seconds using pitch-based audio feedback");
            Console.WriteLine("      • Performance Learning: Machine learning optimization from lap times (requires 3+ valid laps)");
            Console.WriteLine();
            Console.WriteLine("AUDIO FEEDBACK GUIDE");
            Console.WriteLine("--------------------");
            Console.WriteLine();
            Console.WriteLine("STANDARD MODE:");
            Console.WriteLine("  - Progressive beeping pattern for shift point guidance");
            Console.WriteLine("  - Slow beeps (500ms): Far from shift point");
            Console.WriteLine("  - Beeps accelerate: Getting closer to shift point");
            Console.WriteLine("  - Fast beeps (50ms): Very close to shift point");
            Console.WriteLine("  - Solid tone: At or just past optimal shift point");
            Console.WriteLine("  - No audio below 6000 RPM (prevents unnecessary noise)");
            Console.WriteLine();
            Console.WriteLine("ADAPTIVE & PERFORMANCE LEARNING MODES:");
            Console.WriteLine("  Both modes use pitch-based guidance with two selectable profiles:");
            Console.WriteLine();
            Console.WriteLine("  NORMAL PROFILE (Responsive tones):");
            Console.WriteLine("    - High pitch (950 Hz): Shift earlier (you're shifting too late)");
            Console.WriteLine("    - Mid pitch (600 Hz): Shifting at optimal point (±175 RPM)");
            Console.WriteLine("    - Low pitch (400 Hz): Shift later (you're shifting too early)");
            Console.WriteLine();
            Console.WriteLine("  ENDURANCE PROFILE (Low-fatigue for long sessions):");
            Console.WriteLine("    - Higher-mid pitch (650 Hz): Shift earlier (all-sine waveform)");
            Console.WriteLine("    - Mid pitch (500 Hz): Shifting at optimal point (all-sine waveform)");
            Console.WriteLine("    - Low pitch (400 Hz): Shift later with descending glide (all-sine waveform)");
            Console.WriteLine("    - Designed to reduce listener fatigue during extended practice sessions");
            Console.WriteLine();
            Console.WriteLine("  Audio profiles are selected when you enter Adaptive or Performance Learning mode");
            Console.WriteLine();
            Console.WriteLine("CONFIGURATION STORAGE");
            Console.WriteLine("---------------------");
            Console.WriteLine("All configurations, reports, and power curve graphs are stored in:");
            Console.WriteLine("%LocalAppData%\\ACCRPMMonitor\\");
            Console.WriteLine();
            Console.WriteLine("Use [5] Open Data Folder from main menu for quick access.");
            Console.WriteLine();
            Console.WriteLine("KEY CONTROLS");
            Console.WriteLine("-------------");
            Console.WriteLine("F1 (during data collection): Start/Stop data collection");
            Console.WriteLine("F2 (during Adaptive/Performance Learning): Save learned shift points");
            Console.WriteLine("F3 (during Adaptive/Performance Learning): Generate performance report");
            Console.WriteLine("ESC: Return to main menu or exit application");
            Console.WriteLine();
            Console.WriteLine("AUDIO PROFILE SELECTION:");
            Console.WriteLine("  When entering Adaptive or Performance Learning mode, select your audio profile:");
            Console.WriteLine("  [1] Normal - Responsive tones for immediate feedback (practice/qualifying)");
            Console.WriteLine("  [2] Endurance - Low-fatigue tones for extended sessions (long races)");
            Console.WriteLine();
            Console.WriteLine("TROUBLESHOOTING");
            Console.WriteLine("---------------");
            Console.WriteLine();
            Console.WriteLine("No vehicle detected?");
            Console.WriteLine("  - Make sure ACC is running and you're in a session (not in menus)");
            Console.WriteLine("  - The app detects your vehicle automatically");
            Console.WriteLine();
            Console.WriteLine("Data collection not working?");
            Console.WriteLine("  - Ensure you're on track (not in pits or paddock)");
            Console.WriteLine("  - Hold at least 85% throttle");
            Console.WriteLine("  - Travel faster than 5 km/h");
            Console.WriteLine("  - Data must be collected sequentially for gears 1-5");
            Console.WriteLine();
            Console.WriteLine("No audio feedback?");
            Console.WriteLine("  - Check Windows volume and application volume");
            Console.WriteLine("  - Audio only plays when RPM is above 6000");
            Console.WriteLine("  - Ensure audio device is connected and working");
            Console.WriteLine();
            Console.WriteLine("Performance Learning/Adaptive mode not generating results?");
            Console.WriteLine("  - Minimum 2 valid laps required for performance analysis report");
            Console.WriteLine("  - Minimum 3 valid laps required for shift point adjustment/saving");
            Console.WriteLine("  - A lap is invalid if: off-track time ≥3.0 seconds cumulative");
            Console.WriteLine("  - Drive smoothly and stay mostly on track to ensure valid laps");
            Console.WriteLine();
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
            return;
        }
    }

    // Deletes a vehicle config
    private static void DeleteVehicle(ConfigMan configManager, List<string> vehicles)
    {
        Console.Write("\nEnter vehicle number to delete (or press Enter to cancel): ");
        string? input = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(input))
            return;

        if (int.TryParse(input, out int choice) && choice >= 1 && choice <= vehicles.Count)
        {
            string vehicleToDelete = vehicles[choice - 1];

            if (vehicleToDelete == configManager.CurrentVehicleName)
            {
                Console.WriteLine("Cannot delete the currently selected vehicle.");
                Thread.Sleep(1500);
                return;
            }

            Console.Write($"Are you sure you want to delete '{vehicleToDelete}'? (Y/N): ");
            string? confirm = Console.ReadLine()?.ToUpper();

            if (confirm == "Y")
            {
                if (configManager.DeleteVehicle(vehicleToDelete))
                {
                    Console.WriteLine($"\nDeleted vehicle configuration: {vehicleToDelete}");
                }
                else
                {
                    Console.WriteLine($"\nFailed to delete vehicle configuration.");
                }
                Thread.Sleep(1500);
            }
        }
    }

    // Main config menu - lets you edit RPM thresholds for each gear
    public static void ShowConfigMenu(GearRPMConfig config, ConfigMan configManager)
    {
        // Auto mode configs shouldn't be edited manually
        if (configManager.CurrentMode == ConfigMode.Auto)
        {
            Console.Clear();
            Console.WriteLine("=== ACC RPM Monitor - Auto Configuration ===\n");
            Console.WriteLine("You are in Auto mode. Configuration is generated automatically");
            Console.WriteLine("from your driving data and cannot be edited manually.\n");
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
            return;
        }

        while (true)
        {
            Console.Clear();
            Console.WriteLine("=== ACC RPM Monitor - Configuration ===\n");
            Console.WriteLine($"Vehicle: {configManager.CurrentVehicleName}");
            Console.WriteLine($"Mode: {configManager.CurrentMode}");
            Console.WriteLine($"Config file: {configManager.ConfigFilePath}\n");

            Console.WriteLine("Current RPM Thresholds:");
            Console.WriteLine("(RPM value is where you should shift UP to next gear)\n");

            // Display all gear thresholds
            for (int gear = 1; gear <= 8; gear++)
            {
                int rpm = config.GetRPMForGear(gear);
                string nextGear = gear < 8 ? $"Gear {gear + 1}" : "Max Gear";
                Console.WriteLine($"  [{gear}] Gear {gear} Threshold --> {nextGear}: {rpm} RPM");
            }

            Console.WriteLine("\n  [9] Save and Exit");
            Console.WriteLine("  [0] Exit without Saving");

            Console.Write("\nSelect gear to edit (0-9): ");
            string? input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input) || !int.TryParse(input, out int choice))
                continue;

            if (choice == 0)
            {
                return;
            }
            else if (choice == 9)
            {
                configManager.SaveConfig(config);
                Console.WriteLine("\nConfiguration saved!");
                Thread.Sleep(1000);
                return;
            }
            else if (choice >= 1 && choice <= 8)
            {
                EditGearThreshold(config, choice);
            }
        }
    }

    // Edits a single gear's RPM threshold
    private static void EditGearThreshold(GearRPMConfig config, int gear)
    {
        string nextGear = gear < 8 ? $"Gear {gear + 1}" : "Max Gear";
        int currentRPM = config.GetRPMForGear(gear);

        Console.WriteLine($"\nEditing: Gear {gear} Threshold --> {nextGear}");
        Console.WriteLine($"Current value: {currentRPM} RPM");
        Console.Write("Enter new RPM value (or press Enter to cancel): ");

        string? input = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(input))
            return;

        if (int.TryParse(input, out int newRPM) && newRPM > 0)
        {
            config.SetRPMForGear(gear, newRPM);
            Console.WriteLine($"✓ Gear {gear} threshold updated to {newRPM} RPM");
            Thread.Sleep(1000);
        }
        else
        {
            Console.WriteLine("Invalid RPM value. Press any key to continue...");
            Console.ReadKey();
        }
    }
}

// Main menu options enum
public enum MainMenuChoice
{
    CreateAutoConfig = 1,
    CreateManualConfig = 2,
    SelectAndUseConfig = 3,
    ChangeVehicle = 4,
    OpenConfigFolder = 5,
    Help = 6,
    Exit = 7
}
