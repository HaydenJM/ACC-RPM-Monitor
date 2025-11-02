using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Text;

namespace ACCRPMMonitor;

/// <summary>
/// Detects the current vehicle and track from ACC static shared memory.
/// Used for automatic vehicle/track switching and config management.
/// v3.7.1: Enhanced with track detection support.
/// </summary>
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
            using var accessor = _staticMMF.CreateViewAccessor(0, 2048, MemoryMappedFileAccess.Read);

            // Read car model directly from known offset
            // ACC Static Memory Structure:
            // SMVersion (wchar_t[15]) = 30 bytes
            // ACVersion (wchar_t[15]) = 30 bytes
            // NumberOfSessions (int) = 4 bytes
            // NumCars (int) = 4 bytes
            // CarModel (wchar_t[33]) = 66 bytes at offset 68

            byte[] carModelBytes = new byte[66]; // wchar_t[33] = 33 * 2 bytes
            accessor.ReadArray(68, carModelBytes, 0, 66);

            // Convert from Unicode (wide char)
            string carModel = Encoding.Unicode.GetString(carModelBytes).Trim('\0');

            if (string.IsNullOrWhiteSpace(carModel))
                return null;

            // Sanitize for use as filename
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                carModel = carModel.Replace(c, '_');
            }

            return carModel;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Vehicle detection error: {ex.Message}");
            return null;
        }
    }

    // Reads the current track name from static memory
    public string? GetTrackName()
    {
        if (_staticMMF == null)
            return null;

        try
        {
            using var accessor = _staticMMF.CreateViewAccessor(0, 2048, MemoryMappedFileAccess.Read);

            // Read track name directly from known offset
            // ACC Static Memory Structure:
            // SMVersion (wchar_t[15]) = 30 bytes
            // ACVersion (wchar_t[15]) = 30 bytes
            // NumberOfSessions (int) = 4 bytes
            // NumCars (int) = 4 bytes
            // CarModel (wchar_t[33]) = 66 bytes
            // Track (wchar_t[33]) = 66 bytes at offset 134 (68 + 66)

            byte[] trackBytes = new byte[66]; // wchar_t[33] = 33 * 2 bytes
            accessor.ReadArray(134, trackBytes, 0, 66);

            // Convert from Unicode (wide char)
            string track = Encoding.Unicode.GetString(trackBytes).Trim('\0');

            if (string.IsNullOrWhiteSpace(track))
                return null;

            // Sanitize for use as filename
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                track = track.Replace(c, '_');
            }

            return track;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Track detection error: {ex.Message}");
            return null;
        }
    }

    public void Dispose()
    {
        _staticMMF?.Dispose();
        _staticMMF = null;
    }
}
