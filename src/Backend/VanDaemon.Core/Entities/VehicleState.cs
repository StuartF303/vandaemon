namespace VanDaemon.Core.Entities;

/// <summary>
/// Represents the current state of the vehicle
/// (headlights, ignition, doors, etc. - primarily from CAN bus)
/// </summary>
public class VehicleState
{
    public Guid Id { get; set; }

    /// <summary>
    /// Whether headlights are currently on (dipped beam or full beam)
    /// Used for automatic theme switching
    /// </summary>
    public bool HeadlightsOn { get; set; }

    /// <summary>
    /// Whether ignition is on
    /// </summary>
    public bool IgnitionOn { get; set; }

    /// <summary>
    /// Current vehicle speed in km/h
    /// </summary>
    public double Speed { get; set; }

    // Sensor configuration for CAN bus or other vehicle data source
    public string SensorPlugin { get; set; } = string.Empty;
    public Dictionary<string, object> SensorConfiguration { get; set; } = new();

    // Metadata
    public DateTime LastUpdated { get; set; }
    public bool IsActive { get; set; } = true;
}
