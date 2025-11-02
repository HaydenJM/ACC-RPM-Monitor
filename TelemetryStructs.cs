using System.Runtime.InteropServices;

namespace ACCRPMMonitor;

/// <summary>
/// ACC Shared Memory Structure Definitions
/// These structs map directly to ACC's memory-mapped files using StructLayout for precise memory alignment.
/// Based on physics_map.txt, graphics_map.txt, and statics_map.txt
/// </summary>

// ==================== HELPER TYPES ====================

/// <summary>
/// 3D Vector (x, y, z)
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct Vector3f
{
    public float X;
    public float Y;
    public float Z;
}

/// <summary>
/// Wheel data for all 4 wheels (FL, FR, RL, RR)
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct Wheels
{
    public float FrontLeft;
    public float FrontRight;
    public float RearLeft;
    public float RearRight;
}

/// <summary>
/// Contact point data for all 4 wheels
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct ContactPoint
{
    public Vector3f FrontLeft;
    public Vector3f FrontRight;
    public Vector3f RearLeft;
    public Vector3f RearRight;
}

/// <summary>
/// Car damage information
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct CarDamage
{
    public float Front;
    public float Rear;
    public float Left;
    public float Right;
    public float Centre;
}

// ==================== MAIN STRUCTURES ====================

/// <summary>
/// ACC Physics Memory Map - Real-time physics telemetry (67 fields)
/// Memory-mapped file: "Local\\acpmf_physics"
/// Size: ~1000 bytes
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct ACCPhysics
{
    // Based on reference implementation from assettocorsa shared memory
    public int PacketId;
    public float Gas;
    public float Brake;
    public float Fuel;
    public int Gear;
    public int Rpms;
    public float SteerAngle;
    public float SpeedKmh;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public float[] Velocity;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public float[] AccG;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] WheelSlip;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] WheelLoad;  // Not used in ACC
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] WheelPressure;  // In PSI
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] WheelAngularSpeed;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] TyreWear;  // Not used in ACC
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] TyreDirtyLevel;  // Not used in ACC
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] TyreCoreTemp;  // In Celsius
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] CamberRad;  // Not used in ACC
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] SuspensionTravel;

    public int Drs;  // Not used in ACC
    public float TC;
    public float Heading;
    public float Pitch;
    public float Roll;
    public float CgHeight;  // Not used in ACC

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
    public float[] CarDamage;

    public int NumberOfTyresOut;  // Not used in ACC
    public int PitLimiterOn;
    public float Abs;

    public float KersCharge;  // Not used in ACC
    public float KersInput;  // Not used in ACC
    public int AutoShifterOn;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
    public float[] RideHeight;  // Not used in ACC

    public float TurboBoost;
    public float Ballast;  // Not used in ACC
    public float AirDensity;  // Not used in ACC

    public float AirTemp;
    public float RoadTemp;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public float[] LocalAngularVelocity;
    public float FinalFF;

    public float PerformanceMeter;  // Not used in ACC
    public int EngineBrake;  // Not used in ACC
    public int ErsRecoveryLevel;  // Not used in ACC
    public int ErsPowerLevel;  // Not used in ACC
    public int ErsHeatCharging;  // Not used in ACC
    public int ErsisCharging;  // Not used in ACC
    public float KersCurrentKJ;  // Not used in ACC
    public int DrsAvailable;  // Not used in ACC
    public int DrsEnabled;  // Not used in ACC
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] BrakeTemp;

    public float Clutch;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] TyreTempI;  // Not used in ACC
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] TyreTempM;  // Not used in ACC
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] TyreTempO;  // Not used in ACC

    public int IsAIControlled;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public Vector3f[] TyreContactPoint;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public Vector3f[] TyreContactNormal;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public Vector3f[] TyreContactHeading;
    public float BrakeBias;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public float[] LocalVelocity;

    public int P2PActivation;  // Not used in ACC
    public int P2PStatus;  // Not used in ACC
    public float CurrentMaxRpm;  // Not used in ACC
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] MZ;  // Not used in ACC
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] FZ;  // Not used in ACC
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] MY;  // Not used in ACC
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] SlipRatio;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] SlipAngle;

    public int TCInAction;  // Not used in ACC
    public int ABSInAction;  // Not used in ACC
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] SuspensionDamage;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] TyreTemp;  // Not used in ACC
    public float WaterTemp;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] BreakPressure;
    public int FrontBreakCompound;
    public int RearBreakCompount;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] PadLife;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] DiscLife;
    public int IgnitionOn;
    public int StarterEngineOn;
    public int IsEngineRunning;
    public float KerbVibration;
    public float SlipVibrations;
    public float GVibrations;
    public float ABSVibrations;
}

