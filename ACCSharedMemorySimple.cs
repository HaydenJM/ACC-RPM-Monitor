using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace ACCRPMMonitor;

// Reads ACC telemetry from shared memory - just the essentials (gear and RPM)
public class ACCSharedMemorySimple : IDisposable
{
    private MemoryMappedFile? _physicsMMF;
    private MemoryMappedFile? _graphicsMMF;

    private const string PhysicsMMFName = "Local\\acpmf_physics";
    private const string GraphicsMMFName = "Local\\acpmf_graphics";

    // Attempts to connect to ACC's shared memory
    public bool Connect()
    {
        try
        {
            _physicsMMF = MemoryMappedFile.OpenExisting(PhysicsMMFName, MemoryMappedFileRights.Read);
            _graphicsMMF = MemoryMappedFile.OpenExisting(GraphicsMMFName, MemoryMappedFileRights.Read);
            return true;
        }
        catch (Exception ex)
        {
            LastError = $"Connection error: {ex.Message}";
            Dispose();
            return false;
        }
    }

    public bool IsConnected => _physicsMMF != null;

    // Reads just the gear and RPM from physics memory
    public (int gear, int rpm)? ReadGearAndRPM()
    {
        if (_physicsMMF == null)
            return null;

        try
        {
            using var accessor = _physicsMMF.CreateViewAccessor(0, 512, MemoryMappedFileAccess.Read);

            // ACC physics structure offsets: PacketId(4) + Gas(4) + Brake(4) + Fuel(4) + Gear(4) + RPM(4)
            int packetId = accessor.ReadInt32(0);
            int gear = accessor.ReadInt32(16);  // 16 bytes in
            int rpm = accessor.ReadInt32(20);   // 20 bytes in

            return (gear, rpm);
        }
        catch (Exception ex)
        {
            LastError = $"Read error: {ex.Message}";
            return null;
        }
    }

    // Reads full telemetry data including throttle and speed
    public (int gear, int rpm, float throttle, float speed)? ReadFullTelemetry()
    {
        if (_physicsMMF == null)
            return null;

        try
        {
            using var accessor = _physicsMMF.CreateViewAccessor(0, 512, MemoryMappedFileAccess.Read);

            // ACC physics structure offsets:
            // PacketId(4) + Gas(4) + Brake(4) + Fuel(4) + Gear(4) + RPM(4) + SteerAngle(4) + SpeedKmh(4)
            int packetId = accessor.ReadInt32(0);    // Offset 0
            float gas = accessor.ReadSingle(4);      // Offset 4 - Throttle position (0.0 to 1.0)
            int gear = accessor.ReadInt32(16);       // Offset 16 - Gear
            int rpm = accessor.ReadInt32(20);        // Offset 20 - RPM
            float speedKmh = accessor.ReadSingle(28); // Offset 28 - Speed in km/h (was incorrectly 24)

            return (gear, rpm, gas, speedKmh);
        }
        catch (Exception ex)
        {
            LastError = $"Read error: {ex.Message}";
            return null;
        }
    }

    // Reads ACC status from graphics memory (0=OFF, 1=REPLAY, 2=LIVE, 3=PAUSE)
    public int? ReadStatus()
    {
        if (_graphicsMMF == null)
            return null;

        try
        {
            using var accessor = _graphicsMMF.CreateViewAccessor(0, 128, MemoryMappedFileAccess.Read);

            // PacketId(4) + Status(4)
            int status = accessor.ReadInt32(4);
            return status;
        }
        catch (Exception ex)
        {
            LastError = $"Status read error: {ex.Message}";
            return null;
        }
    }

    // Reads the number of completed laps from graphics memory
    public int? ReadCompletedLaps()
    {
        if (_graphicsMMF == null)
            return null;

        try
        {
            using var accessor = _graphicsMMF.CreateViewAccessor(0, 256, MemoryMappedFileAccess.Read);

            // ACC Graphics structure: PacketId(4) + Status(4) + Session(4) + CurrentTime(16 chars) + LastTime(16 chars) + BestTime(16 chars) + Split(16 chars) + CompletedLaps(4)
            // Offset: 4 + 4 + 4 + 16 + 16 + 16 + 16 = 76 bytes
            int completedLaps = accessor.ReadInt32(76);
            return completedLaps;
        }
        catch (Exception ex)
        {
            LastError = $"Lap read error: {ex.Message}";
            return null;
        }
    }

