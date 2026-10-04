# Tasks: Plugin Architecture & Two-Tier Split

**Feature**: `004-plugin-architecture` | **Class**: A (.NET/Blazor, test-backed)
**Input**: plan.md, spec.md, research.md, data-model.md, contracts/ in `specs/004-plugin-architecture/`
**Exit condition**: `dotnet build VanDaemon.sln` succeeds AND `dotnet test` green against FR-001..FR-009.

## Acceptance-criterion → test mapping

| Spec criterion / FR | Test (task) |
|---|---|
| FR-001 UI plugin contract; fake plugin discoverable | `UiPluginRegistryTests.FakePlugin_IsDiscoverable` (T009) |
| FR-002 host enumerates N plugins, yields each render entry | `UiPluginRegistryTests.Enumerates_All_Registered_InOrder` (T009) |
| FR-003 contract is loading-mechanism-agnostic | `UiPluginRegistryTests.Contract_HasNoLoaderAssumption` (T009) |
| FR-004 bridge contract transport-agnostic; stub satisfies it | `StubNativeBridgeTests.Stub_Satisfies_Contract_NoNativeDep` (T015) |
| FR-005 stub returns defined values, never throws (off-device) | `StubNativeBridgeTests.Stub_Returns_Defined_Defaults` (T015) |
| FR-006 reference plugin renders via contract + mocked API + stub | `ReferencePluginTests.Renders_With_StubBridge_And_MockedApi` (T024) |
| FR-007 builds & tests green off-device | T026, T027 (build + test) |
| FR-008 no native/hardware/file refs in Tier-2 | `TwoTierIsolationTests.UiAssembly_References_NoHardwareOrNative` (T016) |
| FR-009 Tier-1 hardware plugins unchanged | `TwoTierIsolationTests` + T028 (git-diff guard) |

---

## Phase 1: Setup (project scaffolding)

