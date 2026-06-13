# Implementation Plan: Plugin Architecture & Two-Tier Split

**Branch**: `004-plugin-architecture` | **Date**: 2026-06-13 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/004-plugin-architecture/spec.md`

## Summary

Deliver the foundational two-tier plugin seam for the VanDaemon head-unit experience: a stable,
loading-mechanism-agnostic **UI plugin contract**, a **plugin host/registry** that discovers and
renders compiled-in UI plugins, a **transport-agnostic native capability bridge contract**
(`INativeBridge`) with a **no-op stub** for off-device development, and **one reference UI plugin**
(single-tank/status tile) proving the seam end-to-end against the existing API. Approach: mirror the
existing `VanDaemon.Plugins.Abstractions` hardware-tier split with a new
`VanDaemon.Plugins.Ui.Abstractions` contracts project; put the registry, stub bridge, mockable API
client, and reference plugin in a Razor Class Library (`VanDaemon.Plugins.Ui`) so they are unit- and
component-testable off-device; wire them into `VanDaemon.Web`. Exit condition is `dotnet build` +
`dotnet test` green against FR-001..FR-009.

## Technical Context

**Language/Version**: C# / .NET 10
**Primary Dependencies**: Blazor WebAssembly (Microsoft.AspNetCore.Components 10.0), MudBlazor 6.11;
contracts depend on `System.Type` only (no Blazor package in the abstractions project)
**Storage**: N/A (no persistence added; reference plugin reads via the existing API)
**Testing**: xUnit + FluentAssertions + Moq; bUnit for Blazor component rendering tests
**Target Platform**: Browser/WASM on the head unit (old WebView, weak SoC) and desktop for off-device dev
**Project Type**: Web (Blazor WASM frontend over the existing ASP.NET Core API)
**Performance Goals**: Robust load on a constrained old WebView → compiled-in registration (no
runtime fetch/lazy-load tax in this feature)
**Constraints**: Off-device buildable/testable (stub bridge, mocked API client); no native/hardware/
filesystem access from UI plugins; Tier-1 hardware plugins unchanged
**Scale/Scope**: One contracts project, one RCL (registry + stub bridge + api client + 1 reference
plugin), one test project, minimal Web wiring. Foundational seam, not a plugin catalogue.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Requirement | This plan |
|-----------|-------------|-----------|
| §I.3 / §VI.2 Two-tier split | No hardware access in WebView-hosted UI plugins | UI projects reference only Ui.Abstractions + Core + the API client; **no** reference to `VanDaemon.Plugins.Abstractions` or any hardware/native/IO API. A test asserts this. ✓ |
| §VI.1 / §VI.4 Bridge is the only native path; bridge is a contract | UI→native only via the bridge; bridge surface is a contract | `INativeBridge` is the sole native seam; defined as an abstract contract in Ui.Abstractions; only a stub implements it here. ✓ |
| §I Plugin-first; Tier-1 unchanged | Hardware tier remains canonical, untouched | No edits to `ISensorPlugin`/`IControlPlugin` or implementations (SC-005). ✓ |
| §VII Engineering standards | .NET 10, Clean layering, `Async` suffix, `CancellationToken`, nullable, structured logging, xUnit+FluentAssertions+Moq | All adopted; registry uses `ILogger`; async bridge/api methods carry `CancellationToken`. ✓ |
| §VIII.2/§VIII.5 Objective exit; no green-washing | `dotnet test` is the exit; nothing unverifiable claimed done | Class A; every FR maps to a test (see tasks). No hardware items here. ✓ |
| §IV Safety/Reversibility | No irreversible/on-device actions | None in scope; real bridge transport + on-device wiring explicitly deferred. ✓ |

**Result: PASS** (initial). No violations → Complexity Tracking left empty. Re-checked post-design below.

## Project Structure

### Documentation (this feature)

```text
specs/004-plugin-architecture/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output (contract markdown)
│   ├── IUiPlugin.md
│   └── INativeBridge.md
└── tasks.md             # Phase 2 output (/speckit.tasks — NOT created here)
```

### Source Code (repository root)

```text
src/
├── Backend/
│   └── VanDaemon.Plugins/
│       ├── VanDaemon.Plugins.Abstractions/        # EXISTING Tier-1 (hardware) — unchanged
│       └── VanDaemon.Plugins.Ui.Abstractions/     # NEW — Tier-2 contracts (Microsoft.NET.Sdk)
│           ├── IUiPlugin.cs                        #   UI plugin contract (Type-based render entry)
│           ├── INativeBridge.cs                    #   transport-agnostic native bridge contract
│           ├── AccState.cs                         #   capability value types (enum)
│           └── WheelKeyEvent.cs                    #   wheel-key event payload
└── Frontend/
    └── VanDaemon.Plugins.Ui/                       # NEW — RCL (Microsoft.NET.Sdk.Razor)
        ├── Hosting/
        │   ├── IUiPluginRegistry.cs                #   registry abstraction
        │   └── UiPluginRegistry.cs                 #   enumerates/render-entries registered plugins
        │   └── UiPluginHost.razor                  #   renders enumerated plugins via DynamicComponent
        ├── Bridge/
        │   └── StubNativeBridge.cs                 #   no-op INativeBridge for off-device dev
        ├── Api/
        │   ├── IVanDaemonApiClient.cs              #   mockable typed API client (GetTanksAsync)
        │   ├── HttpVanDaemonApiClient.cs           #   HttpClient-backed implementation
        │   └── TankDto.cs                          #   DTO for the tank read
        ├── ReferencePlugin/
        │   ├── SystemStatusTile.razor              #   reference UI plugin component
        │   └── SystemStatusUiPlugin.cs             #   its IUiPlugin descriptor
        └── ServiceCollectionExtensions.cs          #   AddVanDaemonUiPlugins(...) DI helper

