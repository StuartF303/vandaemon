namespace VanDaemon.Plugins.Ui.Abstractions;

/// <summary>
/// Ignition / accessory (ACC) state reported by the native shell.
/// <see cref="Unknown"/> is the default returned off-device by the stub bridge.
/// </summary>
public enum AccState
{
    Unknown = 0,
    Off,
    On
}
