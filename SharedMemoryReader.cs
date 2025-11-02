using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace ACCRPMMonitor;

/// <summary>
/// ACC Shared Memory API - Reads telemetry from Assetto Corsa Competizione
///
/// ACC exposes telemetry through three named memory-mapped files:
/// - "Local\\acpmf_physics"  - Real-time physics data (67 fields): car dynamics, inputs, wheels, temperatures
/// - "Local\\acpmf_graphics" - Graphics/UI data (106 fields): lap times, session info, flags, weather
/// - "Local\\acpmf_static"   - Static session data (40 fields): track, car model, player info, aids
///
/// IMPLEMENTATION NOTES:
/// - Current implementation reads fields by manual offset calculation
/// - RECOMMENDED: Map complete structs using [StructLayout(LayoutKind.Sequential)] for reliability
/// - ACC uses metric units: BAR for pressure, km/h for speed, Celsius for temperature
/// - Conversions applied: BAR → PSI (×14.5038)
///
/// See physics_map.txt, graphics_map.txt, statics_map.txt for complete field definitions
/// </summary>
public class SharedMemoryReader : IDisposable
{
    private MemoryMappedFile? _physicsMMF;
    private MemoryMappedFile? _graphicsMMF;
    private int _detectedMaxGear = 6; // Track highest gear seen (GT3=6, Road cars=6-7, etc.)

    // ACC named memory-mapped files (created by ACC when game is running)
    private const string PhysicsMMFName = "Local\\acpmf_physics";
    private const string GraphicsMMFName = "Local\\acpmf_graphics";
    private const string StaticsMMFName = "Local\\acpmf_static"; // Not yet implemented

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

    /// <summary>
    /// Gets the highest gear detected during this session.
    /// Dynamically detects vehicle's max gear (GT3=6, Road cars may be 6-7, etc.)
    /// </summary>
    public int GetDetectedMaxGear() => _detectedMaxGear;

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

            // Track highest gear seen (dynamic max gear detection)
            if (gear > _detectedMaxGear)
                _detectedMaxGear = gear;

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

            // Track highest gear seen (dynamic max gear detection)
            if (gear > _detectedMaxGear)
                _detectedMaxGear = gear;

