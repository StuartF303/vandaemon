namespace VanDaemon.Plugins.Ui.Abstractions;

/// <summary>
/// The single, <b>transport-agnostic</b> seam from Tier-2 UI plugins to native / vehicle
/// capabilities (Constitution §VI.1). The contract names no transport: a future JS-interop or
/// local-socket implementation can satisfy it without any change to plugin code. The only
/// implementation delivered in this feature is the off-device no-op stub.
/// </summary>
public interface INativeBridge
{
    /// <summary>True when the vehicle is reversing (reverse-camera trigger active).</summary>
    Task<bool> GetReversingStateAsync(CancellationToken cancellationToken = default);

    /// <summary>Current ignition / accessory (ACC) state.</summary>
    Task<AccState> GetAccStateAsync(CancellationToken cancellationToken = default);

    /// <summary>Ask the native shell to open the Teyes DSP / EQ activity.</summary>
    Task OpenDspAsync(CancellationToken cancellationToken = default);

    /// <summary>Raised when a steering-wheel key is pressed (pushed by the native shell).</summary>
    event EventHandler<WheelKeyEvent>? WheelKeyPressed;
}
