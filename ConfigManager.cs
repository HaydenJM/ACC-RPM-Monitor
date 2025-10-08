using System.Text.Json;

namespace ACCRPMMonitor;

/// <summary>
/// Manages loading and saving of RPM configuration
/// </summary>
public class ConfigManager
{
    private readonly string _configsDirectory;
    private string _currentVehicleName;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Initializes the ConfigManager with powercurves directory
    /// </summary>
    /// <param name="vehicleName">Name of the vehicle configuration to use</param>
    public ConfigManager(string vehicleName = "default")
    {
        // Store configs in AppData\Local\ACCRPMMonitor\powercurves
        string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string appFolder = Path.Combine(appDataPath, "ACCRPMMonitor");
        _configsDirectory = Path.Combine(appFolder, "powercurves");

        // Create directory if it doesn't exist
        Directory.CreateDirectory(_configsDirectory);

        _currentVehicleName = vehicleName;
    }

    /// <summary>
    /// Loads the configuration from file, or creates default if not found
    /// </summary>
    /// <returns>Loaded or default configuration</returns>
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

            // Create and save default config if file doesn't exist
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

    /// <summary>
    /// Saves the configuration to file
    /// </summary>
    /// <param name="config">Configuration to save</param>
    /// <returns>True if saved successfully, false otherwise</returns>
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

    /// <summary>
    /// Gets the full path to the configuration file for the current vehicle
    /// </summary>
    public string ConfigFilePath => GetConfigPath(_currentVehicleName);

    /// <summary>
    /// Gets the path for a specific vehicle config
    /// </summary>
    private string GetConfigPath(string vehicleName)
    {
        return Path.Combine(_configsDirectory, $"{vehicleName}.json");
    }

    /// <summary>
    /// Gets the current vehicle name
    /// </summary>
    public string CurrentVehicleName => _currentVehicleName;

    /// <summary>
    /// Sets the current vehicle name
    /// </summary>
    public void SetVehicle(string vehicleName)
    {
        _currentVehicleName = vehicleName;
    }

    /// <summary>
    /// Lists all available vehicle configurations
    /// </summary>
    public List<string> GetAvailableVehicles()
    {
        var files = Directory.GetFiles(_configsDirectory, "*.json");
        return files.Select(f => Path.GetFileNameWithoutExtension(f)).OrderBy(n => n).ToList();
    }

    /// <summary>
    /// Deletes a vehicle configuration
    /// </summary>
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

    /// <summary>
    /// Checks if a vehicle configuration exists
    /// </summary>
    public bool VehicleExists(string vehicleName)
    {
        return File.Exists(GetConfigPath(vehicleName));
    }
}
