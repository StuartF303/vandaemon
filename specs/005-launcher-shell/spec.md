# Feature Specification: VanDaemon Launcher Shell (first pass — Tier-0 Kotlin shell hosting WASM UI)

**Feature Branch**: `005-launcher-shell`
**Created**: 2026-06-22
**Status**: Draft
**Constitution**: v2.1.0 (`.specify/memory/constitution.md`) — authoritative on any conflict
**Authoritative input**: `.spec-drafts/0004-launcher-shell.brief.md`
**Risk class**: Mixed **B + C**, **Human-Verified** (loop playbook §4) — no automated check can prove a WebView renders WASM on the unit; the deliverable is a tested, installable debug APK **plus** an on-hardware verification checklist, never a claim of on-device success.

## Overview

This feature delivers the **first pass of the native (Kotlin) half** of VanDaemon's hybrid head-unit architecture: a thin, sideloadable Android app that hosts the existing VanDaemon Blazor/WASM UI inside a WebView and exposes the native capability bridge (`INativeBridge`, pinned by feature 004) to that UI. It deliberately stops short of *being* the launcher — it does not become the home screen, does not touch the stock Teyes launcher, and reads no real vehicle signals. Its single purpose is to prove the **WebView + WASM + bridge seam** end-to-end: off-vehicle by automated tests, and on the FYT unit by a human-run checklist.

## Clarifications

### Session 2026-06-22

- Q: Shell directory + Gradle/Kotlin toolchain target (FR-013)? → A: Top-level `app/` directory at repo root; Kotlin + Android Gradle Plugin **current stable**; `minSdk 29` (Android 10) **provisional**, confirmed/adjusted against the §8 on-hardware fingerprint.
- Q: How should the published WASM get into the APK assets (FR-014)? → A: **Documented two-step** — `dotnet publish VanDaemon.Web` → copy `wwwroot` into the shell's assets → `gradle assemble`; driven by a documented script, Gradle stays decoupled from the .NET toolchain.
- Q: Stock-WebView version threshold for pre-deciding to go AOT (FR-015)? → A: **Defer** — set no numeric threshold now; record the unit's actual WebView version during the §8 on-hardware check first, and let the observed version drive the AOT decision if WASM fails to render.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Hosted WASM UI renders inside the shell (Priority: P1)

As the developer (Stuart), I install the shell APK on a dev machine/emulator (and later the FYT unit) and the VanDaemon Blazor UI loads and renders inside the app's WebView, served entirely from app-bundled assets with no network or server.

**Why this priority**: This is the load-bearing assumption of the whole hybrid architecture. If the bundled WASM UI cannot be served to and rendered by the WebView, nothing else in the head-unit programme is viable. It is also where the single biggest on-hardware risk lives (old stock WebView, constitution §VII.5).

**Independent Test**: Build the APK, install on an emulator, launch it, and observe the WASM UI's known root element / document title appear — provable by an instrumented test asserting the start URL resolves from the virtual app-assets origin and a known DOM marker is present. Delivers a demonstrably-installable app that renders the real UI offline.

**Acceptance Scenarios**:

1. **Given** the published Blazor WASM is bundled as APK assets, **When** the app launches, **Then** the WebView loads the start document from the virtual app-assets origin and the VanDaemon UI's known root element / title is present.
2. **Given** no network connectivity and no local server process, **When** the app launches, **Then** the UI still loads fully from bundled assets (offline-first, constitution §III).
3. **Given** the WebView requests app resources (`.wasm`, `.dll`/framework files, `.js`, `.css`), **When** they are served via the asset loader, **Then** they are returned from the virtual same-origin URL with correct MIME types so fetch/WASM and same-origin rules behave.

---

### User Story 2 - UI ↔ native bridge round-trips through the WebView (Priority: P1)

As the developer, I confirm that the WASM UI can call each member of the `INativeBridge` contract and receive the defined stub value back, and that the shell can push a steering-wheel-key event into the running page — all across the real WebView JS-interop boundary.

**Why this priority**: The bridge is the *only* sanctioned path from Tier-2 UI plugins to native/vehicle capability (constitution §XI.1–2). Proving the round-trip works (even with stub values) validates the transport that 004 deliberately left abstract, and unblocks every later feature that wires real signals onto it.

