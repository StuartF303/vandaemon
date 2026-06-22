# VanDaemon Head-Unit — Roadmap & Backlog

> Status snapshot after merging **005-launcher-shell** (first-pass Kotlin launcher shell).
> Governed by Constitution v2.1.0 (Part II). This file is the living backlog for the head-unit
> sub-project — what's done, what's blocked on hardware, and what comes next.
>
> Last updated: 2026-06-22 (PR #18 merged to `main`).

## Where we are

- ✅ **005 launcher shell (first pass) — merged.** Class-B green off-vehicle: builds to a debug
  APK, unit + instrumented tests pass, the Blazor/WASM UI renders in a modern emulator WebView,
  and the 004 `INativeBridge` contract round-trips over JS-interop with stub values.
- ⏳ **On-hardware verification is still OUTSTANDING.** Merging integrated the code; it did **not**
  prove the app works on the FYT unit. SC-006 / SC-007 remain open (see below). No on-device
  success is claimed (Constitution §XIII.5).

## 1. Immediate — verify 005 on the real unit (the open gate)

Run the install + verification flow in [`installation.md`](./installation.md) (§5–§8). Records to
capture:

- [ ] Exact model / SoC / Android / build fingerprint (`getprop`) and **stock WebView version**.
- [ ] **SC-006** — the unit's WebView renders the VanDaemon UI (resolves the §VII.5 old-WebView risk).
- [ ] **SC-007** — a bridge call from the running UI round-trips through the unit's WebView.
- [ ] Stock Teyes launcher / vehicle settings untouched by install/run.

The outcome of SC-006 decides which branch of the backlog we take next.

## 2. Conditional — if the stock WebView can't run the WASM

If SC-006 fails (blank screen / WASM error — capture `adb logcat -s VanDaemonShell:*` + WebView
version):

- [ ] **New feature: AOT + brotli build pipeline** (FR-015 escalation). Pre-compile the Blazor app
  and pre-compress assets so an older WebView can run it; if even that fails, fall back to hosting
  components natively. Class B + C. Deferred until the on-hardware data says it's needed — don't
  build it speculatively.

## 3. Next features (deferred — gated on on-unit recon)

In rough priority order. Each is its own Spec Kit feature.

- [ ] **Bridge wiring — real vehicle signals.** Replace the stubs with real `INativeBridge`
  behaviour: reverse-cam trigger, ACC on/off, steering-wheel keycodes, "open Teyes DSP" intent.
  Every binding is FYT-specific and **needs-reverse-engineering on the physical unit** (§X.3,
  §XI.3). The JS-interop transport is already proven by 005, so this is wiring + recon, not new
  plumbing. Class C (human-verified, no autonomous success claims).

- [ ] **Launcher / HOME milestone.** Declare the `HOME`/`DEFAULT` intent category, coexist with the
  stock Teyes launcher (never remove it — §VII.3), and forward to its activities/intents for vehicle
  settings / DSP / reverse-cam. Reversible-first (no root) — set via `ro.fyt.launcher` / default-app
  selection (§IX.3, §X.1–2). Class C.

- [ ] **Persistence / survival across sleep & ACC-off.** Register the app in the autostart list and
  `skipkillapp.prop` so the OEM task-killer doesn't cull it (§VII.4, §X.4). ⚠️ On FYT,
  `skipkillapp.prop` only initialises from a full `AllAppUpdate.bin` **flashed to the unit** — this
  touches firmware and is **Class D (irreversible)**: it is produced as a documented procedure with
  backup + tested-unbrick prerequisites and executed by a human, never by the loop (§IX).

## 4. Engineering / process backlog (not feature work)

- [ ] **CI for the shell.** Add a GitHub Actions workflow that runs `./gradlew assembleDebug` +
  `testDebugUnitTest` (and, with an emulator, `connectedDebugAndroidTest`) so the Android module is
  covered in CI, not just on the local dev box. Document the JDK 17 + Android SDK + Windows-ROOT
  truststore setup for the runner.
- [ ] **Decide build-coupling.** The publish→stage→assemble flow is a documented two-step by design
  (FR-014); revisit whether to add an opt-in Gradle task or a convenience wrapper if the manual step
  proves error-prone (UI changes require re-running `build/publish-wasm-to-assets.ps1`).
- [ ] **Branch cleanup.** Delete the merged `005-launcher-shell` branch once no longer needed for
  hardware fix-ups.
- [ ] **Expand `installation.md` troubleshooting/debug** sections as real-unit findings come in
  (ADB-enable steps per variant, common WebView versions, MIME/asset gotchas).

## 5. Pointers

- Spec / plan / tasks / on-hardware checklist: [`specs/005-launcher-shell/`](../../specs/005-launcher-shell/)
- Install guide: [`installation.md`](./installation.md)
- Build/toolchain notes: [`app/README.md`](../../app/README.md)
- Governance: [`.specify/memory/constitution.md`](../../.specify/memory/constitution.md) (Part II),
  loop playbook: `.specify/memory/loop-playbook.md`
- Core IoT roadmap (separate track): [`PROJECT_PLAN.md`](../../PROJECT_PLAN.md)

---

_Update this file as each item lands or as on-hardware findings change priorities._
