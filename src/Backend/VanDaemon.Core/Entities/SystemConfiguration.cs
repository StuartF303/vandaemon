using VanDaemon.Core.Enums;

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

    /// <summary>
    /// How the theme should be determined
    /// </summary>
    public Enums.ThemeMode ThemeMode { get; set; } = Enums.ThemeMode.Manual;

    /// <summary>
    /// The manually selected theme (only used when ThemeMode = Manual)
    /// </summary>
    public Enums.Theme ManualTheme { get; set; } = Enums.Theme.Light;

    /// <summary>
    /// DEPRECATED: Old theme property for backward compatibility
    /// Automatically migrates to ThemeMode = Manual and ManualTheme on load
    /// </summary>
    [Obsolete("Use ThemeMode and ManualTheme instead")]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public string? Theme
    {
        get => null;
        set
        {
            // Migration: Convert old Theme string to new properties
            if (!string.IsNullOrEmpty(value))
            {
                ThemeMode = Enums.ThemeMode.Manual;
                ManualTheme = value.Equals("Dark", StringComparison.OrdinalIgnoreCase)
                    ? Enums.Theme.Dark
                    : Enums.Theme.Light;
            }
        }
    }

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