**Independent Test**: An instrumented test drives a UI-side call to each bridge member and asserts the injected Kotlin object returns the stub value; a shell-pushed `WheelKeyPressed` event is asserted to arrive at the page. Tested against stub values only — no vehicle required.

**Acceptance Scenarios**:

1. **Given** the injected native bridge object, **When** the UI calls `GetReversingStateAsync`, **Then** it receives `false`.
2. **Given** the injected native bridge object, **When** the UI calls `GetAccStateAsync`, **Then** it receives `AccState.Unknown`.
3. **Given** the injected native bridge object, **When** the UI calls `OpenDspAsync`, **Then** the call completes as a no-op without error.
4. **Given** the shell pushes a `WheelKeyPressed` event with a `WheelKeyEvent(Key, TimestampUtc)`, **When** it is delivered to the page, **Then** the UI receives the event with the same key and timestamp payload shape.
5. **Given** the native bridge surface exposed to the UI, **When** it is compared against the 004 `INativeBridge` contract, **Then** the members, enum values, and record shape match exactly with no drift.

---

### User Story 3 - The shell stays thin and coexistence-ready (Priority: P2)

As the maintainer, I want the Kotlin layer to remain a thin host (WebView host, asset loader, JS-interop bridge, lifecycle) with no application logic, and built so a later HOME/coexistence milestone can extend it without rework.

**Why this priority**: Constitution §XII.4 caps the Kotlin layer at "a few hundred lines"; application logic must stay in C#/Blazor. Keeping the seam thin and modular is what makes the deferred launcher-takeover work additive rather than a rewrite.

**Independent Test**: Review/measure the Kotlin source against a stated line-count budget and confirm it contains only host/transport/lifecycle code (no business logic, no hardware access). Confirm the app declares itself an ordinary app (no `HOME`/`DEFAULT` intent category).

**Acceptance Scenarios**:

1. **Given** the Kotlin source, **When** its responsibilities are reviewed, **Then** it contains only WebView host, asset loader, JS-interop bridge, and lifecycle — no application/business logic and no direct hardware/vehicle access.
2. **Given** the app manifest, **When** its intent filters are inspected, **Then** it does **not** declare the `HOME`/`DEFAULT` category and installs/launches as an ordinary sideloaded app.

---

### Edge Cases

