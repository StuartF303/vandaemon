namespace VanDaemon.Core.Enums;

/// <summary>
/// Types of controls available in the system
/// </summary>
public enum ControlType
{
    Toggle,      // On/Off switch
    Momentary,   // Push button
    Dimmer,      // Variable control (0-100)
    Selector     // Multi-position selector
}
