namespace ACCRPMMonitor;

// Stores RPM shift points for each gear
public class GearRPMConfig
{
    // Maps gear number (1-8) to the RPM where you should upshift
    public Dictionary<int, int> GearRPMThresholds { get; set; } = new();

    // Creates a default config with reasonable RPM values
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

    public int GetRPMForGear(int gear)
    {
        return GearRPMThresholds.TryGetValue(gear, out var rpm) ? rpm : 0;
    }

    public void SetRPMForGear(int gear, int rpm)
    {
        GearRPMThresholds[gear] = rpm;
    }
}
