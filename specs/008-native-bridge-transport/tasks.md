# Tasks: Native Bridge Transport (C# JS-interop realisation)

**Feature**: 008-native-bridge-transport | Ordered; `[P]` = parallelisable with the prior task.

## Phase 1 — Transport (C# half)

- **T001** Add `wwwroot/js/vandaemon-bridge.js`: `window.vandaemonBridge` with `hasNative()`,
  `getReversingState()`, `getAccState()`, `openDsp()`, `registerWheelKey(dotNetRef)`. Each native call
  guarded so absence of `window.VanDaemonNativeBridge` returns the safe default (contract G5). Register
  the script in `index.html`.
- **T002** Add `Bridge/JsInteropNativeBridge.cs` implementing `INativeBridge` over `IJSRuntime`:
  `GetReversingStateAsync` → `vandaemonBridge.getReversingState`; `GetAccStateAsync` → parse name →
  `AccState` (unknown → `Unknown`); `OpenDspAsync` → `vandaemonBridge.openDsp`; `[JSInvokable] OnWheelKey`
  re-raises `WheelKeyPressed`; installs the `DotNetObjectReference` via `registerWheelKey`; `IAsyncDisposable`.
- **T003** Add `Bridge/NativeBridgeFactory.cs`: `Create(IJSRuntime, ILoggerFactory)` — sync
  `IJSInProcessRuntime` `hasNative` probe; select + info-log `JsInteropNativeBridge` or `StubNativeBridge`;
  non-in-process runtime ⇒ stub.
- **T004** Rewire `ServiceCollectionExtensions.AddVanDaemonUiPlugins` to register `INativeBridge` via a
  factory calling `NativeBridgeFactory.Create`. Keep everything else identical.

## Phase 2 — Make the seam observable

- **T005** Render `<UiPluginHost />` on `Devices.razor` in a titled section.
- **T006** Harden `SystemStatusTile.razor`: try/catch around `Api.GetTanksAsync()`; on failure set an
  offline flag and render `data-testid="status-offline"`; bridge value still displayed.

## Phase 3 — Tests (Class A)

- **T007** `NativeBridgeFactoryTests` (xUnit + Moq): native-present ⇒ `JsInteropNativeBridge` + logged;
  native-absent ⇒ `StubNativeBridge` + logged; non-in-process runtime ⇒ stub. [P with T008]
- **T008** `SystemStatusTileTests` (bUnit): throwing API client ⇒ offline line, no exception; reachable
  client ⇒ tank line.
- **T009** Extend Android `BridgeRoundTripTest`: assert `vandaemonBridge.hasNative()` true and
  `vandaemonBridge.getReversingState()` resolves `false` on-device. (Class B; runs where an emulator exists.)

## Phase 4 — Verify & document

- **T010** `dotnet build VanDaemon.sln` and `dotnet test VanDaemon.sln` → green (Class-A exit condition).
- **T011** Update `docs/head-unit/roadmap.md` and the 005 on-hardware checklist reference: SC-007 is now
  observable; how to read the selection log on the unit. Leave on-device items unchecked.
- **T012** Atomic commits (docs / impl / tests / doc-updates), push, open PR with the risk-class section;
  no on-device success claim; no self-merge.

## Traceability

| Task | FR / SC |
|---|---|
| T001–T004 | FR-002, FR-003, FR-004, FR-005 / SC-001 |
| T005–T006 | FR-006, FR-007 / SC-002 |
| T007–T008, T010 | SC-001, SC-002, SC-003 |
| T009 | FR-002 (on-device shim presence) / SC-004 support |
| T011–T012 | Class-C gating, §XIII.5 |
