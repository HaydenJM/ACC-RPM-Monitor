using System.Text.Json;

namespace ACCRPMMonitor;

// Handles loading and saving of per-vehicle RPM configs
public class ConfigManager
{
    private readonly string _configsDirectory;
    private string _currentVehicleName;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public ConfigManager(string vehicleName = "default")
    {
        // Store configs in AppData\Local\ACCRPMMonitor\powercurves
        string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string appFolder = Path.Combine(appDataPath, "ACCRPMMonitor");
        _configsDirectory = Path.Combine(appFolder, "powercurves");

        Directory.CreateDirectory(_configsDirectory);

        _currentVehicleName = vehicleName;
    }

    // Loads config from file, or creates default if it doesn't exist
    public GearRPMConfig LoadConfig()
    {
        try
        {
            string configFilePath = GetConfigPath(_currentVehicleName);

            if (File.Exists(configFilePath))
            {
                string json = File.ReadAllText(configFilePath);
                var config = JsonSerializer.Deserialize<GearRPMConfig>(json);

                if (config != null)
                {
                    Console.WriteLine($"Configuration loaded from: {configFilePath}");
                    return config;
                }
            }

            // No config found - create and save default
            Console.WriteLine($"No configuration found. Creating default at: {configFilePath}");
            var defaultConfig = GearRPMConfig.CreateDefault();
            SaveConfig(defaultConfig);
            return defaultConfig;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading configuration: {ex.Message}");
            Console.WriteLine("Using default configuration.");
            return GearRPMConfig.CreateDefault();
        }
    }

    // Saves config to file
    public bool SaveConfig(GearRPMConfig config)
    {
        try
        {
            string configFilePath = GetConfigPath(_currentVehicleName);
            string json = JsonSerializer.Serialize(config, JsonOptions);
            File.WriteAllText(configFilePath, json);
            Console.WriteLine($"Configuration saved to: {configFilePath}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving configuration: {ex.Message}");
            return false;
        }
    }

    public string ConfigFilePath => GetConfigPath(_currentVehicleName);

    private string GetConfigPath(string vehicleName)
    {
        return Path.Combine(_configsDirectory, $"{vehicleName}.json");
    }

    public string CurrentVehicleName => _currentVehicleName;

    public void SetVehicle(string vehicleName)
    {
        _currentVehicleName = vehicleName;
    }

    // Lists all available vehicle configs in the directory
    public List<string> GetAvailableVehicles()
    {
        var files = Directory.GetFiles(_configsDirectory, "*.json");
        return files.Select(f => Path.GetFileNameWithoutExtension(f)).OrderBy(n => n).ToList();
    }

    public bool DeleteVehicle(string vehicleName)
    {
        try
        {
            string configPath = GetConfigPath(vehicleName);
            if (File.Exists(configPath))
            {
                File.Delete(configPath);
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    public bool VehicleExists(string vehicleName)
    {
        return File.Exists(GetConfigPath(vehicleName));
    }
}
