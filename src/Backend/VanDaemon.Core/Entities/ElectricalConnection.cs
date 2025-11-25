namespace VanDaemon.Core.Entities;

public class ElectricalConnection
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    // Source device and port
    public Guid SourceDeviceId { get; set; }
    public string SourcePortId { get; set; } = string.Empty;

    // Target device and port
    public Guid TargetDeviceId { get; set; }
    public string TargetPortId { get; set; } = string.Empty;

    // Real-time flow data (populated at runtime, not stored)
    public double CurrentFlow { get; set; } // Amps
    public double PowerFlow { get; set; } // Watts
    public bool IsFlowing { get; set; }

    // Visual configuration
    public string Color { get; set; } = "#2196F3";
    public double LineWidth { get; set; } = 2.0;

    public DateTime LastUpdated { get; set; }
    public bool IsActive { get; set; } = true;
}
