using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using VanDaemon.Plugins.Ui.Abstractions;

namespace VanDaemon.Plugins.Ui.Bridge;

/// <summary>
/// Selects the <see cref="INativeBridge"/> implementation at first resolution and <b>logs the
/// choice</b>. When the Kotlin shell's native object (<c>window.VanDaemonNativeBridge</c>) is present,
/// the JS-interop transport is used; otherwise the off-device <see cref="StubNativeBridge"/>.
///
/// The log line is the deliberate distinguisher: both bridges return the same stub values, so the
/// on-screen result cannot tell a real round-trip from a silent stub fallback. A Blazor WASM
/// <see cref="ILogger"/> writes to the browser console, which the 005 shell forwards to logcat under
/// tag <c>VanDaemonShell</c> — so the selection is visible via
/// <c>adb logcat -s VanDaemonShell:*</c> and resolves 005 SC-007 on the unit.
///
/// Detection runs synchronously via <see cref="IJSInProcessRuntime"/> (valid in Blazor WebAssembly at
/// first resolution, after the runtime is up). Any runtime that cannot be probed synchronously, or a
/// failed probe, fails safe to the stub.
/// </summary>
public static class NativeBridgeFactory
{
    /// <summary>Global JS predicate exposed by <c>vandaemon-bridge.js</c>.</summary>
    public const string HasNativeProbe = "vandaemonBridge.hasNative";

    /// <summary>Logger category for the transport-selection decision.</summary>
    public const string LogCategory = "VanDaemon.NativeBridge";

    public static INativeBridge Create(IJSRuntime jsRuntime, ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(LogCategory);

        if (jsRuntime is not IJSInProcessRuntime inProcess)
        {
            logger.LogInformation(
                "JS runtime is not in-process (cannot probe synchronously) — using StubNativeBridge.");
            return new StubNativeBridge();
        }

        bool hasNative;
        try
        {
            hasNative = inProcess.Invoke<bool>(HasNativeProbe);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Native-transport probe failed — using StubNativeBridge.");
            return new StubNativeBridge();
        }

        if (hasNative)
        {
            logger.LogInformation(
                "Native transport detected (window.VanDaemonNativeBridge) — using JsInteropNativeBridge.");
            return new JsInteropNativeBridge(jsRuntime, loggerFactory.CreateLogger<JsInteropNativeBridge>());
        }

        logger.LogInformation("No native transport present — using StubNativeBridge.");
        return new StubNativeBridge();
    }
}
