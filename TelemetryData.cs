using System.Text.Json.Serialization;

namespace ACCRPMMonitor;

/// <summary>
/// Real-time telemetry data sent between headless server and viewers via IPC.
/// </summary>
public class TelemetryData
{
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    [JsonPropertyName("gear")]
    public int Gear { get; set; }

    [JsonPropertyName("rpm")]
    public int RPM { get; set; }

    [JsonPropertyName("throttle")]
    public float Throttle { get; set; }

    [JsonPropertyName("speed")]
    public float Speed { get; set; }

    [JsonPropertyName("currentLapTime")]
    public string CurrentLapTime { get; set; } = "00:00.000";

    [JsonPropertyName("lastLapTime")]
    public string LastLapTime { get; set; } = "00:00.000";

    [JsonPropertyName("bestLapTime")]
    public string BestLapTime { get; set; } = "00:00.000";

    [JsonPropertyName("completedLaps")]
    public int CompletedLaps { get; set; }

    [JsonPropertyName("recommendedShiftRPM")]
    public int RecommendedShiftRPM { get; set; }

    [JsonPropertyName("sessionStatus")]
    public int SessionStatus { get; set; } // 0=OFF, 1=REPLAY, 2=LIVE, 3=PAUSE

    [JsonPropertyName("isValidLap")]
    public bool IsValidLap { get; set; }

    [JsonPropertyName("audioProfile")]
    public string AudioProfile { get; set; } = "Normal";

    [JsonPropertyName("audioMode")]
    public string AudioMode { get; set; } = "Standard";

    /// <summary>
    /// Serializes to JSON string for IPC transmission.
    /// </summary>
    public string ToJson()
    {
        return System.Text.Json.JsonSerializer.Serialize(this, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });
    }

    /// <summary>
    /// Deserializes from JSON string.
    /// </summary>
    public static TelemetryData? FromJson(string json)
    {
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<TelemetryData>(json, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });
        }
        catch
        {
            return null;
        }
    }
}
