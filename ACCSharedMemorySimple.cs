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

    public string? LastError { get; private set; }

    public void Dispose()
    {
        _physicsMMF?.Dispose();
        _graphicsMMF?.Dispose();
        _physicsMMF = null;
        _graphicsMMF = null;
    }
}
