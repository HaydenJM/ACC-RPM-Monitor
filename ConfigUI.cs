namespace ACCRPMMonitor;

// Simple console UI for managing vehicle configs and editing RPM thresholds
public static class ConfigUI
{
    // Main vehicle selection menu
    public static void ShowVehicleSelectionMenu(ConfigManager configManager)
    {
        while (true)
        {
            Console.Clear();
            Console.WriteLine("=== ACC RPM Monitor - Vehicle Selection ===\n");

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
                    Console.WriteLine($"  [{i + 1}] {vehicles[i]}{marker}");
                }
                Console.WriteLine();
            }

            Console.WriteLine($"  [N] Create New Vehicle Configuration");
            if (vehicles.Count > 0)
            {
                Console.WriteLine($"  [D] Delete Vehicle Configuration");
            }
            Console.WriteLine($"  [C] Continue with '{configManager.CurrentVehicleName}'");

            Console.Write("\nSelect option: ");
            string? input = Console.ReadLine()?.ToUpper();

            if (string.IsNullOrWhiteSpace(input))
                continue;

            if (input == "C")
            {
                return;
            }
            else if (input == "N")
            {
                CreateNewVehicle(configManager);
            }
            else if (input == "D" && vehicles.Count > 0)
            {
                DeleteVehicle(configManager, vehicles);
            }
            else if (int.TryParse(input, out int choice) && choice >= 1 && choice <= vehicles.Count)
            {
                configManager.SetVehicle(vehicles[choice - 1]);
                Console.WriteLine($"\nSwitched to vehicle: {vehicles[choice - 1]}");
                Thread.Sleep(1000);
                return;
            }
        }
    }

    // Creates a new vehicle config
    private static void CreateNewVehicle(ConfigManager configManager)
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

    // Deletes a vehicle config
    private static void DeleteVehicle(ConfigManager configManager, List<string> vehicles)
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
    public static void ShowConfigMenu(GearRPMConfig config, ConfigManager configManager)
    {
        while (true)
        {
            Console.Clear();
            Console.WriteLine("=== ACC RPM Monitor - Configuration ===\n");
            Console.WriteLine($"Vehicle: {configManager.CurrentVehicleName}");
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

            if (!int.TryParse(input, out int choice))
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
