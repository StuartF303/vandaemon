namespace VanDaemon.Core.Entities;

/// <summary>
/// Represents the toolbar position options
/// </summary>
public enum ToolbarPosition
{
    Left,
    Right,
    Bottom
}

/// <summary>
/// Represents the system-wide configuration
/// </summary>
public class SystemConfiguration
{
    public Guid Id { get; set; }
    public string VanModel { get; set; } = "Mercedes Sprinter LWB";
    public string VanDiagramPath { get; set; } = "/diagrams/sprinter-lwb.svg";
    public ToolbarPosition ToolbarPosition { get; set; } = ToolbarPosition.Left;
    public AlertSettings AlertSettings { get; set; } = new();
    public Dictionary<string, object> PluginConfigurations { get; set; } = new();
    public DateTime LastUpdated { get; set; }
}

/// <summary>
/// Alert configuration settings
/// </summary>
public class AlertSettings
{
    public double TankLowLevelThreshold { get; set; } = 10.0;
    public double TankHighLevelThreshold { get; set; } = 90.0;
    public bool EnableAudioAlerts { get; set; } = true;
    public bool EnablePushNotifications { get; set; } = false;
}
