package com.vandaemon.shell.bridge

/**
 * Canonical Kotlin mirror of the 004 `INativeBridge` surface
 * (specs/004-plugin-architecture/contracts/INativeBridge.md).
 *
 * Names and declaration order MUST match the C# contract exactly — this is a
 * contract, not an implementation detail (Constitution §XI.4). The drift test
 * [com.vandaemon.shell.bridge.BridgeContractDriftTest] fails the build on any
 * divergence. Declarations only; no behaviour lives here.
 */

/** Mirrors C# `enum AccState { Unknown = 0, Off, On }`. */
enum class AccState { Unknown, Off, On }

/** Mirrors C# `enum WheelKey { Unknown = 0, VolumeUp, VolumeDown, Next, Previous, Voice, ModeSwitch }`. */
enum class WheelKey { Unknown, VolumeUp, VolumeDown, Next, Previous, Voice, ModeSwitch }

/** Mirrors C# `record WheelKeyEvent(WheelKey Key, DateTimeOffset TimestampUtc)`. */
data class WheelKeyEvent(val key: WheelKey, val timestampUtc: String)

/**
 * The authoritative surface of `INativeBridge`, expressed as data so the drift
 * test can assert parity without reflecting into Android-only types.
 *
 * `members` are the four contract members in declaration order:
 * three UI→native calls plus the native→UI `wheelKeyPressed` event.
 */
object BridgeContract {
    val members: List<String> = listOf(
        "getReversingState",
        "getAccState",
        "openDsp",
        "wheelKeyPressed",
    )

    val accStates: List<String> = AccState.entries.map { it.name }
    val wheelKeys: List<String> = WheelKey.entries.map { it.name }
}
