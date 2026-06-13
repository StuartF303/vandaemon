# Feature Specification: Plugin Architecture & Two-Tier Split

**Feature Branch**: `004-plugin-architecture`
**Created**: 2026-06-13
**Status**: Draft
**Input**: Prepared feature brief `.spec-drafts/0001-plugin-architecture.brief.md` (authoritative). Establish the foundational two-tier plugin model for the VanDaemon head-unit experience: a stable, loading-mechanism-agnostic UI plugin contract; a plugin host/registry that discovers, registers, and renders UI plugins (compiled-in initially); a stable, transport-agnostic native capability bridge contract; a no-op stub bridge for off-device development; and one reference UI plugin proving the seam end-to-end against the existing API.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Build a UI plugin against a stable contract (Priority: P1)

A UI-plugin developer implements a single contract to describe a presentation plugin (its identity, the component that renders it, and optional display metadata). The plugin host discovers it, lists it, and renders it — without the plugin knowing or caring how it was loaded.

**Why this priority**: This contract is the seam every later feature (launcher shell, individual tiles, hardware UIs) depends on. Pinning it first prevents downstream features from re-deciding the boundary.

**Independent Test**: Implement a fake plugin against the contract, register it, and confirm the host discovers it and yields its render entry — fully testable with `dotnet test`, no device required.

**Acceptance Scenarios**:

1. **Given** a fake UI plugin implementing the contract, **When** it is registered with the host, **Then** the host reports it as a discoverable plugin and exposes its render entry, identity, and display metadata.
2. **Given** N registered UI plugins, **When** the host enumerates plugins, **Then** it reports exactly N and yields each plugin's render entry in a deterministic order.
3. **Given** the UI plugin contract, **When** it is inspected, **Then** it carries no assumption about a loading mechanism (compiled-in, manifest, lazy, or sideloaded).

---

### User Story 2 - Reach native capability only through a transport-agnostic bridge (Priority: P1)

A UI-plugin developer needs vehicle/native capabilities (reverse-cam state, ACC state, wheel-key events, "open Teyes DSP"). The developer calls an abstract bridge contract — never native, filesystem, or hardware APIs directly. Off-device, a no-op stub satisfies the same contract so plugins build and run on a desktop.

**Why this priority**: The bridge is the *only* sanctioned path from a UI plugin to native capability (constitution §VI). Defining it as an abstract, transport-agnostic contract now prevents hardware access from leaking into the WebView layer later.

**Independent Test**: A stub bridge satisfies the interface; calling each capability returns the stubbed value with no WebView/native dependency present — verified with `dotnet test` on a desktop.

**Acceptance Scenarios**:

1. **Given** the native bridge contract, **When** a stub implementation is provided, **Then** it satisfies the interface and every declared capability returns a defined stub value without any native/WebView dependency.
2. **Given** a UI plugin that needs a native capability, **When** it accesses that capability, **Then** the access goes *only* through the bridge contract — the plugin has no direct native/hardware/filesystem reference available to it.
3. **Given** the bridge contract, **When** it is inspected, **Then** it is transport-agnostic — neither JS-interop nor a local socket/HTTP transport is assumed, and either could implement it without changing plugin code.

---

### User Story 3 - Prove the seam end-to-end, off-device (Priority: P2)

A developer runs the whole Blazor app with one reference UI plugin (a simple system-status / single-tank tile) on a desktop, using the stub bridge and the existing VanDaemon API. This proves the off-device development path and exercises the contract end-to-end.

**Why this priority**: A trivial-but-real plugin is the proof the contracts actually compose. Its value is exercising the seam, not the feature it shows.

**Independent Test**: The reference plugin renders against the stub bridge and a mocked API client; `dotnet build` succeeds and `dotnet test` is green; no head-unit or native dependency is required.

**Acceptance Scenarios**:

1. **Given** the reference UI plugin registered in the host, **When** the app is built and tested off-device with the stub bridge, **Then** `dotnet build` succeeds and `dotnet test` passes.
2. **Given** the reference UI plugin, **When** it renders, **Then** it uses only the UI plugin contract, the VanDaemon API client, and the bridge contract — with no direct hardware/native/file access.

---

### Edge Cases

- **No plugins registered**: the host reports zero plugins and yields an empty, non-error result (callers render an empty state).
- **Duplicate plugin identity**: registration of two plugins with the same id is rejected with a clear error rather than silently overwriting (assumption — see Assumptions; to be confirmed in `/clarify`).
- **Capability unavailable off-device**: a stub capability returns a defined "unavailable"/default value and never throws, so off-device builds stay green.
- **Plugin attempts native access outside the bridge**: prevented by contract shape (Tier-2 plugins have no native references in scope); where practical, a test/analyzer check enforces it.

## Clarifications

A `/clarify` scan found no material ambiguity unanswered by the brief; the feature stays Class A
(.NET/Blazor, test-backed). Two low-impact items from brief §9 are resolved here with informed
defaults and are flagged for confirmation at the plan gate (no separate clarify gate, per playbook §2):

- **Contract placement (resolved → default)**: new contracts live in a dedicated UI-abstractions
  project (proposed `VanDaemon.Plugins.Ui.Abstractions`) mirroring the existing
  `VanDaemon.Plugins.Abstractions` hardware-tier split. Confirm exact name/placement at the gate.
- **Reference-plugin API target (resolved → yes)**: the reference UI plugin targets the simplest
  existing API surface (a single tank / system-status read) to minimise coupling.
