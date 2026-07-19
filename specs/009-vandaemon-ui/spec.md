# Feature Specification: VanDaemon UI (unified UI-launcher app)

**Feature Branch**: `009-vandaemon-ui`
**Created**: 2026-07-18
**Status**: Draft
**Supersedes**: `007-tablet-client` (reframed from "separate Capacitor app" to a flavor of the one app)
**Governance**: Constitution v2.1.0 (Part II). Risk class **B** (Android artifact; no on-device claim by the loop).

## Overview

VanDaemon is **one Android app** that is always a launcher (home app) and always presents the VanDaemon
UI. Devices differ by **capability, not identity**: a **tablet** is UI-only; a **head unit** can also
host the control daemons locally and run the vehicle bridge. In the owner's configuration every device
points at the daemons on the **Pi**, so all devices run as UI clients.

This feature delivers **v1**: the app as a **UI launcher pointing at a configured controller**, built
as a `tablet` product flavor of the existing Kotlin shell (`app/`). It reuses the shell's WebView host
but loads the **live** UI served by the Pi over LAN instead of bundled assets, adds a resilient native
connection screen, and registers as a soft-default home app. On-device daemons and the vehicle bridge
are **out of scope for v1** (deferred head-unit capabilities); on a tablet the bridge is simply absent,
so the Pi-served UI's `NativeBridgeFactory` selects the stub with no app-side work.

App label: **"VanDaemon UI"**. `applicationId`: **`dev.vandaemon.ui`** (permanent). Target: Play Store
**internal testing**.

## User Scenarios & Testing

### User Story 1 — The tablet is the VanDaemon device (P1)
Set VanDaemon UI as the home app once; from then on Home/boot shows the live Pi UI. Monitor and control
exactly as in a browser, on a device carried around the van.

**Acceptance**: (a) selectable as home app via standard settings, removing/disabling nothing; (b) with
the Pi reachable, Home/boot shows the live dashboard; (c) live values update in real time; (d) other
apps remain usable and reachable, returning via Home.

### User Story 2 — Never a dead screen when the Pi isn't ready (P1)
As the home app it comes up first on power-up — often before the Pi is up. A native "Waiting for
VanDaemon" screen shows instead of a blank/broken page, auto-connects when the Pi responds, and offers
manual Retry.

**Acceptance**: (a) Pi unreachable → clear connection screen, never blank/raw error; (b) Pi becomes
reachable → auto-transition to the UI, no owner action; (c) Retry re-attempts immediately.

### User Story 3 — Point at the right controller (P2)
Default target is `vandaemon.local:8080`; the owner can override the address and it persists.

**Acceptance**: (a) fresh install targets the default with no config; (b) a saved valid address is used
after restart; (c) an unreachable address keeps the connection screen with a way to correct it.

### Edge Cases
- **Host up but UI not yet serving** → treated as unreachable; keep waiting until the UI actually
  responds (probe the controller, not just a ping).
- **LAN drop mid-session** → return to the connection screen; auto-recover when back in range.
- **`vandaemon.local` doesn't resolve** → the owner enters an IP on the connection screen (persisted).
- **Reversing home-app choice** → standard settings fully restore prior behaviour (no irreversible change).

## Requirements

- **FR-001**: Present the live UI served by the Pi over LAN (real-time updates included) without hosting
  or bundling that UI.
- **FR-002**: Be selectable/settable as the tablet's home (default launcher) app via standard settings.
- **FR-003**: Home is a **soft default** — other apps stay usable and reachable; return via Home.
- **FR-004**: Fully reversible via standard settings; remove/disable no other app; no device-owner
  provisioning or root.
- **FR-005**: When the controller is unreachable, show a clear connection/waiting screen — never a blank
  screen or raw browser error.
- **FR-006**: Auto-detect when the controller becomes reachable and transition to the UI without owner
  action.
- **FR-007**: The connection screen offers a manual Retry.
- **FR-008**: "Reachable" means the **VanDaemon UI actually responds** (probe `/health` on the
  controller), not merely that the host is present.
- **FR-009**: Target a sensible default (`vandaemon.local:8080`) on first run with no configuration.
- **FR-010**: The owner can change the controller address; it persists across restarts (native store,
  since the UI can't be loaded to configure itself when the Pi is down).
- **FR-011**: If the LAN drops while the UI is shown, return to the connection screen and auto-recover.
- **FR-012**: Keep the display awake while VanDaemon UI is the active foreground screen.
- **FR-013**: Integrate **no** vehicle signals and host **no** daemons in v1 (deferred head-unit
  capabilities). The tablet flavor injects no native bridge; the Pi-served UI falls back to its stub.
- **FR-014**: Ship as the `tablet` product flavor of `app/` with `applicationId = dev.vandaemon.ui`,
  label "VanDaemon UI", without changing the existing `headunit` (005) build.

### Key Entities
- **Controller address** — host + port of the Pi (default `vandaemon.local:8080`), owner-editable,
  persisted natively. The only state the tablet client owns.

## Success Criteria

- **SC-001** *(Class B, build)*: `bundleTabletRelease` produces a signed `.aab` with `applicationId
  dev.vandaemon.ui` and label "VanDaemon UI". `assembleHeadunitDebug` still builds (005 unaffected).
- **SC-002** *(on-device, human — not claimed by the loop)*: With the Pi reachable, VanDaemon UI can be
  set as home app and Home/boot lands on the live dashboard.
- **SC-003** *(on-device, human)*: Starting with the Pi unreachable shows the connection screen (never
  blank); powering the Pi on transitions to the UI unaided.
- **SC-004** *(on-device, human)*: Changing the address and restarting still targets the new address.

## Risk Class (loop-playbook §4)

- **Class B** — Android artifact. The loop builds the signed AAB and verifies it builds; it does **not**
  claim on-device behaviour (SC-002..004 are human-run) and does **not** upload to Play (human-gated:
  Console app entry, Play App Signing, upload). No self-publish.

## Out of Scope / Deferred
- On-device daemons (running the VanDaemon backend on the head unit) — separate feasibility problem.
- Vehicle bridge / signals on the head unit — the deferred bridge-wiring feature.
- Full kiosk / device-owner lockdown; bundling/offline-caching the UI; auth or off-LAN/cloud access.
- mDNS **service discovery** via NsdManager — v1 relies on OS resolution of `vandaemon.local` or a
  configured IP; NsdManager discovery is a fast-follow if OS resolution proves unreliable in testing.

## Assumptions
- Modern Android tablet with a current WebView; no old-WebView constraints (unlike 005).
- The Pi advertises/serves at `vandaemon.local:8080` (appliance/006 concern); the editable IP is the
  fallback.
- Local trusted van LAN, no auth (consistent with the offline-first, local-only posture).
