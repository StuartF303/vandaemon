package com.vandaemon.shell.bridge

import android.webkit.JavascriptInterface

/**
 * Native (Kotlin) implementation of the 004 `INativeBridge` contract, exposed to the
 * WASM UI over JS-interop as `window.VanDaemonNativeBridge`.
 *
 * First pass returns the same safe defaults as the C# `StubNativeBridge` (FR-005): the
 * point is to prove the round-trip through a real WebView, not to read real signals.
 * Every real implementation is **needs-reverse-engineering** (Constitution §XI.3).
 *
 * Thin by design: no hardware/native/filesystem access beyond a no-op `openDsp`
 * (Constitution §XI.2, §XII.4). Exposes EXACTLY the three callable members and nothing
 * else (contract guarantee G4); the `wheelKeyPressed` event is delivered separately via
 * [WheelKeyPush].
 */
class NativeBridge {

    /** Stub: `false`. Real impl reads the reverse-cam trigger — needs-reverse-engineering. */
    @JavascriptInterface
    fun getReversingState(): Boolean = false

    /** Stub: `"Unknown"` (wire form of [AccState.Unknown]). Real impl reads ACC — needs-reverse-engineering. */
    @JavascriptInterface
    fun getAccState(): String = AccState.Unknown.name

    /** Stub: no-op. Real impl opens the Teyes DSP/EQ activity — needs-reverse-engineering. */
    @JavascriptInterface
    fun openDsp() {
        // Intentionally empty in the first pass.
    }
}