- **Duplicate plugin identity (resolved → reject)**: registering two plugins with the same id fails
  with a clear error rather than silently overwriting.

## Requirements *(mandatory)*

### Functional Requirements

*(Derived verbatim from brief §7 acceptance criteria; FR numbering preserves that mapping.)*

- **FR-001**: The system MUST define a UI plugin contract with the minimal surface to identify, register, and render a plugin (e.g. id/name, an entry component, optional icon/ordering). A fake plugin implementing the contract MUST be discoverable by the registry.
- **FR-002**: The plugin host MUST enumerate all registered UI plugins and expose them for rendering. Given N registered plugins, the host MUST report N and yield each plugin's render entry.
- **FR-003**: The UI plugin contract MUST be independent of the loading mechanism (it MUST NOT assume compiled-in, manifest-driven, lazy-loaded, or sideloaded loading). Initial loading MUST be compiled-in (project references registered at startup).
- **FR-004**: The system MUST define a native capability bridge contract that is transport-agnostic, describing the capabilities the Kotlin shell will later expose (reverse-cam state, ACC state, wheel-key events, "open Teyes DSP"). A stub implementation MUST satisfy the interface and return stubbed values without any WebView/native dependency.
- **FR-005**: The system MUST provide a no-op / stub bridge implementation for off-device development so the Blazor app and plugins build and run on a desktop without the head unit present.
- **FR-006**: The system MUST include one reference UI plugin (a simple system-status or single-tank tile) that renders using only the UI plugin contract, the VanDaemon API, and the bridge stub — with no direct hardware/native access. Attempts to reach native capabilities MUST go only through the bridge interface.
- **FR-007**: The whole solution MUST build and run off-device (desktop) using the stub bridge, with `dotnet build` succeeding and `dotnet test` green, proving the off-device development path.
- **FR-008**: No Tier-2 (UI) plugin code MUST reference native, hardware, or filesystem APIs directly. This MUST be enforced by the contract shape and, where practical, by a test/analyzer check.
- **FR-009**: The two-tier split MUST be preserved: Tier-1 server-side hardware plugins (`ISensorPlugin`/`IControlPlugin`) remain the canonical hardware tier and MUST NOT be changed by this feature; Tier-2 UI plugins present data the API exposes and never reach hardware directly.

### Key Entities

- **UI Plugin (Tier-2)**: A head-unit presentation unit. Attributes: stable identity (id/name), an entry render component, optional display metadata (icon, ordering). No native/hardware/filesystem access.
- **Plugin Host / Registry**: Enumerates registered UI plugins and yields their render entries for the Blazor host. Compiled-in registration initially; load-agnostic by contract.
- **Native Capability Bridge (contract)**: The single, transport-agnostic seam from UI plugins to native/vehicle capabilities. Declares capabilities (reverse-cam state, ACC state, wheel-key events, open Teyes DSP) abstractly.
- **Stub Bridge**: An off-device implementation of the bridge contract returning defined default values; carries no native/WebView dependency.
- **Reference UI Plugin**: A minimal plugin (system-status / single-tank tile) that exercises the contract end-to-end against the existing API and the stub bridge.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A new UI plugin can be added and appear in the host through registration alone, with no change to the host's enumeration/rendering code.
- **SC-002**: The entire solution builds and 100% of the feature's acceptance tests pass off-device, with no head-unit or native dependency present.
- **SC-003**: 100% of UI-plugin access to native capability goes through the bridge contract — zero direct native/hardware/filesystem references exist in Tier-2 plugin code.
- **SC-004**: Introducing a future loading mechanism (manifest/lazy/sideloaded) requires no change to the UI plugin contract, because the contract carries no loading-mechanism assumption.
- **SC-005**: Tier-1 server-side hardware plugins are unchanged by this feature (no edits to `ISensorPlugin`/`IControlPlugin` or their implementations).

## Assumptions

- **Namespace/placement** of the new contracts (e.g. a `VanDaemon.Plugins.Ui.Abstractions` project analogous to `VanDaemon.Plugins.Abstractions`) is an implementation detail proposed in `/plan`, not assumed here (brief §9). Default proposal: a dedicated UI-abstractions project to mirror the existing hardware-abstractions split.
- **Reference plugin scope**: targets the simplest existing API surface (a single tank/status read) to minimise coupling (brief §9 recommendation: yes).
- **Duplicate plugin ids** are rejected at registration with a clear error (reasonable default; confirm in `/clarify`).
- **Bridge transport** is deliberately undecided in this feature; the contract is abstract and the only implementation delivered now is the stub (brief §6.2 defers transport to `/plan`).
- Engineering conventions follow the repo CLAUDE.md / constitution §VII (Clean Architecture, `Async` suffix, `CancellationToken`, nullable enabled, structured logging, xUnit + FluentAssertions + Moq).

## Out of Scope / Needs-Hardware

- The Kotlin launcher shell itself (this feature defines the contract the shell will implement, not the shell).
- Any real native capability implementation (reverse/ACC/wheel-key wiring) — **needs-hardware-verification**; belongs to the launcher feature.
- Manifest-driven / lazy-loaded / sideloaded plugin loading (future additive path enabled by the load-agnostic contract).
- Final choice of bridge transport (deferred to `/plan`, ideally after a small on-device spike).
- Any change to the server-side Tier-1 hardware plugins.
- Anything requiring root, flashing, or the physical unit (constitution §IV — out of scope, not looped).
