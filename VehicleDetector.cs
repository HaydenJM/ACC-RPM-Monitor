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

    public void Dispose()
    {
        _staticMMF?.Dispose();
        _staticMMF = null;
    }
}
