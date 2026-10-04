# Phase 0 Research: Plugin Architecture & Two-Tier Split

All Technical Context unknowns were pre-resolved by the brief; this records the decisions and the
rejected alternatives so they are not re-litigated.

## R1. UI plugin render entry — how a plugin exposes its component

- **Decision**: The `IUiPlugin` contract exposes the component as a `System.Type ComponentType`
  (a Blazor component type), rendered by the host via `DynamicComponent`.
- **Rationale**: Keeps `VanDaemon.Plugins.Ui.Abstractions` free of any Blazor package dependency and,
  critically, free of any *loading-mechanism* assumption (FR-003 / SC-004) — a `Type` can come from a
  compiled-in reference today or a dynamically loaded assembly later, with no contract change.
- **Alternatives considered**: `RenderFragment` property (couples the abstraction to
  `Microsoft.AspNetCore.Components`, and is awkward to construct outside a component); a string
  component name resolved by reflection (stringly-typed, no compile-time safety).

## R2. UI-plugin loading mechanism

- **Decision**: Compiled-in registration (project references registered in DI at startup), per brief
  §6.1. The contract stays loader-agnostic.
- **Rationale**: Most robust on the unit's old WebView / weak SoC; no network dependency at load.
  Manifest/lazy/sideloaded remain future additive paths enabled by the loader-agnostic contract.
- **Alternatives considered**: API-served manifest + Blazor lazy-load (cold-start tax + load-time API
  dependency at the weakest point); sideloaded onto the unit (file management fights FYT firmware +
  task-killer). Both rejected for now; neither is precluded later.

## R3. Native bridge transport

- **Decision**: Define `INativeBridge` as an abstract, transport-agnostic contract; deliver only a
  no-op `StubNativeBridge` in this feature. No JS-interop or socket/HTTP transport implemented now.
- **Rationale**: Brief §6.2 + constitution §VI.4 — the bridge surface is a contract; transport is an
  on-device concern (Class C/D) chosen later, ideally after an on-device spike. Implementing transport
  now would smuggle on-device work into a Class A feature (playbook §4).
- **Alternatives considered**: JS interop over WebView `@JavascriptInterface` (couples to WebView host);
  local WebSocket/HTTP to the shell (more processes to keep alive vs the OEM task-killer). Deferred.

## R4. Bridge capability shape (sync vs async; events)

- **Decision**: State reads are async methods with `CancellationToken` (`GetReversingStateAsync`,
  `GetAccStateAsync`); the action is async (`OpenDspAsync`); wheel-key input is a C# `event`
  (`WheelKeyPressed`). Values use small DTO/enum types with an explicit `Unknown`/unavailable member.
- **Rationale**: Matches repo §VII async conventions; a future socket transport is inherently async;
  an `Unknown` member lets the stub return a defined value off-device without throwing (edge case).
- **Alternatives considered**: Synchronous properties (don't fit a future async transport);
  `IObservable<T>` for keys (heavier dependency than a plain event for this minimal seam).

## R5. Reference plugin data source & testability

- **Decision**: Introduce a mockable `IVanDaemonApiClient` (e.g. `GetTanksAsync`) implemented by an
  `HttpVanDaemonApiClient` wrapping the existing `HttpClient`/`api/tanks` call. The reference plugin
  (single-tank/status tile) depends on the interface + `INativeBridge`.
- **Rationale**: FR-006 requires rendering "against a mocked API client"; `HttpClient` is awkward to
  mock directly, an interface is clean and matches Clean Architecture (§V/§VII). Reuses the existing
  `api/tanks` surface (brief §9 recommendation).
- **Alternatives considered**: Mock `HttpClient` via `HttpMessageHandler` (works but noisier in every
  test); call `HttpClient` directly in the component (not mockable, couples UI to transport).

## R6. Enforcing "no hardware/native/file access in Tier-2" (FR-008)

- **Decision**: Enforce structurally (the UI projects reference neither
  `VanDaemon.Plugins.Abstractions` nor any native/IO hardware API) plus a guard test that reflects
  over the `VanDaemon.Plugins.Ui` assembly's referenced assemblies and asserts none is a hardware-tier
  / native assembly.
- **Rationale**: "where practical, a test/analyzer check" (FR-008). A referenced-assembly assertion is
  a real, cheap, deterministic test — no analyzer package needed.
- **Alternatives considered**: A Roslyn analyzer (heavier to build/maintain for one rule);
  convention-only (no objective check — would risk green-washing, §VIII.5).

## R7. Component render testing

- **Decision**: Use bUnit for the reference-plugin render test (FR-006 / AC4).
- **Rationale**: bUnit is the standard off-device Blazor component test harness; keeps the feature
  Class A (objective `dotnet test`). Registry/bridge/isolation tests are plain xUnit.
- **Alternatives considered**: Manual `RenderTreeBuilder` assertions (brittle); skip render test
  (would fail to objectively cover AC4 — unacceptable, §VIII.5).
