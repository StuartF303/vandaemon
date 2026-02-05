namespace VanDaemon.Core.Enums;

/// <summary>
/// Represents how the theme should be determined
/// </summary>
public enum ThemeMode
{
    /// <summary>
    /// User manually selects light or dark theme
    /// </summary>
    Manual,

    /// <summary>
    /// Automatically use browser/OS dark mode preference
    /// </summary>
    BrowserAuto,

    /// <summary>
    /// Automatically switch based on headlight status from CAN bus
    /// (Light theme when headlights off, Dark theme when headlights on)
    /// </summary>
    HeadlightsAuto
}
