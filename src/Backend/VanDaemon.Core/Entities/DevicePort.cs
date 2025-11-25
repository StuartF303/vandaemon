using VanDaemon.Core.Enums;

namespace VanDaemon.Core.Entities;

public class DevicePort
{
    public string PortId { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public PortType PortType { get; set; }
    public EnergyType EnergyType { get; set; }

    // Position on device card (0-1 range, relative to card dimensions)
    public double RelativeX { get; set; }
    public double RelativeY { get; set; }
}
