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
/// Represents the driving side configuration (affects default toolbar position)
/// </summary>
public enum DrivingSide
{
    Left,   // Left-hand drive (driver on left, toolbar on left)
    Right   // Right-hand drive (driver on right, toolbar on right)
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
    public DrivingSide DrivingSide { get; set; } = DrivingSide.Left;
    public string Theme { get; set; } = "Light";
    public bool EnableFullscreenOnStartup { get; set; } = true;
    public bool ShowFullscreenToggle { get; set; } = true;
    public AlertSettings AlertSettings { get; set; } = new();
    public Dictionary<string, object> PluginConfigurations { get; set; } = new();
    public DateTime LastUpdated { get; set; }
}

/// <summary>
/// Alert configuration settings
/// </summary>
public class AlertSettings
{
    public bool EnableAudioAlerts { get; set; } = true;
    public bool EnablePushNotifications { get; set; } = false;
}
