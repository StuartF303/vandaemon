using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using VanDaemon.Plugins.Ui.Abstractions;

namespace VanDaemon.Plugins.Ui.Bridge;

/// <summary>
/// On-device <see cref="INativeBridge"/> realised over JS-interop. Calls the
/// <c>window.vandaemonBridge</c> transport shim, which forwards to the Kotlin shell's injected
/// <c>window.VanDaemonNativeBridge</c> (wire shape: <c>specs/005-launcher-shell/contracts/js-interop-bridge.md</c>).
///
/// This is the C# half of the transport the 005 contract anticipated but did not build. Values are
/// still <b>stubs</b> in this pass (the transport is real, the vehicle signals are not — every real
/// value is <c>needs-reverse-engineering</c>, Constitution §XI.3). Selected by
/// <see cref="NativeBridgeFactory"/> only when the native object is present; off-device the
/// <see cref="StubNativeBridge"/> is used instead.
///
/// Every call is fail-safe: a bridge failure (e.g. a call issued before native readiness) is logged
/// and yields the contract's defined default rather than throwing (contract G5).
/// </summary>
public sealed class JsInteropNativeBridge : INativeBridge, IAsyncDisposable
{
    private const string ShimGetReversing = "vandaemonBridge.getReversingState";
    private const string ShimGetAcc = "vandaemonBridge.getAccState";
    private const string ShimOpenDsp = "vandaemonBridge.openDsp";
    private const string ShimRegisterWheelKey = "vandaemonBridge.registerWheelKey";
    private const string ShimUnregisterWheelKey = "vandaemonBridge.unregisterWheelKey";

    private readonly IJSRuntime _js;
    private readonly ILogger<JsInteropNativeBridge> _logger;
    private DotNetObjectReference<JsInteropNativeBridge>? _selfRef;

    /// <inheritdoc />
    public event EventHandler<WheelKeyEvent>? WheelKeyPressed;

    public JsInteropNativeBridge(IJSRuntime js, ILogger<JsInteropNativeBridge> logger)
    {
        _js = js;
        _logger = logger;

        // Install the native->UI wheel-key callback. Fire-and-forget: construction runs inside a
        // synchronous DI factory and no UI consumes the event yet. Failures are logged, never thrown.
        _ = InstallWheelKeyCallbackAsync();
    }

    private async Task InstallWheelKeyCallbackAsync()
    {
        try
        {
            _selfRef = DotNetObjectReference.Create(this);
            await _js.InvokeVoidAsync(ShimRegisterWheelKey, _selfRef);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to register native wheel-key callback; wheel-key events will not be delivered.");
        }
    }

    /// <inheritdoc />
    public async Task<bool> GetReversingStateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _js.InvokeAsync<bool>(ShimGetReversing, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "getReversingState bridge call failed; returning safe default (false).");
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<AccState> GetAccStateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var name = await _js.InvokeAsync<string>(ShimGetAcc, cancellationToken);
            // AccState crosses the wire as its string name; an unknown name -> Unknown (005 contract).
            return Enum.TryParse<AccState>(name, ignoreCase: true, out var state) ? state : AccState.Unknown;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "getAccState bridge call failed; returning Unknown.");
            return AccState.Unknown;
        }
    }

    /// <inheritdoc />
    public async Task OpenDspAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _js.InvokeVoidAsync(ShimOpenDsp, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "openDsp bridge call failed; ignoring (no-op stub).");
        }
    }

    /// <summary>
    /// Invoked from JS when the shell pushes a wheel-key event. Parses the wire shape
    /// (<c>key</c> name + ISO-8601 UTC timestamp) leniently and re-raises <see cref="WheelKeyPressed"/>.
    /// </summary>
    [JSInvokable]
    public void OnWheelKey(string key, string timestampUtc)
    {
        var wheelKey = Enum.TryParse<WheelKey>(key, ignoreCase: true, out var parsedKey)
            ? parsedKey
            : WheelKey.Unknown;

        var timestamp = DateTimeOffset.TryParse(
            timestampUtc,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsedTs)
            ? parsedTs
            : DateTimeOffset.MinValue;

        WheelKeyPressed?.Invoke(this, new WheelKeyEvent(wheelKey, timestamp));
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _js.InvokeVoidAsync(ShimUnregisterWheelKey);
        }
        catch
        {
            // Best-effort teardown; the page may already be tearing down its JS context.
        }

        _selfRef?.Dispose();
    }
}
