# Phase 1 Data Model: VanDaemon Launcher Shell (first pass)

This feature persists no data and defines no domain entities of its own. The only "model" is the **bridge surface** — pinned to the 004 `INativeBridge` contract — and its **JS-interop wire shape**. Nothing here may alter the 004 contract (Constitution §XI.4); this document mirrors it for the Kotlin side.

## Source of truth

- C#: `src/Backend/VanDaemon.Plugins/VanDaemon.Plugins.Ui.Abstractions/INativeBridge.cs`
- Contract of record: `specs/004-plugin-architecture/contracts/INativeBridge.md`

The Kotlin definitions below MUST match these exactly. The drift test (`BridgeContractDriftTest`) enforces it.

## Enums

### AccState
| Ordinal | Name | Notes |
|---|---|---|
| 0 | `Unknown` | Default / stub return for `getAccState` |
| 1 | `Off` | (real value — needs-reverse-engineering) |
| 2 | `On` | (real value — needs-reverse-engineering) |

### WheelKey
| Ordinal | Name |
|---|---|
| 0 | `Unknown` |
| 1 | `VolumeUp` |
| 2 | `VolumeDown` |
| 3 | `Next` |
| 4 | `Previous` |
| 5 | `Voice` |
| 6 | `ModeSwitch` |

> Ordinals mirror the C# `enum` declaration order. The wire format uses the **string name** (not the ordinal) to avoid coupling to numeric values across the JS-interop boundary (see contract).

## Record

### WheelKeyEvent
| Field | Type | Notes |
|---|---|---|
| `key` | `WheelKey` | which steering-wheel key |
| `timestampUtc` | ISO-8601 UTC timestamp (`DateTimeOffset` on the C# side) | when the press occurred |

## Bridge members (surface)

| Member | Direction | Args | Returns | Stub value (this feature) | Real impl |
|---|---|---|---|---|---|
| `getReversingState` | UI → native | — | `bool` | `false` | needs-reverse-engineering |
| `getAccState` | UI → native | — | `AccState` | `Unknown` | needs-reverse-engineering |
| `openDsp` | UI → native | — | void (completes) | no-op | needs-reverse-engineering |
| `wheelKeyPressed` | native → UI (event) | `WheelKeyEvent` | — (pushed) | not raised by shell; raised only when explicitly pushed (e.g. by a test) | needs-reverse-engineering |

> Member names above are the JS-interop/Kotlin spellings (camelCase). They correspond 1:1 to the C# `INativeBridge` members `GetReversingStateAsync`, `GetAccStateAsync`, `OpenDspAsync`, `WheelKeyPressed`. The C# side is `Task`-returning/async and `event`-based; the JS shim adapts sync `@JavascriptInterface` returns into awaitable promises (see contract). **No member is added, removed, or renamed** relative to 004.

## Validation rules

- An unrecognised wheel-key name received over the wire MUST map to `WheelKey.Unknown` (mirrors C# enum default).
- `getAccState` MUST never return outside `{Unknown, Off, On}`; in this feature it always returns `Unknown`.
- `timestampUtc` MUST be a valid UTC ISO-8601 instant; the pushed test event supplies one explicitly.
- The bridge MUST NOT expose any member beyond the four above (no debug/escape hatches) — keeps the seam minimal (§XI.1) and the drift test green.

## State / lifecycle

The bridge is stateless in the first pass (constant stub returns; the event is only pushed on demand). No transitions to model. Shell lifecycle (Activity create → WebView load → bridge inject → destroy) is implementation detail, not domain state.
