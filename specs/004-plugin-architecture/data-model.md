# Phase 1 Data Model: Plugin Architecture & Two-Tier Split

These are contract types (the seam), not persisted entities — nothing here is written to disk.

## IUiPlugin (Tier-2 UI plugin contract)

| Member | Type | Notes |
|--------|------|-------|
| `Id` | `string` | Stable unique identity; duplicates rejected at registration. |
| `Name` | `string` | Human-readable display name. |
| `ComponentType` | `System.Type` | The Blazor component the host renders (via `DynamicComponent`). Loader-agnostic. |
| `Icon` | `string?` | Optional icon name (MudBlazor/material). |
| `Order` | `int` | Optional sort key for deterministic enumeration (default 0; ties broken by `Id`). |

**Validation rules**: `Id` and `Name` non-empty; `ComponentType` must be assignable to a Blazor
component (`IComponent`). Registration of a duplicate `Id` fails with a clear error.

## INativeBridge (transport-agnostic native capability contract)

| Member | Signature | Notes |
|--------|-----------|-------|
| `GetReversingStateAsync` | `Task<bool> GetReversingStateAsync(CancellationToken ct = default)` | Reverse-cam/reversing state. |
| `GetAccStateAsync` | `Task<AccState> GetAccStateAsync(CancellationToken ct = default)` | Ignition/accessory state. |
| `OpenDspAsync` | `Task OpenDspAsync(CancellationToken ct = default)` | Request the shell open the Teyes DSP/EQ activity. |
| `WheelKeyPressed` | `event EventHandler<WheelKeyEvent>?` | Steering-wheel key events pushed by the shell. |

**Transport-agnostic**: the contract names no transport. Either JS-interop or a local socket can
implement it later without changing plugin code. The only implementation now is `StubNativeBridge`.

## AccState (enum)

`Unknown = 0` (default; returned by the stub off-device), `Off`, `On`.

## WheelKeyEvent

| Field | Type | Notes |
|-------|------|-------|
| `Key` | `WheelKey` (enum) | e.g. `VolumeUp`, `VolumeDown`, `Next`, `Previous`, `Voice`, `ModeSwitch`, `Unknown`. |
| `TimestampUtc` | `DateTimeOffset` | When the shell observed the press. |

## TankDto (reference-plugin read model)

Mirrors the existing `api/tanks` payload subset the reference tile needs: `Id` (Guid), `Name`
(string), `Type` (string/enum), `CurrentLevel` (double %), `Capacity` (double L). Read-only.

## Host/registry types

- **IUiPluginRegistry**: `IReadOnlyList<IUiPlugin> Plugins { get; }`, `void Register(IUiPlugin plugin)`
  (throws on duplicate `Id`), enumeration ordered by `Order` then `Id`.
- **UiPluginRegistry**: in-memory implementation; takes registered `IUiPlugin` instances via DI;
  uses `ILogger<UiPluginRegistry>` for structured logging of registrations/rejections.

## Relationships

```
VanDaemon.Web (Blazor host)
   └─ uses IUiPluginRegistry ── lists ──> IUiPlugin (N)
                                           └─ ComponentType rendered via DynamicComponent
   └─ provides INativeBridge (StubNativeBridge) ──> consumed by UI plugins only through the contract
   └─ provides IVanDaemonApiClient ──> consumed by reference plugin
SystemStatusUiPlugin (IUiPlugin)  ── ComponentType ─> SystemStatusTile.razor
SystemStatusTile  ── depends on ─> IVanDaemonApiClient + INativeBridge   (NO hardware/native/file refs)
```
