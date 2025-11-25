namespace VanDaemon.Core.Entities;

public class DevicePosition
{
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceType { get; set; } = string.Empty; // "Tank", "Control", "ElectricalDevice"
    public double X { get; set; }
    public double Y { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
