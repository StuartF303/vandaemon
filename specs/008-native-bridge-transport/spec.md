# Feature Specification: Native Bridge Transport (C# JS-interop realisation)

**Feature Branch**: `008-native-bridge-transport`
**Created**: 2026-07-18
**Status**: Draft
**Governance**: Constitution v2.1.0 (Part II — head-unit sub-project). Loop-playbook risk class **mixed A/C**.
**Input**: Complete the **C# half** of the 004 `INativeBridge` JS-interop transport so a bridge call
issued from the running WASM UI actually crosses into the Kotlin shell's injected
`window.VanDaemonNativeBridge` — making **005 SC-007** an observable, on-device check instead of an
unobservable one. Stub values only; no real vehicle signals; no change to the 004 contract.

## Overview

Feature 005 shipped the launcher shell that hosts the Blazor/WASM UI in a WebView and injects the
Kotlin `NativeBridge` as `window.VanDaemonNativeBridge`. The **Kotlin (native) half** of the
transport is complete and tested. The **C# (UI) half was never built**: the Blazor app registers
`INativeBridge` → `StubNativeBridge` (a pure-C# no-op that never touches JavaScript), and the one
component that calls the bridge (`SystemStatusTile`) is registered in DI but rendered on no page.

The consequence is that **005 SC-007** — "a bridge call from the running UI round-trips through the
unit's real WebView" — has nothing to observe: no C# code calls across the WebView, and the
instrumented test that "passes" drives `window.VanDaemonNativeBridge` directly via the test harness,
bypassing the seam it is meant to certify.

This feature builds the missing C# half:

1. A **JS-interop `INativeBridge` implementation** that calls `window.VanDaemonNativeBridge` for all
   four contract members, per the 005 `contracts/js-interop-bridge.md` wire shape.
2. A small **transport shim** (`vandaemon-bridge.js`) that owns the `@JavascriptInterface` member
   names, wraps synchronous native returns as awaitable Promises, and routes the native→UI wheel-key
   push into .NET.
3. A **transport-detection factory** that, at first bridge resolution, checks whether the native
   object is present, **logs which implementation it selected**, and returns the JS-interop bridge on
   a real unit or the stub off-device. Because a Blazor WASM `ILogger` writes to `console.*` and the
   shell forwards console to logcat, the selection is visible in `adb logcat -s VanDaemonShell:*`.
4. **Rendering the reference tile** on the Devices page and **hardening its API call** so a
   backend-absent unit shows an offline line rather than Blazor's unhandled-error bar (which the
   install guide reads as "WebView too old" — a false SC-006 failure).

This is **not** the deferred real-signals feature: every member still returns its defined stub. The
value delivered is that the transport is now **end-to-end and observable**, so on-device SC-006/SC-007
produce a real pass/fail signal.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - SC-007 becomes a real on-device check (Priority: P1)

When the app runs on the FYT unit, a bridge call from the WASM UI crosses into the injected native
object and returns its stub value, and the app logs which transport it selected — so the human
verifier can confirm the round-trip from the same logcat stream they watch for SC-006.

**Why this priority**: This is the entire point — without the C# half, SC-007 cannot be observed on
hardware and the head-unit backlog's SC-006 branch stays blocked on an unmeasurable gate.

**Independent Test**: On a device where `window.VanDaemonNativeBridge` is injected, open the page that
renders the reference tile and confirm the log line `native transport detected … JsInteropNativeBridge`
appears and the tile renders using the value returned across the bridge.

**Acceptance Scenarios**:

1. **Given** the native object is present, **When** the bridge is first resolved, **Then** the app
   selects the JS-interop implementation and logs that selection at info level.
2. **Given** the native object is absent (desktop/browser/off-device), **When** the bridge is first
   resolved, **Then** the app selects the stub implementation and logs that selection.
3. **Given** the JS-interop bridge is selected, **When** the UI calls each of the four contract
   members, **Then** each call reaches `window.VanDaemonNativeBridge` and returns its defined stub
   value (reversing `false`, ACC `Unknown`, `openDsp` no-op, wheel-key event delivered).

---

### User Story 2 - No false "WebView too old" failure (Priority: P1)

The reference tile renders on a real page and, when no VanDaemon backend is reachable on the unit
(expected in this first pass), shows a clear offline line instead of an unhandled exception.

**Why this priority**: The install guide tells the verifier that Blazor's unhandled-error bar means
the WebView is too old (drives the AOT/brotli decision). An unguarded API call on the backend-less
unit would trip that bar and manufacture a false SC-006 failure — corrupting the exact signal this
work exists to make trustworthy.

**Independent Test**: Render the tile with the API unreachable and confirm it shows an offline line,
not an error boundary.

**Acceptance Scenarios**:

1. **Given** the tile is on a rendered page and the API is unreachable, **When** it initialises,
   **Then** it shows an offline/unavailable line and does not surface an unhandled exception.
