using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace ACCRPMMonitor;

/// <summary>
/// ACC Shared Memory reader for telemetry data
/// </summary>
public class ACCSharedMemory : IDisposable
{
    private MemoryMappedFile? _physicsMMF;
    private MemoryMappedFile? _graphicsMMF;
    private MemoryMappedFile? _staticMMF;

    private const string PhysicsMMFName = "Local\\acpmf_physics";
    private const string GraphicsMMFName = "Local\\acpmf_graphics";
    private const string StaticMMFName = "Local\\acpmf_static";

    /// <summary>
    /// Attempts to connect to ACC shared memory
    /// </summary>
    /// <returns>True if connected successfully</returns>
    public bool Connect()
    {
        try
        {
            _physicsMMF = MemoryMappedFile.OpenExisting(PhysicsMMFName, MemoryMappedFileRights.Read);
            _graphicsMMF = MemoryMappedFile.OpenExisting(GraphicsMMFName, MemoryMappedFileRights.Read);
            _staticMMF = MemoryMappedFile.OpenExisting(StaticMMFName, MemoryMappedFileRights.Read);
            return true;
        }
        catch
        {
            Dispose();
            return false;
        }
    }

    /// <summary>
    /// Checks if currently connected to ACC
    /// </summary>
    public bool IsConnected => _physicsMMF != null;

    /// <summary>
    /// Reads current physics data from shared memory
    /// </summary>
    public ACCPhysics? ReadPhysics()
    {
        if (_physicsMMF == null)
            return null;

        try
        {
            using var accessor = _physicsMMF.CreateViewAccessor(0, Marshal.SizeOf<ACCPhysics>(), MemoryMappedFileAccess.Read);
            accessor.Read(0, out ACCPhysics physics);
            return physics;
        }
        catch (Exception ex)
        {
            LastError = $"Physics read error: {ex.Message}";
            return null;
        }
    }

    /// <summary>
    /// Reads current graphics data from shared memory
    /// </summary>
    public ACCGraphics? ReadGraphics()
    {
        if (_graphicsMMF == null)
            return null;

        try
        {
            using var accessor = _graphicsMMF.CreateViewAccessor(0, Marshal.SizeOf<ACCGraphics>(), MemoryMappedFileAccess.Read);
            accessor.Read(0, out ACCGraphics graphics);
            return graphics;
        }
        catch (Exception ex)
        {
            LastError = $"Graphics read error: {ex.Message}";
            return null;
        }
    }

    public string? LastError { get; private set; }

    public void Dispose()
    {
        _physicsMMF?.Dispose();
        _graphicsMMF?.Dispose();
        _staticMMF?.Dispose();
        _physicsMMF = null;
        _graphicsMMF = null;
        _staticMMF = null;
    }
}

/// <summary>
/// ACC Physics shared memory structure (simplified - only essential fields)
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Unicode)]
public struct ACCPhysics
{
    public int PacketId;
    public float Gas;
    public float Brake;
    public float Fuel;
    public int Gear;
    public int Rpms;
    public float SteerAngle;
    public float SpeedKmh;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] Velocity;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] AccG;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] WheelSlip;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] WheelLoad;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] WheelsPressure;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] WheelAngularSpeed;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] TyreWear;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] TyreDirtyLevel;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] TyreCoreTemperature;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] CamberRAD;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] SuspensionTravel;

    public float Drs;
    public float TC;
    public float Heading;
    public float Pitch;
    public float Roll;
    public float CgHeight;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
    public float[] CarDamage;

    public int NumberOfTyresOut;
    public int PitLimiterOn;
    public float Abs;

    public float KersCharge;
    public float KersInput;
    public int AutoShifterOn;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
    public float[] RideHeight;

    public float TurboBoost;
    public float Ballast;
    public float AirDensity;

    public float AirTemp;
    public float RoadTemp;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public float[] LocalAngularVel;

    public float FinalFF;

    public float PerformanceMeter;
    public int EngineBrake;
    public int ErsRecoveryLevel;
    public int ErsPowerLevel;
    public int ErsHeatCharging;
    public int ErsIsCharging;
    public float KersCurrentKJ;

    public int DrsAvailable;
    public int DrsEnabled;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] BrakeTemp;

    public float Clutch;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] TyreTempI;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] TyreTempM;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] TyreTempO;

    public int IsAIControlled;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] TyreContactPoint;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] TyreContactNormal;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] TyreContactHeading;

    public float BrakeBias;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public float[] LocalVelocity;
}

/// <summary>
/// ACC Graphics shared memory structure (simplified)
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Unicode)]
public struct ACCGraphics
{
    public int PacketId;
    public int Status; // 0 = AC_OFF, 1 = AC_REPLAY, 2 = AC_LIVE, 3 = AC_PAUSE
    public int Session; // 0 = AC_UNKNOWN, 1 = AC_PRACTICE, 2 = AC_QUALIFY, 3 = AC_RACE

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)]
    public string CurrentTime;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)]
    public string LastTime;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)]
    public string BestTime;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)]
    public string Split;

    public int CompletedLaps;
    public int Position;
    public int iCurrentTime;
    public int iLastTime;
    public int iBestTime;
    public float SessionTimeLeft;
    public float DistanceTraveled;
    public int IsInPit;
    public int CurrentSectorIndex;
    public int LastSectorTime;
    public int NumberOfLaps;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)]
    public string TyreCompound;

    public float ReplayTimeMultiplier;
    public float NormalizedCarPosition;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public float[] ActiveCars;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 60)]
    public float[] CarCoordinates;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 60)]
    public int[] CarID;

    public int PlayerCarID;
    public float PenaltyTime;
    public int Flag;
    public int Penalty;
    public int IdealLineOn;
    public int IsInPitLane;

    public float SurfaceGrip;
    public int MandatoryPitDone;

    public float WindSpeed;
    public float WindDirection;

    public int IsSetupMenuVisible;

    public int MainDisplayIndex;
    public int SecondaryDisplayIndex;

    public int TC;
    public int TCCUT;
    public int EngineMap;
    public int ABS;

    public float FuelXLap;
    public int RainLights;
    public int FlashingLights;
    public int LightsStage;

    public float ExhaustTemperature;
    public int WiperLV;
    public int DriverStintTotalTimeLeft;
    public int DriverStintTimeLeft;
    public int RainTyres;
}
