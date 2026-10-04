# Implementation Plan: Native Bridge Transport (C# JS-interop realisation)

**Feature**: 008-native-bridge-transport | **Spec**: [spec.md](./spec.md) | **Risk**: mixed A/C

## Approach

Build the C# half of the transport the 005 `contracts/js-interop-bridge.md` already specifies, keeping
three responsibilities in separate, independently-testable units:

- **Transport shim** (`vandaemon-bridge.js`) — the JS adapter that owns the native member names,
  turns synchronous `@JavascriptInterface` returns into awaitable Promises, exposes a `hasNative()`
  probe, and wires the native→UI wheel-key push into a .NET callback.
- **`JsInteropNativeBridge`** — the `INativeBridge` implementation that calls the shim over
  `IJSRuntime` and re-raises `WheelKeyPressed` from a `[JSInvokable]` callback.
- **`NativeBridgeFactory`** — the selection + logging decision, extracted from DI wiring so it is
  unit-testable with a fake JS runtime.

The existing `StubNativeBridge` is retained unchanged as the off-device / fail-safe implementation.
The 004 `INativeBridge` contract is untouched.

## Components & files

### New

| File | Responsibility |
|---|---|
| `src/Frontend/VanDaemon.Web/wwwroot/js/vandaemon-bridge.js` | Transport shim: `hasNative()`, `getReversingState()`, `getAccState()`, `openDsp()`, `registerWheelKey(dotNetRef)`. Fail-safe if `window.VanDaemonNativeBridge` absent. |
| `src/Frontend/VanDaemon.Plugins.Ui/Bridge/JsInteropNativeBridge.cs` | `INativeBridge` over `IJSRuntime`; `[JSInvokable] OnWheelKey(string key,string ts)`; holds `DotNetObjectReference`; `IAsyncDisposable`. |
| `src/Frontend/VanDaemon.Plugins.Ui/Bridge/NativeBridgeFactory.cs` | `Create(IJSRuntime, ILoggerFactory)`: sync `hasNative` probe → select + log JsInterop or Stub. |

### Modified

| File | Change |
|---|---|
| `src/Frontend/VanDaemon.Plugins.Ui/ServiceCollectionExtensions.cs` | Replace `AddSingleton<INativeBridge, StubNativeBridge>()` with a factory delegating to `NativeBridgeFactory.Create`. |
| `src/Frontend/VanDaemon.Web/wwwroot/index.html` | Add `<script src="/js/vandaemon-bridge.js"></script>` before `blazor.webassembly.js` is not required; place after it is fine (shim only defines globals). |
| `src/Frontend/VanDaemon.Web/Pages/Devices.razor` | Render `<UiPluginHost />` in a titled section (scrollable content page — low blast radius). |
| `src/Frontend/VanDaemon.Plugins.Ui/ReferencePlugin/SystemStatusTile.razor` | Wrap `Api.GetTanksAsync()` in try/catch → offline line on failure; bridge value still shown. |

## Key decisions

1. **Detection at first resolution, not literal registration.** In Blazor WASM, JS interop is not
   available during `Program.cs` before `RunAsync()`. The DI factory delegate runs when `INativeBridge`
   is first resolved — during `SystemStatusTile` render, after the runtime is up — so a **synchronous**
   `IJSInProcessRuntime.Invoke<bool>("vandaemonBridge.hasNative")` is valid there. `IJSRuntime` that is
   not `IJSInProcessRuntime` ⇒ treat as no native ⇒ stub (fail-safe, FR-005).

2. **The log is the distinguisher, by design.** Both stubs return `false` for reversing, so the
   on-screen value cannot distinguish a real round-trip from a silent stub fallback. The info-level
   selection log (`… JsInteropNativeBridge` vs `… StubNativeBridge`) is the sole, unambiguous evidence
   — and it rides the shell's existing console→logcat forwarding to reach `VanDaemonShell`.

3. **Shim owns the native names.** `JsInteropNativeBridge` calls `vandaemonBridge.*`, never
   `VanDaemonNativeBridge.*` directly, so the `@JavascriptInterface` surface lives in exactly one JS
   place and the Promise-wrapping (contract requirement) has a home.

4. **Wheel-key wiring is lazy + disposable.** The `DotNetObjectReference` + `registerWheelKey` install
   happens on first construction via fire-and-forget `InvokeVoidAsync`; `IAsyncDisposable` disposes the
   reference. No UI currently consumes the event, but wiring it completes the contract (closing the
   half-wired seam that caused this feature).

## Testing

- **`NativeBridgeFactoryTests`** (xUnit + Moq): fake `IJSRuntime` implementing `IJSInProcessRuntime`
  returning `true`/`false` for `hasNative` → assert concrete type + a captured log entry. Third case:
  a plain `IJSRuntime` (not in-process) → stub. (SC-001)
- **`SystemStatusTileTests`** (bUnit): throwing `IVanDaemonApiClient` + a fake `INativeBridge` → assert
  the offline testid renders and no exception escapes. Reachable client → tank line renders. (SC-002)
- **Android `BridgeRoundTripTest`** (extend, Class B): assert `window.vandaemonBridge.hasNative()` is
  `true` on-device and `window.vandaemonBridge.getReversingState()` resolves `false` — proving the
  exact shim layer the C# bridge drives is present in the unit's WebView.
- **Exit condition (Class A)**: `dotnet build` + `dotnet test` green (loop-playbook §2).

## Verification & governance

- Deliver Class-A green via `dotnet test`. Do **not** self-merge — open a reviewed PR (user directive +
  Class-C content present).
- Update `specs/005-launcher-shell` cross-reference and `docs/head-unit/roadmap.md` to record that
  SC-007 is now observable and how to read it on the unit.
- On-device SC-004 (= 005 SC-007) stays **unchecked**; no on-device success claimed (§XIII.5).
