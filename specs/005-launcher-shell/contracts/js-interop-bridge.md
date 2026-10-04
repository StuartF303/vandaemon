# Contract: JS-Interop Bridge (Kotlin shell ↔ WASM UI)

This is the **transport realisation** of the 004 `INativeBridge` contract for the WebView/JS-interop channel. It defines the wire shape between the injected Kotlin object and the WASM UI. It **does not change** the 004 `INativeBridge` surface (Constitution §XI.4) — it maps it onto JS-interop. The C# `INativeBridge` remains the source of truth; this contract is how the Kotlin shell satisfies it across a WebView.

## Injection

- The shell injects a single native object into the page under a fixed global name: **`window.VanDaemonNativeBridge`** (via `WebView.addJavascriptInterface(obj, "VanDaemonNativeBridge")`).
- The page exposes a small JS dispatch hook the shell can call for events: **`window.VanDaemonBridgeEvents.onWheelKey(eventJson)`**. The shell pushes events with `webView.evaluateJavascript("window.VanDaemonBridgeEvents.onWheelKey(" + json + ")", null)`.
- Injection happens after the page's bridge-ready signal (or `onPageFinished`); calls issued before readiness MUST fail safe (no crash) and are not part of the success path.

## UI → native calls (request/response)

All UI→native methods are exposed as `@JavascriptInterface` methods on `window.VanDaemonNativeBridge`. They return a **JSON string** (or primitive) synchronously on the binder thread; the JS shim wraps the result in a resolved `Promise` so the WASM/C# side can `await` it.

| JS call | `@JavascriptInterface` Kotlin method | Wire return (stub) | Maps to C# member |
|---|---|---|---|
| `VanDaemonNativeBridge.getReversingState()` | `fun getReversingState(): Boolean` | `false` | `GetReversingStateAsync()` |
| `VanDaemonNativeBridge.getAccState()` | `fun getAccState(): String` | `"Unknown"` | `GetAccStateAsync()` |
| `VanDaemonNativeBridge.openDsp()` | `fun openDsp()` | (no return; resolves) | `OpenDspAsync()` |

**Encoding rules**:
- `AccState` crosses the wire as its **string name** (`"Unknown" | "Off" | "On"`), not an ordinal. The JS/C# side parses the name back to the enum; an unknown string → `Unknown`.
- `getReversingState` returns a JSON boolean.
- `openDsp` performs the native action (no-op stub) and resolves with no value.

## native → UI events (push)

| Shell push | Payload (JSON) | Delivered to | Maps to C# member |
|---|---|---|---|
| `onWheelKey` | `{ "key": "<WheelKey name>", "timestampUtc": "<ISO-8601 UTC>" }` | `window.VanDaemonBridgeEvents.onWheelKey(payload)` | `WheelKeyPressed` event (`WheelKeyEvent`) |

- `key` is a `WheelKey` **string name**; an unrecognised name → `Unknown`.
- `timestampUtc` is ISO-8601 UTC.
- In this feature the shell raises this event **only when explicitly told to** (e.g. by `BridgeRoundTripTest`); it is not driven by any real wheel-key source.

## Behavioural guarantees (testable)

- **G1 (FR-005 / SC-003)**: Each UI→native call returns its defined stub value with no hardware/native dependency present.
- **G2 (FR-004 / SC-003)**: A shell-pushed `onWheelKey` payload is delivered to the page's event hook with the same `key` + `timestampUtc`.
- **G3 (FR-006 / SC-004)**: The exposed member set + enum names + event payload shape match the 004 `INativeBridge` contract exactly — asserted by `BridgeContractDriftTest`. Adding/removing/renaming any member or enum value fails the test.
- **G4 (FR-007 / §XI.4)**: This transport adds **no** capability beyond the four 004 members. `window.VanDaemonNativeBridge` exposes exactly `getReversingState`, `getAccState`, `openDsp` (and the event channel) — nothing else.
- **G5 (fail-safe)**: A UI→native call before bridge readiness does not crash the app.

## Explicitly NOT in this contract (deferred)

- Real values for any member (all `needs-reverse-engineering`, §X.3/§XI.3).
- Any additional native capability (filesystem, intents beyond `openDsp` stub, vehicle signals) — would be a 004-level contract change (§XI.4), out of scope.
