# Quickstart: VanDaemon Launcher Shell (first pass)

A run/validation guide for the shell. It proves the Class-B acceptance criteria off-vehicle and hands off the Class-C criteria to the on-hardware checklist. Implementation detail lives in `tasks.md`; this is the "how do I build, test, and verify it" guide.

## Prerequisites

- .NET 10 SDK (to publish the Blazor WASM UI)
- JDK 17 + Android SDK; an emulator (or device) with a **current WebView** for instrumented tests
- Android Gradle Plugin / Gradle wrapper are pinned in `app/`
- Repo checked out on branch `005-launcher-shell`

## Build (documented two-step — FR-014)

```powershell
# 1+2. Publish the WASM UI and stage it into the shell's assets
./build/publish-wasm-to-assets.ps1
#   → dotnet publish src/Frontend/VanDaemon.Web (Release)
#   → copy published wwwroot  →  app/src/main/assets/www/

# 3. Assemble the debug APK (Gradle does NOT call dotnet)
cd app
./gradlew assembleDebug
#   → app/build/outputs/apk/debug/app-debug.apk
```

**Expected**: an installable `app-debug.apk` (SC-001) with the published WASM present under `assets/www/`.

## Class-B validation (off-vehicle — automated)

```powershell
cd app

# JVM unit tests: stub values + contract drift
./gradlew testDebugUnitTest
#   NativeBridgeStubTest      → each member returns its stub default (SC-003 logic)
#   BridgeContractDriftTest   → surface matches 004 INativeBridge exactly (SC-004)

# Instrumented tests: real WebView on emulator/device
./gradlew connectedDebugAndroidTest
#   AssetLoadTest             → start URL resolves from the virtual app-assets origin,
#                               known root element / document title appears (SC-002)
#   BridgeRoundTripTest       → UI→native call returns stub value across the WebView,
#                               shell-pushed wheel-key event reaches the page (SC-003)
```

**Expected**: all four test classes green. This is the **Class-B exit condition**. Green here proves the seam on a *current* WebView — it does **not** prove the unit's WebView (see Class-C).

### Mapping tests → acceptance criteria

| Criterion | Proven by | Class |
|---|---|---|
| SC-001 build → installable APK | `assembleDebug` produces `app-debug.apk` | B |
| SC-002 WASM loads via asset loader | `AssetLoadTest` (instrumented) | B |
| SC-003 bridge round-trip + event push | `NativeBridgeStubTest` (logic) + `BridgeRoundTripTest` (WebView) | B |
| SC-004 contract surface parity | `BridgeContractDriftTest` | B |
| SC-005 thin Kotlin / no HOME category | LOC check + manifest review (see below) | B |
| SC-006 renders on the unit | **on-hardware checklist** | C (blocked-on-hardware) |
| SC-007 round-trips on the unit | **on-hardware checklist** | C (blocked-on-hardware) |

### SC-005 thin-shell + ordinary-app check

```powershell
# Rough Kotlin line-count budget (≤ ~500 LOC, host/transport/lifecycle only)
(Get-ChildItem app/src/main/kotlin -Recurse -Filter *.kt | Get-Content | Measure-Object -Line).Lines
# Confirm NO HOME/DEFAULT intent category in the manifest
Select-String -Path app/src/main/AndroidManifest.xml -Pattern 'android.intent.category.(HOME|DEFAULT)'
#   → expect NO match (FR-009)
```

## Class-C verification (ON THE UNIT — Stuart runs; blocked-on-hardware, NOT auto-verified)

These steps are the brief §8 / spec checklist. They are the **deliverable**, not an automated pass. Do not mark SC-006/SC-007 done from off-vehicle results (Constitution §XIII.5).

```bash
# Identify the unit (record exact values)
adb shell getprop | grep -iE "fyt|ums512|7862|fingerprint|product"
adb shell dumpsys package com.google.android.webview | grep versionName

# Install as an ordinary app (NOT home screen)
adb install app/build/outputs/apk/debug/app-debug.apk
```

Then walk the checklist in `spec.md` → *On-Hardware Verification Checklist*:
- [ ] Record model/SoC/Android/fingerprint + stock WebView version.
- [ ] APK sideloads and launches as an ordinary app (not home).
- [ ] WebView **renders** the Blazor WASM UI on the unit (resolves the §VII.5 risk) — pass/fail + WebView version + console errors if fail.
- [ ] A bridge call from the running UI round-trips through the unit's real WebView (stub value returns).
- [ ] If WASM does NOT render: capture detail to drive the AOT/brotli decision (FR-015). Do not proceed to the HOME milestone until this passes.
- [ ] Confirm the stock Teyes launcher / vehicle settings are untouched by install/run.

## Done / not-done

- **Done (Class-B)** when `assembleDebug` produces the APK and all four test classes are green, and the SC-005 checks pass.
- **Blocked-on-hardware (Class-C)** until Stuart completes the on-unit checklist. The loop reports the tested APK + this checklist; it makes **no** on-device success claim and does **not** self-merge.