src/Frontend/VanDaemon.Web/
└── Program.cs                                       # MODIFIED — register registry, stub bridge, api client, reference plugin

tests/
└── VanDaemon.Plugins.Ui.Tests/                     # NEW — xUnit + FluentAssertions + Moq + bUnit
    ├── UiPluginRegistryTests.cs                    #   FR-001, FR-002, FR-003
    ├── StubNativeBridgeTests.cs                    #   FR-004, FR-005
    ├── ReferencePluginTests.cs                     #   FR-006 (bUnit render against stub + mocked API)
    └── TwoTierIsolationTests.cs                    #   FR-008/FR-009 (no hardware/native refs in UI assembly)
```

**Structure Decision**: Web application. New code lives in two new projects mirroring the existing
plugin split — `VanDaemon.Plugins.Ui.Abstractions` (contracts, plain SDK, `System.Type` render entry
so the abstraction carries no Blazor/loader dependency) and `VanDaemon.Plugins.Ui` (Razor Class
Library holding the registry, stub bridge, mockable API client, and reference plugin). Both are
referenced by `VanDaemon.Web` and by a new `VanDaemon.Plugins.Ui.Tests` project, keeping the whole
seam unit/component-testable off-device. All four are added to `VanDaemon.sln`.

## Complexity Tracking

> No constitution violations — section intentionally empty.

## Post-Design Constitution Re-check

After Phase 1 design (contracts + data model below): still **PASS**. The `Type`-based render entry
keeps the contract loader-agnostic (SC-004); `INativeBridge` remains the sole native seam with only a
stub implementation (deferring transport per §VI/brief §6.2); the UI RCL references no hardware/native
APIs and a dedicated test enforces it (§I.3/§VI.2, FR-008). Two new projects + one test project is the
minimum to keep contracts loader-agnostic *and* testable off-device — no simpler structure achieves
both, so Complexity Tracking stays empty.
