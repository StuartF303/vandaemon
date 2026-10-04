---
description: "Task list for 005-launcher-shell implementation"
---

# Tasks: VanDaemon Launcher Shell (first pass — Tier-0 Kotlin shell hosting WASM UI)

**Input**: Design documents from `/specs/005-launcher-shell/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/js-interop-bridge.md, quickstart.md
**Constitution**: v2.1.0 (Part II §VI–XIII binding)
**Risk class**: Mixed **B + C**, **Human-Verified** (playbook §4)

**Tests**: INCLUDED. The spec defines its Class-B acceptance criteria *as* automated tests (asset-load, bridge round-trip, contract drift). Per Constitution §XII.3 these tests are the loop's exit condition; they are not optional here.

**Loop limits (Constitution §XIII.2–5)**: Phases 1–6 (Class-B) are the loop's exit — build + unit/instrumented tests green → tested, installable artifact. **No self-merge.** Phase 7 (Class-C) is **blocked-on-hardware**, **human-run by Stuart**, **never auto-verified**, and never marked done from off-vehicle results.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: US1 / US2 / US3 (maps to spec user stories)
- All paths are repo-relative. New native module lives under `app/`.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Stand up the new `app/` Android module and the documented build pipeline. (FR-001, FR-013, FR-014)

- [x] T001 Create the `app/` Android Gradle module skeleton (FR-013): `app/settings.gradle.kts`, `app/build.gradle.kts` (Android application plugin, AGP current stable, Kotlin, JDK 17 toolchain, `minSdk 29` provisional, `compileSdk`/`targetSdk` current), `app/gradle/`, `app/gradlew`, `app/gradlew.bat`, `app/gradle/wrapper/*`.
- [x] T002 [P] Add the AndroidX `webkit` (`WebViewAssetLoader`) + core dependencies and the test deps (JUnit4, AndroidX Test, Espresso/UiAutomator) to `app/build.gradle.kts`.
- [x] T003 [P] Create `app/src/main/AndroidManifest.xml` declaring an **ordinary LAUNCHER** activity (`MAIN` + `LAUNCHER` only) — explicitly **NO** `android.intent.category.HOME` or `DEFAULT` (FR-009, §X). Add `usesCleartextTraffic=false`; no vehicle/hardware permissions.
- [x] T004 [P] Add `.gitignore` entry for generated assets `app/src/main/assets/www/` (generated output, not source — FR-014) and Android build outputs (`app/build/`, `.gradle/`).
- [x] T005 Create the documented two-step build script `build/publish-wasm-to-assets.ps1` (FR-014): (1) `dotnet publish src/Frontend/VanDaemon.Web -c Release`, (2) copy the published `wwwroot` into `app/src/main/assets/www/` (clean-copy). Script must fail loudly per stage; it must **NOT** call Gradle (Gradle stays decoupled).

**Checkpoint**: `app/` module configures and `./gradlew tasks` runs; build script exists.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The bridge contract mirror + WebView host scaffolding that every user story builds on. (FR-002, FR-004, §XI)

**⚠️ CRITICAL**: No user-story work begins until this phase is complete.

- [x] T006 [P] Create `app/src/main/kotlin/com/vandaemon/shell/bridge/BridgeContract.kt` — canonical Kotlin mirror of the 004 surface (data-model.md): enums `AccState{Unknown,Off,On}`, `WheelKey{Unknown,VolumeUp,VolumeDown,Next,Previous,Voice,ModeSwitch}`, data class `WheelKeyEvent(key: WheelKey, timestampUtc: String /*ISO-8601 UTC*/)`. Names/order must match 004 exactly (FR-006). No behaviour — declarations only.
- [x] T007 Create `app/src/main/kotlin/com/vandaemon/shell/WebAppHost.kt` — configures `WebViewAssetLoader` mapping `/assets/` to `app/src/main/assets/www/` on the virtual same-origin origin (`https://appassets.androidplatform.net/...`), wires `shouldInterceptRequest`, enables JS + WASM, computes the start URL (`index.html`). Correct MIME for `.wasm`/framework files (FR-002, FR-003). No app logic.
- [x] T008 Create `app/src/main/kotlin/com/vandaemon/shell/MainActivity.kt` — hosts the WebView via `WebAppHost`, loads the start URL on create, manages lifecycle (FR-002). Thin: create → load → destroy only.

