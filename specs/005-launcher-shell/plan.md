# Implementation Plan: VanDaemon Launcher Shell (first pass — Tier-0 Kotlin shell hosting WASM UI)

**Branch**: `005-launcher-shell` | **Date**: 2026-06-22 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `specs/005-launcher-shell/spec.md`
**Constitution**: v2.1.0 (Part II head-unit principles VI–XIII binding)
**Risk class**: Mixed **B + C**, **Human-Verified** (loop playbook §4) — no self-merge; Class-C items are blocked-on-hardware.

## Summary

Build a thin, sideloadable Android app in a new top-level `app/` directory that hosts the published VanDaemon Blazor WASM UI in a WebView (served from APK assets via `WebViewAssetLoader` on a virtual same-origin app-assets origin) and implements the 004 `INativeBridge` contract over JS-interop with `StubNativeBridge`-equivalent stub defaults. The shell is host/transport/lifecycle only — no application logic, no hardware access, not the home screen. Off-vehicle, an instrumented test suite proves asset-load, the bridge round-trip, and contract-surface parity (Class-B exit). On-vehicle rendering and round-trip are delivered as the brief §8 checklist for Stuart (Class-C, blocked-on-hardware).

## Technical Context

**Language/Version**: Kotlin (current stable, JDK 17 toolchain) for the shell; the hosted UI is the existing .NET 10 Blazor WASM (`src/Frontend/VanDaemon.Web`, unchanged by this feature).
**Primary Dependencies**: AndroidX `webkit` (`WebViewAssetLoader`), Android WebView, AndroidX core; tests: JUnit4 + AndroidX Test + Espresso/`UiAutomator` for instrumented WebView tests; bridge-drift test reads the 004 contract source. Android Gradle Plugin **current stable**, Gradle wrapper pinned.
**Storage**: None persisted by the shell. UI assets are read-only APK assets (the published `wwwroot`). Offline-first; no server process on the unit (Constitution §III).
**Testing**: Gradle unit tests (JVM) for pure logic (bridge serialization/echo, contract-drift reflection check); **instrumented tests** (`androidTest`, emulator/device) for WebView asset-load, JS-interop round-trip, and event push. No `dotnet test` proves on-device behaviour (Constitution §XIII.2).
**Target Platform**: Android on Teyes FYT / Unisoc UMS512 (ums9230-class) head unit (Constitution §VII.1). `minSdk 29` provisional (confirmed vs §8 fingerprint); off-vehicle tests run on a current-WebView emulator.
**Project Type**: Mobile/Android app (new `app/` module) hosting a WASM web payload built elsewhere — a third top-level source area alongside `src/` (.NET) and `hw/` (firmware).
**Performance Goals**: First-pass functional only — UI loads and renders; bridge calls return promptly. No latency targets beyond "loads offline, round-trips without error". (Real-time §II targets belong to the API, not this shell.)
**Constraints**: Thin Kotlin (≤ ~500 LOC, host/transport/lifecycle only — Constitution §XII.4); offline; no `HOME`/`DEFAULT` category; never touch the stock Teyes launcher (§VII.3); UI gets no native/filesystem/hardware access except via the bridge (§XI.2); bridge surface pinned to 004 — no contract change (§XI.4).
**Scale/Scope**: One Android module, one Activity + WebView host, one bridge implementation (4 members + 1 pushed event), one asset-loader wiring, ~4 test classes, one documented build script. No multi-screen, no navigation, no persistence.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Gate | Status |
|---|---|---|
| **VI. Scope & Intent** | First pass customises the existing install; no custom ROM; modular for later integration. | ✅ PASS — sideloaded app only; HOME/launcher-takeover explicitly deferred. |
| **VI.3 Two-tier plugin model** | No hardware access inside WebView-hosted WASM. | ✅ PASS — UI reaches native only via the bridge; bridge returns stubs, touches no hardware. |
| **VII.1 Platform truth (Unisoc/FYT)** | Don't re-derive Allwinner assumptions. | ✅ PASS — plan targets FYT/UMS512; minSdk provisional pending fingerprint. |
| **VII.3 Never remove stock launcher** | Coexist, don't replace. | ✅ PASS — ordinary app, no HOME category, stock app untouched (verified in §8 checklist). |
| **VII.5 Old WebView risk (ANECDOTAL)** | Treat WebView capability as unproven until tested on-device. | ✅ PASS — SC-006 is the explicit on-hardware test; AOT escalation deferred (FR-015). |
| **IX. Safety & Reversibility** | No autonomous irreversible actions. | ✅ PASS — first pass is a reversible sideload; no root/flash. Class D not entered. |
| **X. Launcher & Coexistence** | HOME/forwarding/autostart deferred. | ✅ PASS — all out of scope this feature. |
| **XI. Native↔WASM Boundary** | Bridge is the only seam; surface is a contract; members tagged confirmed/needs-RE. | ✅ PASS — implements 004 `INativeBridge` unchanged; all real signals tagged needs-reverse-engineering (FR-010); drift test enforces parity (FR-006). |
| **XII.4 Thin Kotlin** | App logic stays in C#/Blazor; ~few hundred lines. | ✅ PASS — ≤ ~500 LOC budget; host/transport/lifecycle only. |
| **XIII.2–5 Loop limits / no green-washing** | Build+unit-test only; on-device is a checklist, not a success claim; blocked-on-hardware ≠ done. | ✅ PASS — Class-B automated exit; Class-C = §8 checklist; no self-merge. |

