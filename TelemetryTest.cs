using System.Text.Json;

namespace ACCRPMMonitor;

/// <summary>
/// Test Harness for Telemetry Server - Allows testing without ACC running
/// Run with: dotnet run --test-telemetry
/// </summary>
public class TelemetryTest
{
    public static void RunTest()
    {
        Console.WriteLine("=== Telemetry Server Test Harness ===");
        Console.WriteLine("This test simulates ACC telemetry without needing the game running.\n");

        // Create a mock ACCSharedMemorySimple (not actually needed for server start)
        var mockMemory = new MockACCMemory();

        // Create and start telemetry server
        var server = new TelemetryServer
        {
            Port = 8080,
            EnableHttpServer = true  // Enable HTTP for testing
        };

        Console.WriteLine("Starting telemetry server...");
        if (!server.Start(mockMemory))
        {
            Console.WriteLine("❌ Failed to start telemetry server");
            return;
        }

        Console.WriteLine($"✓ Telemetry server started on http://localhost:{server.Port}/telemetry");
        Console.WriteLine("\nGenerating mock telemetry data...");
        Console.WriteLine("Press Ctrl+C to stop\n");

        // Simulate a driving session with realistic telemetry
        int frameCount = 0;
        int rpm = 2000;
        int gear = 2;
        float speed = 50.0f;
        float fuel = 80.0f;
        bool accelerating = true;

        try
        {
            while (true)
            {
                frameCount++;

                // Simulate driving dynamics
                if (accelerating)
                {
                    rpm += 50;
                    speed += 0.5f;

                    // Shift up
                    if (rpm >= 7000 && gear < 6)
                    {
                        gear++;
                        rpm = 5000;
                    }
                    else if (rpm >= 7500)
                    {
                        accelerating = false;
                    }
                }
                else
                {
                    rpm -= 50;
                    speed -= 0.3f;

                    // Shift down
                    if (rpm <= 3000 && gear > 1)
                    {
                        gear--;
                        rpm = 5000;
                    }
                    else if (rpm <= 2000)
                    {
                        accelerating = true;
                    }
                }

                // Simulate tire telemetry
                var tireData = GenerateMockTireData(speed, frameCount);

                // Update server
                server.UpdateTelemetry(tireData, rpm, gear, speed, fuel);

                // Slowly decrease fuel
                fuel -= 0.002f;

                // Console feedback every 50 frames (~5 seconds at 10Hz)
                if (frameCount % 50 == 0)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Sending telemetry: RPM={rpm} Gear={gear} Speed={speed:F1} km/h Fuel={fuel:F1}L");
                    Console.WriteLine($"                      Tire Pressure FL={tireData.WheelPressureFL:F1} PSI, Temp FL={tireData.TyreCoreTempFL:F1}°C");
                }

                // Run at ~10Hz (100ms per frame)
                Thread.Sleep(100);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Error: {ex.Message}");
        }
        finally
        {
            server.Stop();
            Console.WriteLine("\nTelemetry server stopped.");
        }
    }

    private static WheelAndTireData GenerateMockTireData(float speed, int frameCount)
    {
        // Simulate realistic tire behavior
        // Base pressures around 27-28 PSI, increasing with speed
        float basePressure = 27.0f + (speed / 300.0f) * 3.0f;  // Increases with speed
        float baseTemp = 60.0f + (speed / 200.0f) * 40.0f;     // 60-100°C based on speed

        // Add some noise and variation between wheels
        Random rnd = new Random(frameCount);
        float pressureNoise = (float)(rnd.NextDouble() - 0.5) * 0.5f;
        float tempNoise = (float)(rnd.NextDouble() - 0.5) * 2.0f;

        return new WheelAndTireData
        {
            // Front tires slightly lower pressure
            WheelPressureFL = basePressure - 0.3f + pressureNoise,
            WheelPressureFR = basePressure - 0.2f + pressureNoise,
            // Rear tires slightly higher pressure
            WheelPressureRL = basePressure + 0.2f + pressureNoise,
            WheelPressureRR = basePressure + 0.3f + pressureNoise,

            // Front tires hotter (more braking/steering)
            TyreCoreTempFL = baseTemp + 5.0f + tempNoise,
            TyreCoreTempFR = baseTemp + 4.0f + tempNoise,
            // Rear tires cooler
            TyreCoreTempRL = baseTemp - 2.0f + tempNoise,
            TyreCoreTempRR = baseTemp - 3.0f + tempNoise
        };
    }

    /// <summary>
    /// Test fetching from the telemetry endpoint
    /// </summary>
    public static async Task TestTelemetryEndpoint()
    {
        Console.WriteLine("=== Testing Telemetry Endpoint ===\n");

        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(5);

        try
        {
            Console.WriteLine("Fetching from http://localhost:8080/telemetry...");
            var response = await client.GetAsync("http://localhost:8080/telemetry");

            Console.WriteLine($"Status Code: {response.StatusCode}");
            Console.WriteLine($"Content Type: {response.Content.Headers.ContentType}\n");

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                Console.WriteLine("Response JSON:");
                Console.WriteLine(json);

                // Try to parse
                var snapshot = JsonSerializer.Deserialize<TelemetrySnapshot>(json, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                if (snapshot != null)
                {
                    Console.WriteLine("\n✓ Successfully parsed telemetry data:");
                    Console.WriteLine($"  RPM: {snapshot.RPM}");
                    Console.WriteLine($"  Gear: {snapshot.Gear}");
                    Console.WriteLine($"  Speed: {snapshot.SpeedKmh:F1} km/h");
                    Console.WriteLine($"  Tire Pressure FL: {snapshot.TirePressureFL:F1} PSI");
                    Console.WriteLine($"  Tire Temp FL: {snapshot.TireTempFL:F1} °C");
                }
            }
            else
            {
                var content = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"❌ Error response: {content}");
            }
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"❌ Connection failed: {ex.Message}");
            Console.WriteLine("Make sure the telemetry server is running first.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
        }
    }
}

/// <summary>
/// Mock ACC memory for testing
/// </summary>
class MockACCMemory : ACCSharedMemorySimple
{
    // This is just a placeholder for the telemetry server
    // The server doesn't actually use the memory object after Start()
}