**Checkpoint**: App builds and launches to a WebView pointed at the virtual origin (UI assets staged by the build script).

---

## Phase 3: User Story 1 — Hosted WASM UI renders inside the shell (Priority: P1) 🎯 MVP

**Goal**: The bundled VanDaemon Blazor UI loads and renders in the WebView from app assets, fully offline. (Spec US1; FR-002/003; SC-002)

**Independent Test**: Stage the published `wwwroot`, build the APK, launch on a current-WebView emulator; an instrumented test asserts the start URL resolves and a known root element / document title appears — no network, no server.

### Tests for User Story 1 ⚠️ (write first, must fail before impl)

- [x] T009 [US1] Instrumented test `app/src/androidTest/kotlin/com/vandaemon/shell/AssetLoadTest.kt` (SC-002, FR-002/003): launch `MainActivity`, wait for page load, assert the document title / a known VanDaemon root DOM element is present, and that assets are served from the virtual app-assets origin. Runs with no network.

### Implementation for User Story 1

- [x] T010 [US1] Run `build/publish-wasm-to-assets.ps1` to stage the real published `wwwroot` into `app/src/main/assets/www/`; confirm `index.html` + `_framework/*.wasm` present (FR-002). (Generated content; not committed.)
- [x] T011 [US1] Finalize `WebAppHost`/`MainActivity` asset-loader + WebView settings so `AssetLoadTest` passes (correct MIME, same-origin, JS/WASM enabled) (FR-003). Iterate until T009 is green.

**Checkpoint**: SC-001 (APK builds) + SC-002 (renders on emulator) hold. MVP demonstrable off-vehicle.

---

## Phase 4: User Story 2 — UI ↔ native bridge round-trips through the WebView (Priority: P1)

**Goal**: Each `INativeBridge` member called from the UI returns its stub value across the real WebView, and the shell can push a wheel-key event into the page. Surface matches 004 exactly. (Spec US2; FR-004/005/006/007/012; SC-003/SC-004)

**Independent Test**: Unit tests assert the stub values and contract parity; an instrumented test drives a UI→native call across the WebView and asserts the stub return + a shell-pushed event arriving at the page.

### Tests for User Story 2 ⚠️ (write first, must fail before impl)

- [x] T012 [P] [US2] Unit test `app/src/test/kotlin/com/vandaemon/shell/bridge/NativeBridgeStubTest.kt` (FR-005, SC-003 logic): `getReversingState`→`false`, `getAccState`→`"Unknown"`, `openDsp`→no-op/completes, and the wheel-key push serializes the expected JSON payload.
- [x] T013 [P] [US2] Unit test `app/src/test/kotlin/com/vandaemon/shell/bridge/BridgeContractDriftTest.kt` (FR-006, SC-004): assert the Kotlin surface (members + `AccState`/`WheelKey` names + `WheelKeyEvent` shape) matches the authoritative 004 list (from `specs/004-plugin-architecture/contracts/INativeBridge.md`). Fails on any add/remove/rename. Must also assert **no extra** member exists (FR-007/§XI.4, contract G4).
- [x] T014 [US2] Instrumented test `app/src/androidTest/kotlin/com/vandaemon/shell/BridgeRoundTripTest.kt` (FR-004/012, SC-003): from the loaded page, call `window.VanDaemonNativeBridge.getReversingState()` etc. and assert stub values cross the JS-interop boundary; push `onWheelKey` from the shell and assert `window.VanDaemonBridgeEvents.onWheelKey` receives the payload (per contracts/js-interop-bridge.md).

### Implementation for User Story 2