            return (gear, rpm, gas, speedKmh);
        }
        catch (Exception ex)
        {
            LastError = $"Read error: {ex.Message}";
            return null;
        }
    }

    // Reads wheel pressure and tire core temperature data
    public WheelAndTireData? ReadWheelAndTireData()
    {
        if (_physicsMMF == null)
        {
            Console.WriteLine("[DEBUG] ReadWheelAndTireData: _physicsMMF is null");
            return null;
        }

        try
        {
            using var accessor = _physicsMMF.CreateViewAccessor(0, 2048, MemoryMappedFileAccess.Read);

            // Based on reference Physics.cs struct layout:
            // PacketId(4) + Gas(4) + Brake(4) + Fuel(4) + Gear(4) + Rpms(4) + SteerAngle(4) + SpeedKmh(4) = 32 bytes
            // Velocity[3](12) + AccG[3](12) + WheelSlip[4](16) + WheelLoad[4](16) = 56 bytes
            // Total before WheelPressure = 88 bytes
            // WheelPressure[4] at offset 88 (4 floats = 16 bytes)
            // WheelAngularSpeed[4](16) + TyreWear[4](16) + TyreDirtyLevel[4](16) = 48 bytes
            // TyreCoreTemp[4] at offset 88 + 16 + 48 = 152 bytes

            int wheelPressureOffset = 88;  // 4 floats: FL, FR, RL, RR
            int tyreTempOffset = 152;       // 4 floats: FL, FR, RL, RR

            float[] wheelPressure = new float[4];
            float[] tyreCoreTemp = new float[4];

            // Read wheel pressures (already in PSI)
            for (int i = 0; i < 4; i++)
            {
                wheelPressure[i] = accessor.ReadSingle(wheelPressureOffset + (i * 4));
            }

            // Read tire core temperatures (in Celsius)
            for (int i = 0; i < 4; i++)
            {
                tyreCoreTemp[i] = accessor.ReadSingle(tyreTempOffset + (i * 4));
            }

            // Debug output - show raw values
            Console.WriteLine($"[DEBUG] Tire Data - Pressure: FL={wheelPressure[0]:F1} FR={wheelPressure[1]:F1} RL={wheelPressure[2]:F1} RR={wheelPressure[3]:F1}");
            Console.WriteLine($"[DEBUG] Tire Data - Temp: FL={tyreCoreTemp[0]:F1} FR={tyreCoreTemp[1]:F1} RL={tyreCoreTemp[2]:F1} RR={tyreCoreTemp[3]:F1}");

            // Check if all values are zero (likely wrong offsets)
            if (wheelPressure[0] == 0 && wheelPressure[1] == 0 && wheelPressure[2] == 0 && wheelPressure[3] == 0 &&
                tyreCoreTemp[0] == 0 && tyreCoreTemp[1] == 0 && tyreCoreTemp[2] == 0 && tyreCoreTemp[3] == 0)
            {
                Console.WriteLine("[DEBUG] All values are zero - possibly wrong offsets or ACC not in session");
            }

            return new WheelAndTireData
            {
                WheelPressureFL = wheelPressure[0],
                WheelPressureFR = wheelPressure[1],
                WheelPressureRL = wheelPressure[2],
                WheelPressureRR = wheelPressure[3],
                TyreCoreTempFL = tyreCoreTemp[0],
                TyreCoreTempFR = tyreCoreTemp[1],
                TyreCoreTempRL = tyreCoreTemp[2],
                TyreCoreTempRR = tyreCoreTemp[3]
            };
        }
        catch (Exception ex)
        {
            LastError = $"Wheel/Tire data read error: {ex.Message}";
            Console.WriteLine($"[DEBUG] Exception in ReadWheelAndTireData: {ex.Message}");
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
            // Try using the new struct-based API first (more reliable)
            var graphics = ReadGraphicsStruct();
            if (graphics.HasValue)
            {
                var g = graphics.Value;

                // Use the struct's string fields, but fall back to formatting from ms values if strings are empty/invalid
                string currentTimeStr = FormatTimeStringOrUseMs(g.CurrentTime, g.CurrentTimeMs);
                string lastTimeStr = FormatTimeStringOrUseMs(g.LastTime, g.LastTimeMs);
                string bestTimeStr = FormatTimeStringOrUseMs(g.BestTime, g.BestTimeMs);

                return new LapTimingData
                {
                    CurrentLapTime = currentTimeStr,
                    LastLapTime = lastTimeStr,
                    BestLapTime = bestTimeStr,
                    CompletedLaps = g.CompletedLaps,
                    LastLapTimeMs = g.LastTimeMs,
                    IsCurrentLapValid = g.IsValidLap,
                    Status = g.Status,
                    SessionType = g.SessionType,
                    SessionTimeLeft = g.SessionTimeLeft
                };
            }

            // Fallback to old offset-based reading if struct reading fails
            using var accessor = _graphicsMMF.CreateViewAccessor(0, 2048, MemoryMappedFileAccess.Read);

            int status = accessor.ReadInt32(4);
            int sessionType = accessor.ReadInt32(8);

            // Read lap times as wide strings (wchar_t[15] each = 30 bytes)
            byte[] currentTimeBytes = new byte[30];
            byte[] lastTimeBytes = new byte[30];
            byte[] bestTimeBytes = new byte[30];

            accessor.ReadArray(12, currentTimeBytes, 0, 30);
            accessor.ReadArray(42, lastTimeBytes, 0, 30);
            accessor.ReadArray(72, bestTimeBytes, 0, 30);

            int completedLaps = accessor.ReadInt32(132);
            int currentTimeMs = accessor.ReadInt32(136);
            int lastTimeMs = accessor.ReadInt32(144);
            int bestTimeMs = accessor.ReadInt32(148);
            int isValidLap = accessor.ReadInt32(1408);

            // Parse strings and use ms values as fallback
            var currentLapStr = FormatTimeStringOrUseMs(ParseTimeString(currentTimeBytes), currentTimeMs);
            var lastLapStr = FormatTimeStringOrUseMs(ParseTimeString(lastTimeBytes), lastTimeMs);
            var bestLapStr = FormatTimeStringOrUseMs(ParseTimeString(bestTimeBytes), bestTimeMs);

            return new LapTimingData
            {
                CurrentLapTime = currentLapStr,
                LastLapTime = lastLapStr,
                BestLapTime = bestLapStr,
                CompletedLaps = completedLaps,
                LastLapTimeMs = lastTimeMs,
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

    // Helper method: Use the string if valid, otherwise format from milliseconds
    private string FormatTimeStringOrUseMs(string timeStr, int timeMs)
    {
        // If string is valid (not empty and contains colon), use it
        if (!string.IsNullOrEmpty(timeStr) && timeStr.Contains(":") && timeStr != "00:00.000")
        {
            return timeStr;
        }

        // Otherwise format from milliseconds
        if (timeMs > 0)
        {
            int totalSeconds = timeMs / 1000;
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            int milliseconds = timeMs % 1000;
            return $"{minutes}:{seconds:D2}.{milliseconds:D3}";
        }

        return "00:00.000";
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

            // Validate the result looks like a time (MM:SS.mmm format)
            if (result.Length > 0 && result.Contains(":"))
            {
                // Check if it's a reasonable time format
                // Valid times should have format like "1:23.456" or "12:34.567"
                // and minutes should be reasonable (< 999)
                var parts = result.Split(':');
                if (parts.Length == 2 && int.TryParse(parts[0], out int minutes) && minutes < 999)
                {
                    return result;
                }
            }

            return "00:00.000";
        }
        catch
        {
            return "00:00.000";
        }
    }

    public string? LastError { get; private set; }

    // ==================== STRUCT-BASED API (Recommended Approach) ====================

    /// <summary>
    /// Reads the complete physics structure using proper struct mapping.
    /// This is more reliable than manual offset reading.
    /// </summary>
    public ACCPhysics? ReadPhysicsStruct()
    {
        if (_physicsMMF == null)
            return null;

        try
        {
            using var accessor = _physicsMMF.CreateViewAccessor(0, Marshal.SizeOf<ACCPhysics>(), MemoryMappedFileAccess.Read);

            byte[] buffer = new byte[Marshal.SizeOf<ACCPhysics>()];
            accessor.ReadArray(0, buffer, 0, buffer.Length);

            GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                var physics = Marshal.PtrToStructure<ACCPhysics>(handle.AddrOfPinnedObject());
                return physics;
            }
            finally
            {
                handle.Free();
            }
        }
        catch (Exception ex)
        {
            LastError = $"Physics struct read error: {ex.Message}";
            return null;
        }
    }

    /// <summary>
    /// Reads the complete graphics structure using proper struct mapping.
    /// This is more reliable than manual offset reading.
    /// </summary>
    public ACCGraphics? ReadGraphicsStruct()
    {
        if (_graphicsMMF == null)
            return null;

        try
        {
            using var accessor = _graphicsMMF.CreateViewAccessor(0, Marshal.SizeOf<ACCGraphics>(), MemoryMappedFileAccess.Read);

            byte[] buffer = new byte[Marshal.SizeOf<ACCGraphics>()];
            accessor.ReadArray(0, buffer, 0, buffer.Length);

            GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                var graphics = Marshal.PtrToStructure<ACCGraphics>(handle.AddrOfPinnedObject());
                return graphics;
            }
            finally
            {
                handle.Free();
            }
        }
        catch (Exception ex)
        {
            LastError = $"Graphics struct read error: {ex.Message}";
            return null;
        }
    }

    /// <summary>
    /// Reads the complete statics structure using proper struct mapping.
    /// This provides session information like track name, max RPM, player info.
    /// </summary>
    public ACCStatics? ReadStaticsStruct()
    {
        try
        {
            // Open statics memory-mapped file
            using var staticsMMF = MemoryMappedFile.OpenExisting(StaticsMMFName, MemoryMappedFileRights.Read);
            using var accessor = staticsMMF.CreateViewAccessor(0, Marshal.SizeOf<ACCStatics>(), MemoryMappedFileAccess.Read);

            byte[] buffer = new byte[Marshal.SizeOf<ACCStatics>()];
            accessor.ReadArray(0, buffer, 0, buffer.Length);

            GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                var statics = Marshal.PtrToStructure<ACCStatics>(handle.AddrOfPinnedObject());
                return statics;
            }
            finally
            {
                handle.Free();
            }
        }
        catch (Exception ex)
        {
            LastError = $"Statics struct read error: {ex.Message}";
            return null;
        }
    }

    /// <summary>
    /// Reads just the track name from ACC static memory
    /// </summary>
    public string? ReadTrackName()
    {
        var statics = ReadStaticsStruct();
        return statics?.Track;
    }

    /// <summary>
    /// Reads just the car model from ACC static memory
    /// </summary>
    public string? ReadCarModel()
    {
        var statics = ReadStaticsStruct();
        return statics?.CarModel;
    }

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
    public float SessionTimeLeft { get; set; } // Session time remaining in seconds

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

