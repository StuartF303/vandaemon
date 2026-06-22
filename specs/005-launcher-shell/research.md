# Phase 0 Research: VanDaemon Launcher Shell (first pass)

All NEEDS CLARIFICATION items from the spec were resolved in the `/clarify` session (2026-06-22). This file records the technical decisions and their rationale. No open unknowns remain that block planning; the one genuinely-unknown quantity (the unit's stock WebView version) is **intentionally deferred to on-hardware verification** (FR-015) and is not a blocker for off-vehicle work.

## D1 — Shell location & toolchain (resolves FR-013)

- **Decision**: New top-level `app/` Android Gradle module; Kotlin + Android Gradle Plugin **current stable**; JDK 17 toolchain; `minSdk 29` (Android 10) **provisional**; Gradle wrapper pinned in-repo.
- **Rationale**: `app/` sits beside `src/` (.NET) and `hw/` (firmware), matching the repo's "native subproject at root" convention. Current-stable AGP/Kotlin avoids carrying known-old toolchain bugs; JDK 17 is the current AGP baseline. `minSdk 29` is a reasonable provisional for FYT/UMS512 units (commonly Android 10) and is explicitly reconfirmed against the §8 `getprop` fingerprint before release.
- **Alternatives considered**: `shell/` name (rejected — `app/` is the conventional Android module name and the brief's first suggestion); pinning to an LTS toolchain with lower minSdk 26 (rejected for the first pass — no compatibility need yet; revisit if the fingerprint shows an older Android).

## D2 — WASM hosting & WebView serving (resolves FR-002/003)

- **Decision**: Bundle the published Blazor `wwwroot` as APK assets and serve it through AndroidX `WebViewAssetLoader` mapped to a **virtual same-origin origin** (`https://appassets.androidplatform.net/assets/...`). The WebView's start URL is that virtual origin's `index.html`.
- **Rationale**: Blazor WASM uses `fetch`/streaming compilation and same-origin rules that break under `file://`. `WebViewAssetLoader` serves assets over an `https://` virtual origin with correct MIME types, satisfying WASM MIME + same-origin without any server process — fully offline, robust on a head unit (brief §4). This is the AndroidX-blessed replacement for the deprecated `WebView.loadUrl("file://…")` asset pattern.
- **Alternatives considered**: Point the WebView at a local embedded HTTP server (rejected — extra process, port/lifecycle fragility on a head unit, brief explicitly chose assets over a server); `file://` + `setAllowFileAccessFromFileURLs` (rejected — deprecated, insecure, breaks WASM same-origin/MIME).
- **Open on-hardware risk (deferred, FR-015)**: whether the unit's *stock* WebView is new enough to run modern Blazor WASM (Constitution §VII.5, ANECDOTAL). Resolved only by SC-006 on the unit; mitigation (AOT/brotli) is out of scope here.

## D3 — Bridge transport: JS-interop (resolves FR-004)

- **Decision**: Inject a Kotlin object annotated `@JavascriptInterface` into the page for **UI→native** calls; push **native→UI** events (e.g. `WheelKeyPressed`) by calling `WebView.evaluateJavascript(...)` against a small JS dispatch hook the page exposes.
- **Rationale**: JS-interop is the standard, dependency-free WebView↔native channel and realises the transport 004 deliberately left abstract — 004 names no transport, so JS-interop slots in without any plugin-code change (Constitution §XI, spec FR-007). `@JavascriptInterface` methods run on a WebView-managed binder thread; results are returned to the WASM caller as a serialized value.
- **Async/return shaping**: `INativeBridge` members are `Task`-returning on the C# side. Across JS-interop, synchronous `@JavascriptInterface` returns are wrapped into resolved promises on the JS shim so the WASM side sees an awaitable. Stub values are constant, so no real async work occurs in the first pass.
- **Alternatives considered**: a local WebSocket/socket transport (rejected for first pass — heavier, another listener to manage; 004 keeps it possible later); `postMessage`/`MessageChannel` (viable but more plumbing than `@JavascriptInterface` for a stub round-trip).

## D4 — Bridge depth: stub/echo only (resolves FR-005/010)

- **Decision**: The native bridge returns exactly the `StubNativeBridge` defaults — `GetReversingState→false`, `GetAccState→Unknown`, `OpenDsp→no-op`, `WheelKeyPressed` wirable but only raised when explicitly pushed (e.g. by a test). Every member is documented **needs-reverse-engineering** for its real implementation.
- **Rationale**: The first pass proves the *round-trip through a real WebView*, not signal reading (brief §4). Stub parity with the C# `StubNativeBridge` keeps both ends of the seam behaviourally identical off-device and makes the round-trip test deterministic. Real signals are FYT-specific and must be reverse-engineered on hardware (Constitution §X.3, §XI.3).
- **Alternatives considered**: wiring any one real signal now (rejected — explicitly out of scope, needs on-unit recon, would smuggle Class-C/needs-RE work into a Class-B deliverable).

## D5 — Build wiring: documented two-step (resolves FR-014)

- **Decision**: A documented script `build/publish-wasm-to-assets.ps1` performs (1) `dotnet publish src/Frontend/VanDaemon.Web` (Release), (2) copy the published `wwwroot` into `app/src/main/assets/www/`, then the developer/CI runs (3) `gradlew assemble`. Gradle does **not** invoke the .NET toolchain.
- **Rationale**: Decoupling keeps the Android build runnable on any host with just a JDK + Android SDK; the .NET publish is a separate, independently-debuggable stage (brief §9 recommendation). Each stage fails loudly on its own. The generated `assets/www/` is git-ignored output.
- **Alternatives considered**: Gradle task that shells out to `dotnet publish` as an `assemble` dependency (rejected for first pass — couples every Android build/CI host to a working .NET 10 SDK; more brittle, harder to debug; can be added later if desired).
- **Repo fit**: the repo's Spec Kit script backend is PowerShell (`script: "ps"`), so PowerShell is the primary script; an optional `.sh` sibling can follow for Linux CI.

## D6 — Contract-drift enforcement (resolves FR-006)

- **Decision**: A JVM unit test (`BridgeContractDriftTest`) asserts the Kotlin bridge surface mirrors the 004 contract: member set (`getReversingState`, `getAccState`, `openDsp`, `wheelKeyPressed`), `AccState{Unknown,Off,On}`, `WheelKey{Unknown,VolumeUp,VolumeDown,Next,Previous,Voice,ModeSwitch}`, and the `WheelKeyEvent(key,timestampUtc)` shape. The canonical names live in `BridgeContract.kt`, compared against an authoritative list derived from `specs/004-plugin-architecture/contracts/INativeBridge.md` / `INativeBridge.cs`.
- **Rationale**: §XI.4 makes the surface a contract; a drift test fails the build on any divergence rather than letting a mismatch reach runtime. Because the C# side is the source of truth, the test encodes the expected surface as data and checks the Kotlin enums/members against it.
- **Alternatives considered**: generating Kotlin from the C# contract (rejected — over-engineered for a 4-member surface; a drift test is simpler and equally strict); manual review only (rejected — not enforceable, §XIII.5).

## D7 — Test split (Class-B vs Class-C boundary)

- **Decision**: JVM unit tests (`test/`) cover pure logic (stub values, contract drift). Instrumented tests (`androidTest/`, emulator with a current WebView) cover real WebView behaviour: asset-load/start-URL resolution and the JS-interop round-trip + event push. On-unit rendering/round-trip stay manual (§8 checklist).
- **Rationale**: This maps tests onto the risk classes. Instrumented emulator tests give real WebView coverage off-vehicle (Class-B exit) but cannot stand in for the *unit's old WebView* (Class-C) — so SC-006/007 remain human-gated (Constitution §XIII.2–3). No green-washing: emulator-green ≠ on-unit-green, and the report says so.
- **Alternatives considered**: Robolectric for WebView (rejected — Robolectric's WebView is a shadow, proves nothing about real asset-load/JS-interop); skipping instrumented tests (rejected — would leave the Class-B render/round-trip claims unproven off-vehicle).

## Resolved unknowns summary

| Item | Resolution | Residual |
|---|---|---|
| FR-013 dir + toolchain | `app/`, current-stable AGP/Kotlin, JDK 17, minSdk 29 provisional | minSdk reconfirm vs §8 fingerprint (hardware) |
| FR-014 build wiring | Documented two-step PS script, Gradle decoupled | none |
| FR-015 WebView→AOT threshold | Deferred; record observed version on unit first | resolved on hardware (SC-006) |
| Transport (004 left abstract) | JS-interop (`@JavascriptInterface` + `evaluateJavascript`) | none |
| On-unit WebView capability | Unprovable off-vehicle | §8 checklist (SC-006/007), blocked-on-hardware |
