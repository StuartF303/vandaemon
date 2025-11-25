namespace VanDaemon.Core.Entities;

public class UnifiedDeviceConfiguration
{
    public List<Tank> Tanks { get; set; } = new();
    public List<Control> Controls { get; set; } = new();
    public List<ElectricalDevice> ElectricalDevices { get; set; } = new();
    public List<ElectricalConnection> ElectricalConnections { get; set; } = new();
    public List<DevicePosition> DevicePositions { get; set; } = new();
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public int Version { get; set; } = 1;
}
