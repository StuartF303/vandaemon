using VanDaemon.Plugins.Ui.Abstractions;

namespace VanDaemon.Plugins.Ui.Bridge;

/// <summary>
/// Off-device no-op <see cref="INativeBridge"/>. Returns defined defaults and never throws, so the
/// Blazor app and UI plugins build and run on a desktop without the head unit present. The real
/// transport (JS-interop or local socket) is supplied by the launcher feature, on-device.
/// </summary>
public sealed class StubNativeBridge : INativeBridge
{
    /// <inheritdoc />
    public Task<bool> GetReversingStateAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    /// <inheritdoc />
    public Task<AccState> GetAccStateAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(AccState.Unknown);

    /// <inheritdoc />
    public Task OpenDspAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc />
    public event EventHandler<WheelKeyEvent>? WheelKeyPressed;

    /// <summary>
    /// Test/seam hook: lets a host (or test) raise <see cref="WheelKeyPressed"/>. The stub never
    /// raises it on its own off-device.
    /// </summary>
    public void RaiseWheelKey(WheelKeyEvent wheelKeyEvent) =>
        WheelKeyPressed?.Invoke(this, wheelKeyEvent);
}