- **Stock WebView too old to run modern Blazor WASM** (constitution §VII.5, ANECDOTAL): off-vehicle the emulator carries a current WebView so tests pass; on the unit this may fail. This is an expected *on-hardware* outcome to be **surfaced, not assumed away** — captured by the §8 checklist, and the trigger for the deferred AOT/brotli mitigation decision.
- **Asset MIME / same-origin handling**: serving WASM from a `file://` or wrong-MIME origin breaks fetch/streaming compilation; the virtual same-origin app-assets origin exists specifically to avoid this. A failure here must surface as a resolvable test failure, not a silent blank page.
- **Bridge contract drift**: if the Kotlin bridge surface diverges from the 004 C# contract (extra/missing member, renamed enum value, changed record), the drift test must fail rather than the mismatch reaching runtime.
- **Bridge called before the page/bridge is ready**: a UI call issued before injection completes must fail safe (no crash); behaviour is asserted by test.
- **WheelKey value outside the known set**: only the enumerated `WheelKey` values are valid; an unknown maps to `WheelKey.Unknown` (mirrors the C# enum default).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The shell MUST build to an installable **debug APK** via Gradle on a developer machine / CI without a connected vehicle.
- **FR-002**: The shell MUST bundle the **published** VanDaemon Blazor WASM (`src/Frontend/VanDaemon.Web` output) as APK assets and serve it to a WebView via an asset loader from a **virtual same-origin app-assets origin**, so the UI runs fully offline with no server process on the unit.
- **FR-003**: The WebView MUST load the bundled UI such that WASM/framework assets are returned with correct MIME types and same-origin semantics (fetch / streaming WASM compilation succeed).
- **FR-004**: The shell MUST implement the **004 `INativeBridge` contract** as the native side of the seam, exposing it to the WASM UI over JS-interop (an injected `@JavascriptInterface`-style native object for UI→native calls; shell→UI event push via script evaluation).
- **FR-005**: The native bridge implementation MUST return the **same safe stub/echo defaults** as the 004 `StubNativeBridge`: `GetReversingStateAsync` → `false`; `GetAccStateAsync` → `AccState.Unknown`; `OpenDspAsync` → completes as a no-op; `WheelKeyPressed` → wirable but not raised by the shell on its own (raised only when explicitly pushed, e.g. by a test).
- **FR-006**: The bridge surface exposed by the shell MUST match the 004 `INativeBridge` contract **exactly** — same members, same `AccState` / `WheelKey` enum values, same `WheelKeyEvent(Key, TimestampUtc)` record shape — verified by a **drift / contract-comparison test**.
- **FR-007**: The shell MUST NOT change the `INativeBridge` contract surface in any way (that is a 004-level spec change, constitution §XI.4).
- **FR-008**: The Kotlin layer MUST stay **thin** — only WebView host, asset loader, JS-interop bridge, and lifecycle — with **no application/business logic** and **no direct native/filesystem/hardware access for the UI** (constitution §XI.2, §XII.4). A line-count budget MUST be stated and honoured.
- **FR-009**: The app MUST install and launch as an **ordinary sideloaded app**. It MUST NOT declare the `HOME`/`DEFAULT` intent category and MUST NOT replace, disable, or remove the stock Teyes launcher (constitution §VII.3, §X).
- **FR-010**: All real-vehicle bridge behaviours (real reversing state, real ACC state, real "open Teyes DSP" intent, real wheel-key events) MUST be documented as **needs-reverse-engineering / needs-hardware-verification** (constitution §XI.3), not implemented in this feature.
- **FR-011**: The feature MUST ship the **on-hardware verification checklist** (below) as the Class-C deliverable; Class-C items MUST be reported as **blocked-on-hardware**, never as done (constitution §XIII.5, no green-washing).
- **FR-012**: The off-vehicle automated tests (Class-B) MUST be runnable without a vehicle and MUST cover: asset-load/start-URL resolution, each bridge member's stub round-trip, the shell→UI event push, and the contract drift check.

### Key Entities

- **Launcher shell (Kotlin app)**: The thin native host. Responsibilities: bundle + serve the WASM UI, host the WebView, inject and operate the bridge, manage app lifecycle. Non-responsibilities: application logic, vehicle/hardware access, being the home screen.
- **Bundled WASM UI**: The published `VanDaemon.Web` output, embedded as read-only app assets. Authored elsewhere; not modified by this feature.
- **Native bridge implementation**: The Kotlin realisation of `INativeBridge`, returning `StubNativeBridge`-equivalent defaults. Surface is a **contract**, pinned to 004.
- **`INativeBridge` contract (from 004, unchanged here)**: members `GetReversingStateAsync() → bool`, `GetAccStateAsync() → AccState`, `OpenDspAsync() → void`, event `WheelKeyPressed → WheelKeyEvent`; `AccState { Unknown, Off, On }`; `WheelKey { Unknown, VolumeUp, VolumeDown, Next, Previous, Voice, ModeSwitch }`; `WheelKeyEvent(Key, TimestampUtc)`.

## Success Criteria *(mandatory)*

### Measurable Outcomes — Class B (objectively checkable off-vehicle / emulator)

- **SC-001**: The shell builds to an installable debug APK via Gradle from a clean checkout, off-vehicle. *(brief AC-1)*
- **SC-002**: With the app installed on an emulator and launched, the bundled VanDaemon UI loads from the virtual app-assets origin and a known root element / document title is present — asserted by an instrumented test, fully offline. *(brief AC-2)*
- **SC-003**: Every `INativeBridge` member called from the UI reaches the injected native object and returns its defined stub value, and a shell-pushed `WheelKeyPressed` event is delivered to the page — asserted against stub values with no vehicle. *(brief AC-3)*
- **SC-004**: The shell's bridge surface matches the 004 `INativeBridge` contract exactly, proven by a passing drift/contract-comparison test (0 mismatches). *(brief AC-4)*
- **SC-005**: The Kotlin layer contains no application logic and stays within the stated line-count budget; the manifest declares no `HOME`/`DEFAULT` category. *(brief AC-5)*

### Measurable Outcomes — Class C (require the FYT unit; **blocked-on-hardware**, NOT auto-verified)

- **SC-006** *(blocked-on-hardware)*: The APK installs on the FYT unit (`adb install`) and the unit's WebView renders the Blazor WASM UI — resolving the §VII.5 old-WebView risk one way or the other. *(brief AC-6)*
- **SC-007** *(blocked-on-hardware)*: A bridge call issued from the running WASM UI round-trips through the **unit's real WebView** and returns the stub value. *(brief AC-7)*

## On-Hardware Verification Checklist *(Class-C deliverable — Stuart runs on the unit; constitution §XIII.3)*

- [ ] Record exact model + SoC + Android version + build fingerprint (`adb shell getprop | grep -iE "fyt|ums512|7862|fingerprint|product"`) and stock WebView version (`adb shell dumpsys package com.google.android.webview | grep versionName`).
- [ ] APK sideloads (`adb install`) and launches as an **ordinary app** (NOT as home screen).
- [ ] The WebView **renders** the VanDaemon Blazor WASM UI on the unit — note pass/fail; if fail, record the WebView version + console errors (this resolves the §VII.5 risk).
- [ ] A **bridge call from the running UI round-trips** through the unit's real WebView (stub value returns).
- [ ] If WASM does **NOT** render: capture failure detail (WebView version, console/log errors) to drive the AOT/brotli or native-host mitigation decision. **Do not** proceed to the HOME milestone until this passes.
- [ ] Confirm **nothing** about the stock Teyes launcher or vehicle settings is touched by installing/running the app.

## Out of Scope / Deferred *(constitution §XIII.4 — explicit)*

- **Becoming the launcher**: declaring the `HOME`/`DEFAULT` intent category or replacing/disabling the stock Teyes launcher. Deferred to a later milestone gated on Phase-1 root/launcher-replacement feasibility (constitution §X, §IX).
- **Real vehicle signals**: actual reverse-cam/ACC state, real wheel-key events, real "open Teyes DSP" intent — all `needs-reverse-engineering` (constitution §X.3, §XI.3); belong to the bridge-wiring feature after on-unit recon.
- **Autostart / persistence**: registration in the autostart list and `skipkillapp.prop` (constitution §VII.4, §X.4) — relevant only once the app is the launcher.
- **Background-app survival across sleep / ACC-off** — separate Phase-1 concern.
- **Any change to the `INativeBridge` contract** — a 004-level spec change (constitution §XI.4).
- **AOT / brotli build pipeline** beyond plain `dotnet publish` into assets — escalate to AOT only if the on-hardware check (SC-006) shows the stock WebView cannot run standard publish output.
- **Root, flash, or any irreversible step** — out of scope; would be Class D (constitution §IX.4), never executed by the loop.

## Assumptions

- The emulator/CI environment carries a **modern WebView**, so Class-B render/round-trip tests are meaningful off-vehicle; the unit's older WebView is the explicit unknown that SC-006/007 exist to test.
- The published `VanDaemon.Web` output is self-hostable as static assets with no server-side dependency at runtime (consistent with its Blazor WASM nature and constitution §III).
- "A few hundred lines" is interpreted as a soft budget on the order of ≤ ~500 lines of Kotlin for the shell's host/transport/lifecycle code; the exact figure is fixed at planning.
- The platform is the **FYT / Unisoc UMS512 (ums9230-class)** unit per the constitution's platform correction (§VII.1); the brief's "Allwinner" label is superseded. Build-specific facts are confirmed from the §8 fingerprint before they are relied upon.

### Resolved Decisions *(from Clarifications — brief §9)*

- **FR-013**: The shell MUST live in a top-level **`app/`** directory at the repo root (sibling to `src/`, `hw/`), built with **Kotlin + Android Gradle Plugin at current stable**. `minSdk` is **29 (Android 10) provisionally**, to be confirmed or adjusted against the §8 on-hardware fingerprint before release; a mismatch with the unit's Android is a hardware-confirmation item, not a blocker for off-vehicle build/test.
- **FR-014**: The build MUST use a **documented two-step pipeline**: (1) `dotnet publish` of `VanDaemon.Web`, (2) copy the published `wwwroot` into the shell's APK assets, (3) `gradle assemble`. This is driven by a documented script; Gradle does **not** invoke the .NET toolchain, keeping the Android build decoupled from a .NET SDK on the build host.
- **FR-015**: The project sets **no numeric stock-WebView threshold** in this feature. The unit's actual WebView version MUST be **recorded first** during the §8 on-hardware check; only if WASM fails to render does that observed version drive the (deferred, out-of-scope here) AOT/brotli escalation decision.
