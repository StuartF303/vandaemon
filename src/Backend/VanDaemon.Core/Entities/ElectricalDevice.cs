using VanDaemon.Core.Enums;

namespace VanDaemon.Core.Entities;

public class ElectricalDevice
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ElectricalDeviceType DeviceType { get; set; }

    // Device configuration
    public Dictionary<string, object> Configuration { get; set; } = new();

    // Device ports for connections
    public List<DevicePort> Ports { get; set; } = new();

    // Data source configuration (e.g., Cerbo GX device ID, MQTT topic, etc.)
    public string DataSourcePlugin { get; set; } = "Simulated";
    public Dictionary<string, object> DataSourceConfiguration { get; set; } = new();

    // Real-time metrics (populated at runtime, not stored)
    public Dictionary<string, double> CurrentMetrics { get; set; } = new();

    public DateTime LastUpdated { get; set; }
    public bool IsActive { get; set; } = true;
}
