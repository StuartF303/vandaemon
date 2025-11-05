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
    public double LowLevelThreshold { get; set; } = 10.0; // Percentage
    public double HighLevelThreshold { get; set; } = 90.0; // Percentage
    public string SensorPlugin { get; set; } = string.Empty;
    public Dictionary<string, object> SensorConfiguration { get; set; } = new();
    public DateTime LastUpdated { get; set; }
    public bool IsActive { get; set; } = true;
}
