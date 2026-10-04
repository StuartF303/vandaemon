# Feature Brief — Plugin Architecture & Two-Tier Split

> **How to use this file.** This is the prepared input for Spec Kit's `/specify` command for the
> first feature. Paste/reference its content into `/specify`. It is written to pre-answer the
> questions `/clarify` would otherwise ask: scope, the two-tier boundary, the stable contracts, the
> deferred-to-`/plan` decisions (with trade-offs recorded), acceptance criteria, and an explicit
> out-of-scope / needs-hardware section.
>
> Written against **Constitution v0.1.0**. Where this brief and the constitution disagree, the
> constitution wins.
>
> Status: DRAFT (pending Stuart review).

---

## 1. Summary / intent

Establish the foundational plugin architecture for the VanDaemon head-unit experience: a **two-tier
plugin model** with a stable, loading-mechanism-agnostic contract for head-unit UI plugins, and a
stable contract for the native↔WASM capability bridge. This feature is the seam every later feature
(launcher shell, individual UI plugins, hardware tiles) depends on. It delivers the **contracts and
a minimal working reference implementation**, not a full plugin catalogue.

## 2. Why this first

Every downstream spec hangs off these two contracts (the UI-plugin interface and the bridge
interface). Pinning them now prevents later features re-deciding the boundary and prevents the
classic failure where hardware access leaks into the WebView layer.

## 3. The two-tier model (the architectural spine — from Constitution §I.3)

- **Tier 1 — Hardware plugins (server-side).** Sensors/controls (I2C, Modbus, Victron, etc.) live in
  the VanDaemon API/host where they can physically reach hardware. These already exist as
  `ISensorPlugin` / `IControlPlugin` in the repo and are **out of scope to change** here, except to
  confirm they remain the canonical hardware tier. No new hardware plugin is built in this feature.
- **Tier 2 — UI / presentation plugins (head unit).** Rendered in the Blazor app hosted by the
  Kotlin WebView shell. They present data and controls, calling the VanDaemon API and the native
  bridge. They have **no** direct native, filesystem, or hardware access (Constitution §VI.2).
- **The split is by where the capability physically lives.** A tile that shows Victron battery state
  is a Tier-2 UI plugin that reads data the Tier-1 Victron hardware plugin exposes via the API.
  These tiers are never merged.

## 4. In scope (this feature delivers)

1. A stable **UI plugin contract** (`IUiPlugin` or equivalent) the Blazor host uses to discover,
   register, and render UI plugins — defined so it is **independent of the loading mechanism**.
2. A **plugin host/registry** in the Blazor app that enumerates registered UI plugins and renders
   them (navigation/tiles). Initial loading is **compiled-in** (see §6).
3. A stable **native capability bridge contract** (`INativeBridge` or equivalent on the Blazor side)
   describing the capabilities the Kotlin shell will expose (reverse-cam state, ACC state,
   wheel-key events, "open Teyes DSP activity", etc.), **abstracted from transport** (see §6).
4. A **no-op / stub bridge implementation** for off-device development, so the Blazor app and
   plugins build and run on a desktop without the head unit present.
5. **One reference UI plugin** (e.g. a simple "system status" or single-tank tile) proving the
   contract end-to-end against the existing API. Trivial functionally; its job is to exercise the
   seam.
6. Acceptance tests (see §7).

## 5. Out of scope / explicitly deferred

- The Kotlin launcher shell itself (separate feature; this feature defines the *contract* the shell
  will implement, not the shell).
- Any real native capability implementation (reverse/ACC/wheel-key wiring) — those are
  **needs-hardware-verification** and belong to the launcher feature.
- Manifest-driven / lazy-loaded / sideloaded plugin loading (future additive path — see §6.1).
- Final choice of bridge transport (deferred to `/plan` — see §6.2).
- Any change to the server-side Tier-1 hardware plugins.
- Anything requiring root, flashing, or the physical unit.

## 6. Decisions framed for `/plan` (recorded trade-offs so they aren't re-litigated in `/clarify`)