/// <summary>
/// ACC Graphics Memory Map - UI and session information (106 fields)
/// Memory-mapped file: "Local\\acpmf_graphics"
/// Size: ~2000 bytes
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Unicode)]
public struct ACCGraphics
{
    // Metadata
    public int PacketId;
    public int Status;        // 0=OFF, 1=REPLAY, 2=LIVE, 3=PAUSE
    public int SessionType;   // Practice, Qualify, Race, etc.
    public int SessionIndex;

    // Lap Timing Strings (wchar_t[15] = 30 bytes each)
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)]
    public string CurrentTime;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)]
    public string LastTime;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)]
    public string BestTime;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)]
    public string Split;

    // Lap Data (integers)
    public int CompletedLaps;
    public int Position;
    public int CurrentTimeMs;
    public int LastTimeMs;
    public int BestTimeMs;
    public int SessionTimeLeft;
    public float DistanceTraveled;

    [MarshalAs(UnmanagedType.I1)] public bool IsInPit;
    [MarshalAs(UnmanagedType.I1)] public bool IsInPitLane;

    public int CurrentSectorIndex;
    public int LastSectorTimeMs;
    public int NumberOfLaps;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)]
    public string TyreCompound;

    public float NormalizedCarPosition;  // 0.0 to 1.0 along track

    public int ActiveCars;

    // Car Coordinates (variable size arrays - need special handling)
    // ... skipping for now as these need dynamic marshaling

    public int PlayerCarID;
    public float PenaltyTime;
    public int Flag;  // Yellow, Blue, Black, etc.
    public int Penalty;
    public int IdealLineOn;

    [MarshalAs(UnmanagedType.I1)] public bool IsValidLap;

    public float FuelEstimatedLaps;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)]
    public string TrackStatus;

    public int MissingMandatoryPits;
    public float Clock;

    [MarshalAs(UnmanagedType.I1)] public bool DirectionLightsLeft;
    [MarshalAs(UnmanagedType.I1)] public bool DirectionLightsRight;

    [MarshalAs(UnmanagedType.I1)] public bool GlobalYellow;
    [MarshalAs(UnmanagedType.I1)] public bool GlobalYellow1;
    [MarshalAs(UnmanagedType.I1)] public bool GlobalYellow2;
    [MarshalAs(UnmanagedType.I1)] public bool GlobalYellow3;
    [MarshalAs(UnmanagedType.I1)] public bool GlobalWhite;
    [MarshalAs(UnmanagedType.I1)] public bool GlobalGreen;
    [MarshalAs(UnmanagedType.I1)] public bool GlobalChequered;
    [MarshalAs(UnmanagedType.I1)] public bool GlobalRed;

    public int MFDTyreSet;
    public float MFDFuelToAdd;
    public Wheels MFDTyrePressure;  // ← Alternative tire pressure source (in BAR)

    public float TrackGripStatus;
    public float RainIntensity;
    public float RainIntensityIn10min;
    public float RainIntensityIn30min;
    public int CurrentTyreSet;
    public int StrategyTyreSet;
}

/// <summary>
/// ACC Statics Memory Map - Static session information (40 fields)
/// Memory-mapped file: "Local\\acpmf_static"
/// Size: ~1000 bytes
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Unicode)]
public struct ACCStatics
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)]
    public string SharedMemoryVersion;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)]
    public string ACVersion;

    public int NumberOfSessions;
    public int NumCars;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)]
    public string CarModel;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)]
    public string Track;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)]
    public string PlayerName;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)]
    public string PlayerSurname;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)]
    public string PlayerNick;

    public int SectorCount;

    public int MaxRPM;  // ← Useful for shift point calculations!
    public float MaxFuel;

    [MarshalAs(UnmanagedType.I1)] public bool PenaltiesEnabled;

    public float AidFuelRate;
    public float AidTireRate;
    public float AidMechanicalDamage;

    [MarshalAs(UnmanagedType.I1)] public bool AidAllowTyreBlankets;

    public float AidStability;

    [MarshalAs(UnmanagedType.I1)] public bool AidAutoClutch;
    [MarshalAs(UnmanagedType.I1)] public bool AidAutoBlip;

    public int PitWindowStart;
    public int PitWindowEnd;

    [MarshalAs(UnmanagedType.I1)] public bool IsOnline;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)]
    public string DryTyresName;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)]
    public string WetTyresName;
}
