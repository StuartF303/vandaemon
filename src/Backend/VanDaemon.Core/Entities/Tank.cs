using VanDaemon.Core.Enums;

namespace VanDaemon.Core.Entities;

/// <summary>
/// Represents a tank in the camper van (water, waste, LPG, fuel, etc.)
/// </summary>
public class Tank
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public TankType Type { get; set; }
    public double CurrentLevel { get; set; } // Percentage (0-100)
    public double Capacity { get; set; } // Liters
    public double AlertLevel { get; set; } = 20.0; // Percentage
    public bool AlertWhenOver { get; set; } = false; // false = alert when under (empty), true = alert when over (full)
    public string SensorPlugin { get; set; } = string.Empty;
    public Dictionary<string, object> SensorConfiguration { get; set; } = new();
    public DateTime LastUpdated { get; set; }
    public bool IsActive { get; set; } = true;
}
