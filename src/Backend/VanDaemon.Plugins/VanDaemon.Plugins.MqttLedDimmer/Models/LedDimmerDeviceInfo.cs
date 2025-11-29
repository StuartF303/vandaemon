using System.Text.Json.Serialization;

namespace VanDaemon.Plugins.MqttLedDimmer.Models;

/// <summary>
/// Device information received from MQTT config topic
/// </summary>
public class LedDimmerDeviceInfo
{
    /// <summary>
    /// Device identifier
    /// </summary>
    [JsonPropertyName("deviceId")]
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Device name
    /// </summary>
    [JsonPropertyName("deviceName")]
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>
    /// Number of channels
    /// </summary>
    [JsonPropertyName("channels")]
    public int Channels { get; set; }

    /// <summary>
    /// Firmware version
    /// </summary>
    [JsonPropertyName("version")]
    public string? Version { get; set; }

    /// <summary>
    /// Board variant (4CH or 8CH)
    /// </summary>
    [JsonPropertyName("variant")]
    public string? Variant { get; set; }

    /// <summary>
    /// Last seen timestamp
    /// </summary>
    [JsonIgnore]
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Device status (online/offline)
    /// </summary>
    [JsonIgnore]
    public bool IsOnline { get; set; }

    /// <summary>
    /// Channel states (brightness 0-255)
    /// </summary>
    [JsonIgnore]
    public Dictionary<int, int> ChannelStates { get; set; } = new();
}

/// <summary>
/// Device heartbeat message
/// </summary>
public class LedDimmerHeartbeat
{
    /// <summary>
    /// Uptime in seconds
    /// </summary>
    [JsonPropertyName("uptime")]
    public long Uptime { get; set; }

    /// <summary>
    /// Free heap memory in bytes
    /// </summary>
    [JsonPropertyName("freeHeap")]
    public long FreeHeap { get; set; }

    /// <summary>
    /// WiFi signal strength (RSSI in dBm)
    /// </summary>
    [JsonPropertyName("rssi")]
    public int Rssi { get; set; }
}
