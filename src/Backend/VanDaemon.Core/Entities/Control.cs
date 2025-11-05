using VanDaemon.Core.Enums;

namespace VanDaemon.Core.Entities;

/// <summary>
/// Represents a control element (switch, dimmer, etc.) in the camper van
/// </summary>
public class Control
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ControlType Type { get; set; }
    public object State { get; set; } = false; // bool for Toggle/Momentary, int for Dimmer/Selector
    public string ControlPlugin { get; set; } = string.Empty;
    public Dictionary<string, object> ControlConfiguration { get; set; } = new();
    public DateTime LastUpdated { get; set; }
    public bool IsActive { get; set; } = true;
    public string IconName { get; set; } = string.Empty; // For UI display
}
