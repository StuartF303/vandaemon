namespace VanDaemon.Plugins.Ui.Abstractions;

/// <summary>
/// Steering-wheel keys the native shell may report. <see cref="Unknown"/> covers
/// keycodes not (yet) mapped during on-device reverse-engineering.
/// </summary>
public enum WheelKey
{
    Unknown = 0,
    VolumeUp,
    VolumeDown,
    Next,
    Previous,
    Voice,
    ModeSwitch
}

/// <summary>
/// A single steering-wheel key press, pushed from the native shell to UI plugins
/// via <see cref="INativeBridge.WheelKeyPressed"/>.
/// </summary>
public sealed record WheelKeyEvent(WheelKey Key, DateTimeOffset TimestampUtc);