// Wheel and tire telemetry data
public class WheelAndTireData
{
    // Wheel pressures in PSI (Front Left, Front Right, Rear Left, Rear Right)
    public float WheelPressureFL { get; set; }
    public float WheelPressureFR { get; set; }
    public float WheelPressureRL { get; set; }
    public float WheelPressureRR { get; set; }

    // Tire core temperatures in Celsius
    public float TyreCoreTempFL { get; set; }
    public float TyreCoreTempFR { get; set; }
    public float TyreCoreTempRL { get; set; }
    public float TyreCoreTempRR { get; set; }

    // Helper properties for average values
    public float AverageWheelPressure => (WheelPressureFL + WheelPressureFR + WheelPressureRL + WheelPressureRR) / 4f;
    public float AverageTyreCoreTemp => (TyreCoreTempFL + TyreCoreTempFR + TyreCoreTempRL + TyreCoreTempRR) / 4f;

    // Helper properties for front/rear averages
    public float AverageFrontPressure => (WheelPressureFL + WheelPressureFR) / 2f;
    public float AverageRearPressure => (WheelPressureRL + WheelPressureRR) / 2f;
    public float AverageFrontTemp => (TyreCoreTempFL + TyreCoreTempFR) / 2f;
    public float AverageRearTemp => (TyreCoreTempRL + TyreCoreTempRR) / 2f;
}