2. **Given** the API is reachable, **When** the tile initialises, **Then** it shows the first tank's
   name and level as before.

---

### Edge Cases

- **Bridge called before injection/readiness**: a UI→native call issued before the native object
  exists must fail safe (fall back to the stub's defined default), never crash the app (contract G5).
- **`IJSInProcessRuntime` unavailable** (non-WASM host, e.g. a prerender or a unit test with an async
  runtime): detection treats "cannot synchronously probe" as "no native transport" and selects the
  stub — the safe default.
- **Unknown enum names on the wire** (`AccState`, `WheelKey`): parsed leniently, unknown → `Unknown`
  (per the 005 contract), never an exception.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The UI MUST reach native capability **only** through the 004 `INativeBridge` contract;
  this feature MUST NOT add, remove, or rename any contract member or enum value (no §XI.4 change).
- **FR-002**: A JS-interop `INativeBridge` implementation MUST call `window.VanDaemonNativeBridge` for
  all four members using the wire shape defined in `specs/005-launcher-shell/contracts/js-interop-bridge.md`
  (reversing → JSON boolean; ACC → string name; `openDsp` → no-op resolve; wheel-key → push into .NET).
- **FR-003**: A transport shim MUST own the `@JavascriptInterface` member names, expose a synchronous
  `hasNative()` probe, wrap native returns so the C# side can `await` them, and marshal
  `window.VanDaemonBridgeEvents.onWheelKey` back into .NET.
- **FR-004**: On first resolution the bridge selection MUST detect native-transport presence, select
  the JS-interop implementation when present and the stub when absent, and **log the selected
  implementation at info level** so it appears in the shell's forwarded console log.
- **FR-005**: Selection MUST fail safe: if the native object cannot be synchronously probed, the stub
  is selected. A pre-readiness UI→native call MUST NOT crash the app.
- **FR-006**: The reference UI plugin (`SystemStatusTile`) MUST be rendered on a normal content page
  so the bridge is exercised during on-device verification.
- **FR-007**: The tile's data fetch MUST be guarded so an unreachable backend yields an offline line,
  never an unhandled exception / Blazor error boundary.
- **FR-008**: All native values remain **stubs** — this feature integrates no real vehicle signal
  (reverse, ACC, wheel-key, DSP). Real values remain `needs-reverse-engineering`, out of scope.

### Key Entities

- **Transport selection**: the one-time decision "native present → JS-interop bridge, else stub,"
  plus the log record of that decision. Owns no persisted state.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001** *(Class A, off-device)*: With a fake JS runtime reporting the native object present, the
  factory returns the JS-interop bridge and logs the selection; reporting it absent returns the stub
  and logs the selection. Verified by `dotnet test`.
- **SC-002** *(Class A, off-device)*: The reference tile, rendered with a throwing API client, shows
  an offline line and raises no unhandled exception. Verified by bUnit in `dotnet test`.
- **SC-003** *(Class A, off-device)*: `dotnet build` and `dotnet test` for the solution are green.
- **SC-004** *(Class C, on-device — Stuart runs on the unit; not claimed by the loop)*: On the FYT
  unit the log shows `JsInteropNativeBridge` selected, and a UI→native call returns its stub value
  across the real WebView — **this is the now-observable 005 SC-007**. Recorded via
  `adb logcat -s VanDaemonShell:*`.

## Risk Class *(loop-playbook §4)*

- **Class A** — the C# bridge, detection factory, DI wiring, tile placement, and tile hardening are
  all `dotnet test`-backed and verifiable off-device. May run Trusted-Test; delivered green.
- **Class C** — the on-device round-trip through the unit's real WebView (SC-004 / 005 SC-007) has no
  automated check; it is a human-verified checklist item on the FYT unit. **No on-device success is
  claimed by the loop; no self-merge — delivered as a reviewed PR.**
- The JS shim itself is not directly `dotnet test`-covered; it is exercised by the extended Android
  instrumented test (Class B artifact) and by SC-004 on hardware.

## Out of Scope / Deferred

- **Real vehicle signals** (actual reverse/ACC/wheel-key/DSP) — `needs-reverse-engineering`; the
  separate bridge-wiring feature after on-unit recon.
- **Any change to the 004 `INativeBridge` contract** — a 004-level spec change (§XI.4).
- **Launcher/HOME, persistence, AOT/brotli** — separate deferred features per the head-unit roadmap.
- **Root / flash / any irreversible step** — Class D, never executed by the loop.

## Assumptions

- Target runtime is Blazor **WebAssembly**, so `IJSRuntime` can be used as `IJSInProcessRuntime` for a
  synchronous presence probe at first resolution (after the Blazor runtime is up).
- The 005 Kotlin shell is unchanged and continues to inject `window.VanDaemonNativeBridge` and forward
  WebView console to logcat under tag `VanDaemonShell`.
- The reference tile is a legitimate product surface (the Tier-2 seam demonstrator from 004), not a
  throwaway debug artifact.