### 6.1 UI-plugin loading mechanism — DECIDED: compiled-in first, contract stays load-agnostic
The plugin contract MUST NOT assume a loading mechanism. Initial implementation loads plugins
**compiled-in** (project references registered at startup), chosen for runtime robustness on the
unit's old WebView and weak SoC. Manifest-driven lazy-loading and sideloading remain **future
additive paths**, enabled by the load-agnostic contract, not required now.

Trade-offs on record (so `/clarify` need not ask):
- *Compiled-in (chosen):* fastest/most robust at runtime, no network dependency at load, easiest on
  old WebView; cost is rebuild+redeploy to add/change a plugin — acceptable for a single-author
  personal system.
- *API-served manifest, lazy-loaded:* central updates, aligns with VanDaemon's API model; cost is
  per-load network fetch + Blazor lazy-load cold-start tax at the weakest point (old WebView/slow
  SoC) and a load-time dependency on the API being reachable.
- *Sideloaded onto unit:* offline-capable; worst fit — file management on a unit governed by FYT
  firmware + task-killer, loses central updates. Not recommended.

### 6.2 Native↔WASM bridge transport — DEFERRED to `/plan` (spec both)
Define the bridge as an **abstract contract** now; choose transport in `/plan`, ideally after a
small on-device spike. Candidates and trade-offs on record:
- *JS interop over WebView (`@JavascriptInterface`):* standard, synchronous-friendly, well-trodden
  for WebView-hosted apps; tightly coupled to the WebView host.
- *Local WebSocket/HTTP to the shell:* more decoupled and testable off-device; more moving parts and
  another process to keep alive against the task-killer.
The contract MUST be transport-agnostic so either can implement it without changing plugin code.

## 7. Acceptance criteria (testable; .NET/Blazor layer — implementation loop exit condition is `dotnet test` green)

1. A `UiPlugin` contract exists with the minimal surface to identify, register, and render a plugin
   (e.g. id/name, an entry component, optional icon/ordering). Unit test: a fake plugin implementing
   the contract is discoverable by the registry.
2. The plugin host enumerates all registered UI plugins and exposes them for rendering. Unit test:
   given N registered plugins, the host reports N and yields each's render entry.
3. The native bridge contract exists and is transport-agnostic. Unit test: a stub bridge satisfies
   the interface; calling a capability returns the stubbed value without any WebView/native
   dependency.
4. The reference UI plugin renders using only the contract + API + bridge stub, with no direct
   hardware/native access. Test: it compiles and renders against the stub bridge and a mocked API
   client; attempts to reach native capabilities go *only* through the bridge interface.
5. The whole thing **builds and runs off-device** (desktop) using the stub bridge — proving the
   off-device development path. (`dotnet build` + `dotnet test` green.)
6. No Tier-2 plugin code references native/hardware/file APIs directly (enforced by the contract
   shape and, where practical, a test/analyzer check).

## 8. Constraints inherited from the constitution (do not violate)

- Two-tier split is mandatory; no hardware access in WebView-hosted plugins (§I.3, §VI.2).
- Bridge is the *only* path from UI plugins to native capability; bridge surface is a contract, not
  an incidental detail (§VI.1, §VI.4).
- Follow repo .NET conventions: Clean Architecture layering, `Async` suffix, `CancellationToken`,
  structured logging, nullable enabled, JSON-serialisable `Dictionary<string,object>` config (§VII).
- Tests: xUnit + FluentAssertions + Moq, Arrange/Act/Assert; acceptance criteria are the loop's exit
  condition (§VII.3, §VIII.2).
- Self-contained spec; carry context, contracts, acceptance criteria, and out-of-scope/needs-hardware
  (§VIII.4). No green-washing (§VIII.5).

## 9. Open questions for `/clarify` (kept deliberately short)

- Naming/namespace placement for the UI-plugin and bridge contracts within the existing solution
  structure (e.g. a new `VanDaemon.Plugins.Ui.Abstractions` analogous to
  `VanDaemon.Plugins.Abstractions`?). Propose, don't assume.
- Whether the reference UI plugin should target the simplest existing API surface (e.g. a single
  tank/status read) to minimise coupling — recommend yes.