- [x] T001 Create `VanDaemon.Plugins.Ui.Abstractions` project (`Microsoft.NET.Sdk`, `net10.0`, `Nullable`+`ImplicitUsings` enabled, no Blazor package) at `src/Backend/VanDaemon.Plugins/VanDaemon.Plugins.Ui.Abstractions/VanDaemon.Plugins.Ui.Abstractions.csproj`
- [x] T002 Create `VanDaemon.Plugins.Ui` Razor Class Library (`Microsoft.NET.Sdk.Razor`, `net10.0`, `Nullable`+`ImplicitUsings`) at `src/Frontend/VanDaemon.Plugins.Ui/VanDaemon.Plugins.Ui.csproj` with package refs `Microsoft.AspNetCore.Components.WebAssembly` 10.0, `MudBlazor` 6.11.2, and project refs to `VanDaemon.Plugins.Ui.Abstractions` and `VanDaemon.Core`
- [x] T003 Create test project `VanDaemon.Plugins.Ui.Tests` (`net10.0`) at `tests/VanDaemon.Plugins.Ui.Tests/VanDaemon.Plugins.Ui.Tests.csproj` with `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, `FluentAssertions`, `Moq`, `bunit`; project refs to `VanDaemon.Plugins.Ui` and `VanDaemon.Plugins.Ui.Abstractions`
- [x] T004 Add all three new projects to `VanDaemon.sln` (`dotnet sln VanDaemon.sln add ...`)
- [x] T005 Add project references from `src/Frontend/VanDaemon.Web/VanDaemon.Web.csproj` to `VanDaemon.Plugins.Ui` and `VanDaemon.Plugins.Ui.Abstractions`

**Checkpoint**: `dotnet build VanDaemon.sln` succeeds with empty new projects.

---

## Phase 2: Foundational (shared contract value types — blocks US1 & US2)

- [x] T006 [P] Add `AccState` enum (`Unknown=0, Off, On`) in `src/Backend/VanDaemon.Plugins/VanDaemon.Plugins.Ui.Abstractions/AccState.cs`
- [x] T007 [P] Add `WheelKey` enum + `WheelKeyEvent` record (`Key`, `TimestampUtc`) in `src/Backend/VanDaemon.Plugins/VanDaemon.Plugins.Ui.Abstractions/WheelKeyEvent.cs`

**Checkpoint**: abstractions project compiles with the shared value types.

---

## Phase 3: User Story 1 — Build a UI plugin against a stable contract (Priority: P1)

**Goal**: A loader-agnostic `IUiPlugin` contract + a registry that discovers, orders, and yields plugins.
**Independent test**: register fake plugins → registry reports them in order; duplicate id rejected; contract has no loader assumption. (`dotnet test`, no device.)

- [x] T008 [P] [US1] Add `IUiPlugin` contract (`Id`, `Name`, `Type ComponentType`, `string? Icon`, `int Order`) per `contracts/IUiPlugin.md` in `src/Backend/VanDaemon.Plugins/VanDaemon.Plugins.Ui.Abstractions/IUiPlugin.cs`
- [x] T009 [P] [US1] Add `IUiPluginRegistry` contract (`IReadOnlyList<IUiPlugin> Plugins`, `void Register(IUiPlugin)`) in `src/Backend/VanDaemon.Plugins/VanDaemon.Plugins.Ui.Abstractions/IUiPluginRegistry.cs`
- [x] T010 [US1] Write `UiPluginRegistryTests` (xUnit + FluentAssertions) covering FR-001/002/003 + duplicate-id rejection in `tests/VanDaemon.Plugins.Ui.Tests/UiPluginRegistryTests.cs`
- [x] T011 [US1] Implement `UiPluginRegistry` (in-memory; DI-injected `IEnumerable<IUiPlugin>`; ordered by `Order` then `Id`; throws on duplicate `Id`; `ILogger<UiPluginRegistry>` structured logging) in `src/Frontend/VanDaemon.Plugins.Ui/Hosting/UiPluginRegistry.cs`
- [x] T012 [US1] Implement `UiPluginHost.razor` rendering each `registry.Plugins` via `<DynamicComponent Type="plugin.ComponentType" />` in `src/Frontend/VanDaemon.Plugins.Ui/Hosting/UiPluginHost.razor`

**Checkpoint**: `dotnet test` green for `UiPluginRegistryTests`; US1 independently demonstrable.

---

## Phase 4: User Story 2 — Native bridge contract + stub, with isolation guard (Priority: P1)

**Goal**: A transport-agnostic `INativeBridge` contract + a no-op stub; enforce no hardware/native leak.
**Independent test**: stub satisfies the contract returning defined defaults with no native dependency; a guard test proves the UI assembly references no hardware/native assembly. (`dotnet test`, no device.)

- [x] T013 [US2] Add `INativeBridge` contract (`GetReversingStateAsync`, `GetAccStateAsync`, `OpenDspAsync` with `CancellationToken`; `event EventHandler<WheelKeyEvent>? WheelKeyPressed`) per `contracts/INativeBridge.md` in `src/Backend/VanDaemon.Plugins/VanDaemon.Plugins.Ui.Abstractions/INativeBridge.cs`
- [x] T014 [US2] Write `StubNativeBridgeTests` (FR-004/005: satisfies interface, returns `false`/`Unknown`/no-op, never throws, no native dep) in `tests/VanDaemon.Plugins.Ui.Tests/StubNativeBridgeTests.cs`
- [x] T015 [US2] Implement `StubNativeBridge` (no-op defaults: reversing=`false`, ACC=`Unknown`, `OpenDspAsync` completes, event wirable) in `src/Frontend/VanDaemon.Plugins.Ui/Bridge/StubNativeBridge.cs`
- [x] T016 [US2] Write `TwoTierIsolationTests` (FR-008/009): reflect over `VanDaemon.Plugins.Ui` assembly's `GetReferencedAssemblies()` and assert none is `VanDaemon.Plugins.Abstractions` or a native/hardware assembly, in `tests/VanDaemon.Plugins.Ui.Tests/TwoTierIsolationTests.cs`

**Checkpoint**: `dotnet test` green for `StubNativeBridgeTests` + `TwoTierIsolationTests`.

---

## Phase 5: User Story 3 — Reference plugin proves the seam end-to-end, off-device (Priority: P2)

**Goal**: One reference UI plugin (single-tank/status tile) rendering via contract + mockable API + stub bridge, wired into the Web host.
**Independent test**: bUnit render of the tile against a mocked `IVanDaemonApiClient` and `StubNativeBridge` succeeds; app builds. (`dotnet test`, no device.)

- [x] T017 [P] [US3] Add `TankDto` (`Id`, `Name`, `Type`, `CurrentLevel`, `Capacity`) in `src/Frontend/VanDaemon.Plugins.Ui/Api/TankDto.cs`
- [x] T018 [P] [US3] Add `IVanDaemonApiClient` (`Task<IReadOnlyList<TankDto>> GetTanksAsync(CancellationToken)`) in `src/Frontend/VanDaemon.Plugins.Ui/Api/IVanDaemonApiClient.cs`
- [x] T019 [US3] Implement `HttpVanDaemonApiClient` wrapping `HttpClient` `GET api/tanks` (case-insensitive + `JsonStringEnumConverter`) in `src/Frontend/VanDaemon.Plugins.Ui/Api/HttpVanDaemonApiClient.cs`
- [x] T020 [US3] Implement `SystemStatusTile.razor` (injects `IVanDaemonApiClient` + `INativeBridge` ONLY; shows first tank level; no native/file refs) in `src/Frontend/VanDaemon.Plugins.Ui/ReferencePlugin/SystemStatusTile.razor`
- [x] T021 [US3] Implement `SystemStatusUiPlugin : IUiPlugin` (`Id="system-status"`, `ComponentType=typeof(SystemStatusTile)`) in `src/Frontend/VanDaemon.Plugins.Ui/ReferencePlugin/SystemStatusUiPlugin.cs`
- [x] T022 [US3] Implement `AddVanDaemonUiPlugins(this IServiceCollection)` DI extension registering `IUiPluginRegistry`→`UiPluginRegistry`, `INativeBridge`→`StubNativeBridge`, `IVanDaemonApiClient`→`HttpVanDaemonApiClient`, and `IUiPlugin`→`SystemStatusUiPlugin` in `src/Frontend/VanDaemon.Plugins.Ui/ServiceCollectionExtensions.cs`
- [x] T023 [US3] Write `ReferencePluginTests` (bUnit): render `SystemStatusTile` with `Mock<IVanDaemonApiClient>` + `StubNativeBridge`, assert it renders the tank and reaches native only via the bridge (FR-006) in `tests/VanDaemon.Plugins.Ui.Tests/ReferencePluginTests.cs`
- [x] T024 [US3] Wire `builder.Services.AddVanDaemonUiPlugins();` into `src/Frontend/VanDaemon.Web/Program.cs` (and optionally drop `<UiPluginHost />` reference availability; no behavioral page change required)

**Checkpoint**: `dotnet test` green for `ReferencePluginTests`; app builds with the plugin registered.

---

## Phase 6: Polish & exit-condition verification

- [x] T025 [P] Add XML-doc/structured-logging polish and confirm conventions (Async suffix, `CancellationToken`, nullable) across new files
- [x] T026 Run `dotnet build VanDaemon.sln` — must succeed with zero new warnings (FR-007)
- [x] T027 Run `dotnet test VanDaemon.sln` — all new tests green; map results back to the FR table above (FR-007 exit)
- [x] T028 Guard FR-009/SC-005: `git diff --stat` shows **no** changes under `src/Backend/VanDaemon.Plugins/VanDaemon.Plugins.Abstractions/` or any hardware plugin project

---

## Dependencies & order

- **Setup (T001–T005)** → blocks everything.
- **Foundational (T006–T007)** → blocks US1 (T008 uses nothing from it) and US2 (T013 uses `AccState`/`WheelKeyEvent`).
- **US1 (T008–T012)** and **US2 (T013–T016)** are independent of each other after Foundational (both P1). US1 contract (`IUiPlugin`) is reused by US3.
- **US3 (T017–T024)** depends on US1 (`IUiPlugin`) and US2 (`INativeBridge`, `StubNativeBridge`).
- **Polish (T025–T028)** last.

## Parallel execution examples

- After T005: `T006` ∥ `T007` (different files).
- US1: `T008` ∥ `T009` (different contract files); then T010 (test) then T011/T012.
- US2: `T013` then `T014` ∥ `T016` (different test files) alongside T015.
- US3: `T017` ∥ `T018` (DTO + interface) before T019/T020.

## Implementation strategy

- **MVP = US1 + US2** (both P1): the two contracts + registry + stub + isolation guard are the seam every later feature needs. US3 (P2) is the end-to-end proof.
- Class A profile: `/implement` feature-at-once, iterate build→test→fix to green, self-merge on green (playbook §2).
- **Stop-and-report** if any task turns out to need on-device behaviour, a real bridge transport, or a Tier-1 change (reclassify per playbook §5) — none expected.