    // Reads comprehensive lap timing data including validated laps
    public LapTimingData? ReadLapTiming()
    {
        if (_graphicsMMF == null)
            return null;

        try
        {
            using var accessor = _graphicsMMF.CreateViewAccessor(0, 1024, MemoryMappedFileAccess.Read);

            // ACC Graphics structure offsets
            int packetId = accessor.ReadInt32(0);
            int status = accessor.ReadInt32(4);
            int session = accessor.ReadInt32(8);

            // Read lap times as wide strings (16 chars each = 32 bytes)
            byte[] currentTimeBytes = new byte[32];
            byte[] lastTimeBytes = new byte[32];
            byte[] bestTimeBytes = new byte[32];

            accessor.ReadArray(12, currentTimeBytes, 0, 32);  // Offset 12: iCurrentTime (wchar_t[15])
            accessor.ReadArray(44, lastTimeBytes, 0, 32);     // Offset 44: iLastTime (wchar_t[15])
            accessor.ReadArray(76, bestTimeBytes, 0, 32);     // Offset 76: iBestTime (wchar_t[15])

            // Split (wchar_t[15]) at offset 108 - not used
            int completedLaps = accessor.ReadInt32(140);      // Offset 140: completedLaps
            int numberOfLaps = accessor.ReadInt32(144);       // Offset 144: numberOfLaps (this is the TOTAL laps in session, not validated)

            // The actual validated lap count needs different logic
            // We'll compare if iLastTime == iBestTime or if it's a valid time
            // For now, let's try reading from a different offset based on ACC SDK

            // Try reading from what might be the correct offset for session completed laps
            int sessionTimeLeft = accessor.ReadInt32(148);    // Offset 148: iSessionTimeLeft

            // Since numberOfLaps at 144 might be total laps in session, not validated laps,
            // we need to use a different approach: check if last lap time is valid
            bool lastLapWasValid = !string.IsNullOrWhiteSpace(ParseTimeString(lastTimeBytes)) &&
                                    ParseTimeString(lastTimeBytes) != "00:00.000";

            return new LapTimingData
            {
                CurrentLapTime = ParseTimeString(currentTimeBytes),
                LastLapTime = ParseTimeString(lastTimeBytes),
                BestLapTime = ParseTimeString(bestTimeBytes),
                CompletedLaps = completedLaps,
                ValidatedLaps = lastLapWasValid ? completedLaps : Math.Max(0, completedLaps - 1), // Estimate: if last lap has time, it's valid
                Status = status,
                LastLapWasInvalidated = !lastLapWasValid && completedLaps > 0
            };
        }
        catch (Exception ex)
        {
            LastError = $"Lap timing read error: {ex.Message}";
            return null;
        }
    }

    // Reads position data to detect off-track events
    public PositionData? ReadPosition()
    {
        if (_physicsMMF == null)
            return null;

        try
        {
            using var accessor = _physicsMMF.CreateViewAccessor(0, 1024, MemoryMappedFileAccess.Read);

            // Position data offsets in physics structure
            // LocalVelocity(12 bytes) + LocalAngularVelocity(12 bytes) + ...
            // WorldPosition is typically around offset 100+
            float localPosX = accessor.ReadSingle(52);    // Local position X
            float localPosY = accessor.ReadSingle(56);    // Local position Y
            float localPosZ = accessor.ReadSingle(60);    // Local position Z

            // Road position (normalized track position)
            float normalizedCarPosition = accessor.ReadSingle(112);  // 0.0 to 1.0 along track

            return new PositionData
            {
                LocalX = localPosX,
                LocalY = localPosY,
                LocalZ = localPosZ,
                NormalizedPosition = normalizedCarPosition
            };
        }
        catch (Exception ex)
        {
            LastError = $"Position read error: {ex.Message}";
            return null;
        }
    }

    // Helper to parse ACC time strings (wide char format)
    private string ParseTimeString(byte[] bytes)
    {
        try
        {
            // ACC uses wide strings (2 bytes per char)
            return System.Text.Encoding.Unicode.GetString(bytes).TrimEnd('\0');
        }
        catch
        {
            return "00:00.000";
        }
    }

    public string? LastError { get; private set; }

    public void Dispose()
    {
        _physicsMMF?.Dispose();
        _graphicsMMF?.Dispose();
        _physicsMMF = null;
        _graphicsMMF = null;
    }
}

// Lap timing information
public class LapTimingData
{
    public string CurrentLapTime { get; set; } = "00:00.000";
    public string LastLapTime { get; set; } = "00:00.000";
    public string BestLapTime { get; set; } = "00:00.000";
    public int CompletedLaps { get; set; }
    public int ValidatedLaps { get; set; }
    public int Status { get; set; }
    public bool LastLapWasInvalidated { get; set; }

    // Parse time string to milliseconds for comparison
    public int ParseTimeToMs(string timeStr)
    {
        try
        {
            // Format: "MM:SS.mmm" or "M:SS.mmm"
            var parts = timeStr.Split(':');
            if (parts.Length != 2) return int.MaxValue;

            int minutes = int.Parse(parts[0]);
            var secondParts = parts[1].Split('.');
            if (secondParts.Length != 2) return int.MaxValue;

            int seconds = int.Parse(secondParts[0]);
            int milliseconds = int.Parse(secondParts[1]);

            return (minutes * 60 * 1000) + (seconds * 1000) + milliseconds;
        }
        catch
        {
            return int.MaxValue; // Invalid time
        }
    }

    public int CurrentLapTimeMs => ParseTimeToMs(CurrentLapTime);
    public int LastLapTimeMs => ParseTimeToMs(LastLapTime);
    public int BestLapTimeMs => ParseTimeToMs(BestLapTime);

    // Check if the last completed lap was valid
    // Uses heuristic: if last lap has a valid time, it wasn't invalidated
    // NOTE: This is an approximation since ACC's validated_laps field location is unclear
    public bool WasLastLapValid()
    {
        if (LastLapWasInvalidated) return false;
        if (ValidatedLaps == CompletedLaps) return true;

        // Fallback: check if last lap time is valid
        return LastLapTimeMs < int.MaxValue && LastLapTimeMs > 0;
    }
}

// Position data for off-track detection
public class PositionData
{
    public float LocalX { get; set; }
    public float LocalY { get; set; }
    public float LocalZ { get; set; }
    public float NormalizedPosition { get; set; }
}
