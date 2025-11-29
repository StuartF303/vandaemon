using System.Text.Json.Serialization;

namespace VanDaemon.Plugins.MqttLedDimmer.Models;

/// <summary>
/// Configuration for MQTT LED Dimmer plugin
/// </summary>
public class MqttLedDimmerConfiguration
{
    /// <summary>
    /// MQTT broker hostname or IP address
    /// </summary>
    [JsonPropertyName("mqttBroker")]
    public string MqttBroker { get; set; } = "localhost";

    /// <summary>
    /// MQTT broker port
    /// </summary>
    [JsonPropertyName("mqttPort")]
    public int MqttPort { get; set; } = 1883;

    /// <summary>
    /// MQTT username (optional)
    /// </summary>
    [JsonPropertyName("mqttUsername")]
    public string? MqttUsername { get; set; }

    /// <summary>
    /// MQTT password (optional)
    /// </summary>
    [JsonPropertyName("mqttPassword")]
    public string? MqttPassword { get; set; }

    /// <summary>
    /// Base topic for LED dimmer devices
    /// </summary>
    [JsonPropertyName("baseTopic")]
    public string BaseTopic { get; set; } = "vandaemon/leddimmer";

    /// <summary>
    /// Registered LED dimmer devices
    /// </summary>
    [JsonPropertyName("devices")]
    public List<LedDimmerDeviceConfig> Devices { get; set; } = new();

    /// <summary>
    /// Auto-discovery enabled
    /// </summary>
    [JsonPropertyName("autoDiscovery")]
    public bool AutoDiscovery { get; set; } = true;

    /// <summary>
    /// Discovery timeout in seconds
    /// </summary>
    [JsonPropertyName("discoveryTimeoutSeconds")]
    public int DiscoveryTimeoutSeconds { get; set; } = 30;
}

/// <summary>
/// Configuration for a single LED dimmer device
/// </summary>
public class LedDimmerDeviceConfig
{
    /// <summary>
    /// Unique device identifier (matches MQTT topic)
    /// </summary>
    [JsonPropertyName("deviceId")]
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Friendly device name
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Number of channels (4 or 8)
    /// </summary>
    [JsonPropertyName("channels")]
    public int Channels { get; set; } = 8;

    /// <summary>
    /// Icon prefix for controls (e.g., "mdi-lightbulb")
    /// </summary>
    [JsonPropertyName("iconPrefix")]
    public string IconPrefix { get; set; } = "mdi-lightbulb";

    /// <summary>
    /// Whether device is enabled
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Custom channel names (optional, indexed by channel number)
    /// </summary>
    [JsonPropertyName("channelNames")]
    public Dictionary<int, string> ChannelNames { get; set; } = new();
}
