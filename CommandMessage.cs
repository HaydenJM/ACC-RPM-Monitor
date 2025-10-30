using System.Text.Json;
using System.Text.Json.Serialization;

namespace ACCRPMMonitor;

/// <summary>
/// Commands that can be sent from viewer to headless server
/// </summary>
public enum CommandType
{
    GetStatus = 1,
    StartCollection = 2,
    StopCollection = 3,
    GetCollectionStats = 4
}

/// <summary>
/// Message sent from viewer to headless server
/// </summary>
public class CommandMessage
{
    [JsonPropertyName("command")]
    public CommandType Command { get; set; }

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public string ToJson()
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }

    public static CommandMessage? FromJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<CommandMessage>(json, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Response from headless server to viewer
/// </summary>
public class CommandResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("collectionActive")]
    public bool CollectionActive { get; set; }

    [JsonPropertyName("dataPointCount")]
    public int DataPointCount { get; set; }

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public string ToJson()
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }

    public static CommandResponse? FromJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<CommandResponse>(json, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        }
        catch
        {
            return null;
        }
    }
}
