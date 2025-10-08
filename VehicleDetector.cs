using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Text;

namespace ACCRPMMonitor;

// Detects the current vehicle name from ACC static shared memory
public class VehicleDetector : IDisposable
{
    private MemoryMappedFile? _staticMMF;
    private const string StaticMMFName = "Local\\acpmf_static";

    // Attempts to connect to ACC static memory
    public bool Connect()
    {
        try
        {
            _staticMMF = MemoryMappedFile.OpenExisting(StaticMMFName, MemoryMappedFileRights.Read);
            return true;
        }
        catch
        {
            return false;
        }
    }

    // Reads the current car model name from static memory
    public string? GetCarModel()
    {
        if (_staticMMF == null)
            return null;

        try
        {
            using var accessor = _staticMMF.CreateViewAccessor(0, Marshal.SizeOf<ACCStatic>(), MemoryMappedFileAccess.Read);

            ACCStatic staticData;
            accessor.Read(0, out staticData);

            // Clean up the car model string (remove null characters)
            string carModel = staticData.CarModel?.Trim('\0') ?? "unknown";

            // Sanitize for use as filename
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                carModel = carModel.Replace(c, '_');
            }

            return string.IsNullOrWhiteSpace(carModel) ? "unknown" : carModel;
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        _staticMMF?.Dispose();
        _staticMMF = null;
    }
}

// ACC Static shared memory structure (simplified - only car model)
[StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Unicode)]
public struct ACCStatic
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)]
    public string SMVersion;

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

    // Padding to match actual structure size
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 100)]
    public float[] _padding;
}
