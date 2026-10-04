# VanDaemon Launcher Shell (`app/`)

First-pass **Tier-0 Kotlin launcher shell**: a thin, sideloadable Android app that hosts the
published VanDaemon Blazor/WASM UI in a WebView and implements the 004 `INativeBridge` contract
over JS-interop with stub values. Spec: [`specs/005-launcher-shell/`](../specs/005-launcher-shell/).

> **This is NOT the launcher yet.** It declares no `HOME`/`DEFAULT` category, never touches the
> stock Teyes launcher, and reads no real vehicle signals — those are deferred milestones
> (Constitution §X, brief §7). It is an ordinary sideloaded app that proves the
> WebView + WASM + bridge seam.

## What it does

- Serves the bundled Blazor `wwwroot` from APK assets via `WebViewAssetLoader` on a virtual
  same-origin origin (`https://appassets.androidplatform.net/…`) — fully offline, no server.
- Injects `window.VanDaemonNativeBridge` implementing `INativeBridge` with `StubNativeBridge`
  defaults (`getReversingState`→false, `getAccState`→`Unknown`, `openDsp`→no-op), and pushes
  `wheelKeyPressed` events into the page. The bridge surface is pinned to 004 — a drift test fails
  the build on any change (Constitution §XI.4).
- Stays thin (host / asset-loader / bridge / lifecycle only; no application logic) — guarded by
  `scripts/check-thin-shell.ps1` (Constitution §XII.4).

## Build (documented two-step — FR-014)

```powershell
# 1+2. Publish the WASM UI and stage it into app/src/main/assets/www/
./build/publish-wasm-to-assets.ps1            # run from repo root

# 3. Assemble the debug APK (Gradle does NOT invoke dotnet)
cd app
./gradlew assembleDebug                        # -> app/build/outputs/apk/debug/app-debug.apk
```

Prerequisites: .NET 10 SDK, JDK 17, Android SDK (`ANDROID_HOME`). On this corp dev box the JVM
must trust the Windows root store — set in `gradle.properties` via `org.gradle.jvmargs`
(`-Djavax.net.ssl.trustStoreType=Windows-ROOT`).

## Test (off-vehicle — Class B)

```powershell
cd app
./gradlew testDebugUnitTest          # NativeBridgeStubTest, BridgeContractDriftTest, AssetMimeTest
./gradlew connectedDebugAndroidTest  # AssetLoadTest, BridgeRoundTripTest (needs an emulator/device)
pwsh scripts/check-thin-shell.ps1    # LOC budget + no-HOME-category guard
```

Green here proves the seam on a **current** WebView. It does **not** prove the FYT unit's
(older) WebView — that is human-run.

## Installing on the head unit

See the **[Head-Unit Installation Guide](../docs/head-unit/installation.md)** — the basic user
guide for building, sideloading (`adb install` or file manager), launching, and verifying the app
on a Teyes FYT unit. No root required.

## On-hardware verification (Class C — human-run, blocked-on-hardware)

The unit's WebView rendering (SC-006) and the on-unit bridge round-trip (SC-007) can only be
proven on the FYT head unit. See the **On-Hardware Verification Checklist** in
[`specs/005-launcher-shell/spec.md`](../specs/005-launcher-shell/spec.md). The implementation loop
makes **no** on-device success claim and does not self-merge (Constitution §XIII.2–5).