- [x] T015 [US2] Create `app/src/main/kotlin/com/vandaemon/shell/bridge/NativeBridge.kt` — `@JavascriptInterface` object exposing exactly `getReversingState(): Boolean`, `getAccState(): String`, `openDsp()` returning the `StubNativeBridge`-equivalent defaults (FR-004/005). Uses `BridgeContract` types. No hardware/native access beyond a no-op `openDsp` (§XI.2). Make T012/T013 pass.
- [x] T016 [US2] Create `app/src/main/kotlin/com/vandaemon/shell/bridge/WheelKeyPush.kt` — shell→UI event push via `WebView.evaluateJavascript("window.VanDaemonBridgeEvents.onWheelKey(<json>)")` with `WheelKeyEvent` serialized per contract; raised only when explicitly invoked (FR-005). 
- [x] T017 [US2] Inject the bridge in `MainActivity`/`WebAppHost` (`addJavascriptInterface(NativeBridge, "VanDaemonNativeBridge")`) after page-ready; ensure calls before readiness fail safe (contract G5). Iterate until T014 is green.

**Checkpoint**: SC-003 (round-trip + event push) + SC-004 (contract parity) hold. The full seam is proven on a current WebView.

---

## Phase 5: User Story 3 — The shell stays thin and coexistence-ready (Priority: P2)

**Goal**: The Kotlin layer is host/transport/lifecycle only, within the LOC budget, and the app is an ordinary sideloaded app (no HOME). (Spec US3; FR-008/009; SC-005)

**Independent Test**: A LOC measurement + manifest inspection confirm the thin-shell budget and the absence of any HOME/DEFAULT category.

- [x] T018 [P] [US3] Add a thin-shell guard: a script/test `app/scripts/check-thin-shell.ps1` (or a Gradle verification task) that counts Kotlin LOC under `app/src/main/kotlin` and fails if > ~500, and greps `app/src/main/AndroidManifest.xml` to assert **no** `android.intent.category.(HOME|DEFAULT)` (FR-008/009, SC-005). Document the budget rationale (§XII.4).
- [x] T019 [US3] Review/refactor the Kotlin sources to confirm **no application/business logic** and **no direct hardware/filesystem access for the UI** (§XI.2, §XII.4); move any stray logic out (there should be none). Confirm T018 passes.

**Checkpoint**: SC-005 holds. All Class-B criteria (SC-001–SC-005) met.

---

## Phase 6: Polish & Class-B Exit Gate

**Purpose**: Documentation + the off-vehicle exit gate. (Constitution §XIII.2)

- [x] T020 [P] Write `app/README.md`: documents the two-step build, the thin-shell intent, the no-HOME/coexistence stance, and that on-device verification is human-run (links spec §On-Hardware Checklist).
- [x] T021 Run the full off-vehicle gate per quickstart.md: `build/publish-wasm-to-assets.ps1` → `./gradlew assembleDebug` → `./gradlew testDebugUnitTest` → `./gradlew connectedDebugAndroidTest` → thin-shell check (T018). All green = **Class-B exit**: a tested, installable `app-debug.apk`.
- [x] T022 Assemble the **deliverable bundle**: the `app-debug.apk` path + the spec's On-Hardware Verification Checklist, ready to hand to Stuart. **Do NOT self-merge; do NOT claim on-device success** (§XIII.3–5).

**Checkpoint**: Loop exit reached. STOP here for off-vehicle work.

---

## Phase 7: Class-C On-Hardware Verification — BLOCKED-ON-HARDWARE / HUMAN-RUN (Stuart)

**Purpose**: Resolve SC-006/SC-007 on the FYT unit. **These tasks are NOT executed or marked done by the implementation loop** (Constitution §IX.4, §XIII.3–5). They are the deliverable checklist for Stuart on the vehicle. Each stays `blocked-on-hardware` until Stuart records a result.

