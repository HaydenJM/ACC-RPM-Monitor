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

    // Reads comprehensive lap timing data including lap validity
    public LapTimingData? ReadLapTiming()
    {
        if (_graphicsMMF == null)
            return null;

        try
        {
            using var accessor = _graphicsMMF.CreateViewAccessor(0, 2048, MemoryMappedFileAccess.Read);

            // ACC Graphics structure offsets (based on SPageFileGraphic from ACC SDK)
            int packetId = accessor.ReadInt32(0);
            int status = accessor.ReadInt32(4);
            int sessionType = accessor.ReadInt32(8);

            // Read lap times as wide strings (wchar_t[15] each = 30 bytes)
            byte[] currentTimeBytes = new byte[30];
            byte[] lastTimeBytes = new byte[30];
            byte[] bestTimeBytes = new byte[30];

            accessor.ReadArray(12, currentTimeBytes, 0, 30);   // Offset 12
            accessor.ReadArray(42, lastTimeBytes, 0, 30);      // Offset 42
            accessor.ReadArray(72, bestTimeBytes, 0, 30);      // Offset 72
            // last_sector_time_str at offset 102 (skip)

            int completedLaps = accessor.ReadInt32(132);       // Offset 132: completed_lap
            int lastTime = accessor.ReadInt32(144);            // Offset 144: last_time (milliseconds)

            // Read is_valid_lap from offset 1408 (based on ACC shared memory structure)
            // This field indicates if the CURRENT lap (in progress) is valid
            // Works reliably in practice/qualifying, less reliable in races
            int isValidLap = accessor.ReadInt32(1408);         // Offset 1408: is_valid_lap

            return new LapTimingData
            {
                CurrentLapTime = ParseTimeString(currentTimeBytes),
                LastLapTime = ParseTimeString(lastTimeBytes),
                BestLapTime = ParseTimeString(bestTimeBytes),
                CompletedLaps = completedLaps,
                LastLapTimeMs = lastTime,
                IsCurrentLapValid = isValidLap != 0,
                Status = status,
                SessionType = sessionType
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
            // ACC uses wide strings (2 bytes per char, UTF-16)
            // Find the null terminator (0x00 0x00 for wide char)
            int nullIndex = -1;
            for (int i = 0; i < bytes.Length - 1; i += 2)
            {
                if (bytes[i] == 0 && bytes[i + 1] == 0)
                {
                    nullIndex = i;
                    break;
                }
            }

            // Decode only up to null terminator, or entire array if no null found
            int length = (nullIndex >= 0) ? nullIndex : bytes.Length;
            string result = System.Text.Encoding.Unicode.GetString(bytes, 0, length);

            return result.Length > 0 ? result : "00:00.000";
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
    public int LastLapTimeMs { get; set; }
    public int Status { get; set; }
    public int SessionType { get; set; }
    public bool IsCurrentLapValid { get; set; }

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
    public int BestLapTimeMs => ParseTimeToMs(BestLapTime);
}

// Position data for off-track detection
public class PositionData
{
    public float LocalX { get; set; }
    public float LocalY { get; set; }
    public float LocalZ { get; set; }
    public float NormalizedPosition { get; set; }
}
