namespace ACCRPMMonitor;

/// <summary>
/// Configuration class for storing RPM shift points per gear
/// </summary>
public class GearRPMConfig
{
    /// <summary>
    /// Dictionary mapping gear number to RPM shift point
    /// Key: Gear number (1-8), Value: RPM threshold for upshift
    /// </summary>
    public Dictionary<int, int> GearRPMThresholds { get; set; } = new();

    /// <summary>
    /// Creates a default configuration with preset RPM values
    /// </summary>
    /// <returns>Default configuration</returns>
    public static GearRPMConfig CreateDefault()
    {
        return new GearRPMConfig
        {
            GearRPMThresholds = new Dictionary<int, int>
            {
                { 1, 6000 },
                { 2, 6500 },
                { 3, 7000 },
                { 4, 7000 },
                { 5, 7000 },
                { 6, 7000 },
                { 7, 7000 },
                { 8, 7000 }
            }
        };
    }

    /// <summary>
    /// Gets the RPM threshold for a specific gear
    /// </summary>
    /// <param name="gear">Gear number</param>
    /// <returns>RPM threshold, or 0 if gear not configured</returns>
    public int GetRPMForGear(int gear)
    {
        return GearRPMThresholds.TryGetValue(gear, out var rpm) ? rpm : 0;
    }

    /// <summary>
    /// Sets the RPM threshold for a specific gear
    /// </summary>
    /// <param name="gear">Gear number</param>
    /// <param name="rpm">RPM threshold</param>
    public void SetRPMForGear(int gear, int rpm)
    {
        GearRPMThresholds[gear] = rpm;
    }
}