**Result**: No violations. Complexity Tracking not required.

## Project Structure

### Documentation (this feature)

```text
specs/005-launcher-shell/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output (bridge surface + JS-interop wire shape)
├── quickstart.md        # Phase 1 output (build + test + on-hardware run guide)
├── contracts/
│   └── js-interop-bridge.md   # JS-interop wire contract mapping INativeBridge ↔ injected JS object
├── checklists/
│   └── requirements.md  # spec quality checklist (from /specify, updated by /clarify)
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
app/                                  # NEW top-level Android module (FR-013)
├── build.gradle.kts                  # Android app module, AGP current stable, minSdk 29 (provisional)
├── src/
│   ├── main/
│   │   ├── AndroidManifest.xml       # ordinary LAUNCHER app — NO HOME/DEFAULT category (FR-009)
│   │   ├── kotlin/com/vandaemon/shell/
│   │   │   ├── MainActivity.kt       # WebView host + lifecycle
│   │   │   ├── WebAppHost.kt         # WebViewAssetLoader wiring, virtual app-assets origin (FR-002/003)
│   │   │   └── bridge/
│   │   │       ├── NativeBridge.kt        # @JavascriptInterface impl of INativeBridge, stub defaults (FR-004/005)
│   │   │       ├── BridgeContract.kt      # canonical member/enum/record names mirrored from 004
│   │   │       └── WheelKeyPush.kt        # shell→UI event push via evaluateJavascript
│   │   └── assets/www/               # populated by the build script (published VanDaemon.Web wwwroot) — git-ignored
│   ├── test/kotlin/                  # JVM unit tests
│   │   ├── NativeBridgeStubTest.kt        # each member returns stub default (FR-005)
│   │   └── BridgeContractDriftTest.kt     # surface parity vs 004 contract (FR-006)
│   └── androidTest/kotlin/           # instrumented (emulator) tests
│       ├── AssetLoadTest.kt              # start URL resolves, known root element/title appears (FR-002, SC-002)
│       └── BridgeRoundTripTest.kt        # UI→native stub round-trip + shell→UI event push (FR-012, SC-003)
├── gradle/ , gradlew , gradlew.bat , settings.gradle.kts
└── README.md                         # documents the two-step build + thin-shell intent

build/
└── publish-wasm-to-assets.ps1        # documented two-step glue: dotnet publish → copy wwwroot → (gradle assemble) (FR-014)
                                       #   (sibling .sh optional; PowerShell primary per repo script backend)

# UNCHANGED / referenced only:
src/Frontend/VanDaemon.Web/           # the hosted WASM UI — published, not modified
src/Backend/VanDaemon.Plugins/VanDaemon.Plugins.Ui.Abstractions/INativeBridge.cs   # the pinned 004 contract
specs/004-plugin-architecture/contracts/INativeBridge.md                            # contract of record for the drift test
```

**Structure Decision**: A new top-level **`app/`** Android Gradle module (FR-013), sibling to `src/` (.NET) and `hw/` (firmware) — mirroring the repo's existing "native subproject lives at root" pattern. The shell is intentionally a single module with one Activity, one asset-host, and one bridge package. The published WASM lands in `app/src/main/assets/www/` via a documented build script (`build/publish-wasm-to-assets.ps1`, FR-014); that asset directory is git-ignored (it is generated output, not source). Tests split JVM-unit (`test/`, for pure bridge logic + contract drift) from instrumented (`androidTest/`, for real WebView behaviour) — matching the Class-B/Class-C boundary: instrumented tests on a current-WebView emulator stand in for, but do not replace, the on-unit §8 checks.

## Complexity Tracking

> No Constitution Check violations — section intentionally empty.