- [ ] T023 [US1] *(blocked-on-hardware, human-run)* Record exact model + SoC + Android + build fingerprint (`adb shell getprop | grep -iE "fyt|ums512|7862|fingerprint|product"`) and stock WebView version (`adb shell dumpsys package com.google.android.webview | grep versionName`). Confirm/adjust the provisional `minSdk 29` against the real Android (FR-013).
- [ ] T024 [US1] *(blocked-on-hardware, human-run)* `adb install app-debug.apk`; confirm it launches as an **ordinary app** (NOT home screen) (FR-009).
- [ ] T025 [US1] *(blocked-on-hardware, human-run)* Confirm the unit's WebView **renders** the Blazor WASM UI (SC-006, resolves §VII.5). Record pass/fail; if fail, capture WebView version + console errors.
- [ ] T026 [US2] *(blocked-on-hardware, human-run)* Confirm a bridge call from the running UI **round-trips through the unit's real WebView** (stub value returns) (SC-007).
- [ ] T027 *(blocked-on-hardware, human-run)* If WASM does NOT render: capture failure detail to drive the deferred AOT/brotli decision (FR-015). Do **not** proceed to the HOME milestone until SC-006 passes.
- [ ] T028 *(blocked-on-hardware, human-run)* Confirm nothing about the stock Teyes launcher or vehicle settings is touched by install/run (§VII.3).

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no deps — start immediately.
- **Foundational (Phase 2)**: depends on Setup — BLOCKS all user stories.
- **US1 (Phase 3)** and **US2 (Phase 4)** (both P1): depend on Foundational; US2's instrumented round-trip (T014) needs the page loading from US1's host (T011), so run US1 before/with US2.
- **US3 (Phase 5, P2)**: depends on the Kotlin sources existing (Phases 2–4) to measure.
- **Polish/Exit (Phase 6)**: depends on US1–US3.
- **Class-C (Phase 7)**: depends on the Phase 6 APK artifact; **human-gated**, runs off-loop.

### Within stories

- Tests (T009, T012, T013, T014) are written first and fail before their implementation tasks.
- `BridgeContract` (T006) before `NativeBridge`/`WheelKeyPush` (T015/T016).
- `WebAppHost` (T007) before `MainActivity` wiring (T008/T011/T017).

### Parallel Opportunities

- T002, T003, T004 [P] (Setup, different files).
- T006 [P] (foundational contract mirror, independent file).
- T012, T013 [P] (US2 unit tests, different files).
- T018 [P], T020 [P] (independent files).

---

## Parallel Example: Phase 1 Setup

```bash
# After T001 (module skeleton), these touch different files:
Task: "Add webkit + test deps to app/build.gradle.kts"          # T002
Task: "Create ordinary-LAUNCHER AndroidManifest.xml (no HOME)"  # T003
Task: "Add .gitignore for generated assets + build outputs"     # T004
```

---

## Implementation Strategy

### MVP First (User Story 1)

1. Phase 1 Setup → 2. Phase 2 Foundational → 3. Phase 3 US1 → **STOP & VALIDATE**: UI renders on emulator (SC-002). Demonstrable MVP.

### Incremental Delivery

1. Setup + Foundational → host ready.
2. US1 → UI renders offline (SC-001/002).
3. US2 → bridge round-trip + contract parity (SC-003/004).
4. US3 → thin-shell + ordinary-app guards (SC-005).
5. Phase 6 → Class-B exit gate: tested installable APK + checklist.
6. Phase 7 → hand to Stuart for on-vehicle verification (SC-006/007).

### Loop discipline (Constitution §XIII)

- The loop runs Phases 1–6 only. Green `gradlew` build + tests = the objective Class-B exit; **no self-merge**.
- Phase 7 is produced as a checklist, **never executed or marked done by the loop**. Blocked-on-hardware ≠ done.
- Do not add tasks that change the 004 `INativeBridge` contract, declare HOME/DEFAULT, touch the stock launcher, wire real vehicle signals, or build the AOT/brotli pipeline — all out of scope/deferred.

---

## Notes

- [P] = different files, no dependencies. [Story] maps a task to a spec user story for traceability.
- Verify each test fails before implementing it.
- Commit after each task or logical group (conventional commits, §Development Workflow).
- Class-C tasks (T023–T028) carry `(blocked-on-hardware, human-run)` and must never be checked off from off-vehicle results.
