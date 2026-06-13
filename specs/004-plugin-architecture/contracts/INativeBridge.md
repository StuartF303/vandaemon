# Contract: INativeBridge

Namespace: `VanDaemon.Plugins.Ui.Abstractions`

The single, transport-agnostic seam from Tier-2 UI plugins to native/vehicle capabilities
(constitution §VI). No transport is named; either JS-interop or a local socket can implement it later
without changing plugin code. The only implementation delivered in this feature is `StubNativeBridge`.

```csharp
namespace VanDaemon.Plugins.Ui.Abstractions;

/// <summary>
/// Transport-agnostic contract for native/vehicle capabilities the Kotlin shell will expose.
/// This is the ONLY path from UI plugins to native capability (Constitution §VI.1).
/// </summary>
public interface INativeBridge
{
    /// <summary>True when the vehicle is reversing (reverse-cam trigger active).</summary>
    Task<bool> GetReversingStateAsync(CancellationToken cancellationToken = default);

    /// <summary>Current ignition/accessory (ACC) state.</summary>
    Task<AccState> GetAccStateAsync(CancellationToken cancellationToken = default);

    /// <summary>Ask the shell to open the Teyes DSP/EQ activity.</summary>
    Task OpenDspAsync(CancellationToken cancellationToken = default);

    /// <summary>Raised when a steering-wheel key is pressed (pushed by the shell).</summary>
    event EventHandler<WheelKeyEvent>? WheelKeyPressed;
}

public enum AccState { Unknown = 0, Off, On }

public enum WheelKey { Unknown = 0, VolumeUp, VolumeDown, Next, Previous, Voice, ModeSwitch }

public sealed record WheelKeyEvent(WheelKey Key, DateTimeOffset TimestampUtc);
```

## Stub behavior (off-device, testable)

`StubNativeBridge` implements the contract with **defined, non-throwing defaults** so the app builds
and tests run off-device (FR-005):

- `GetReversingStateAsync` → `false`
- `GetAccStateAsync` → `AccState.Unknown`
- `OpenDspAsync` → completes (no-op)
- `WheelKeyPressed` → never raised by the stub (but the event is wirable)

## Behavioral guarantees (testable)

- The stub satisfies `INativeBridge` and every member returns its defined value with no native/WebView
  dependency present. (FR-004 / FR-005)
- The contract names no transport type. (FR-004 / transport-agnostic)
- UI plugins reach native capability only through this interface. (FR-008 / §VI.1)
