package com.vandaemon.shell.bridge

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Test

/**
 * FR-005 / SC-003 (logic): the native bridge returns the StubNativeBridge-equivalent
 * defaults, and the wheel-key event serialises to the agreed wire shape. Pure JVM test —
 * no Android runtime, no WebView, no vehicle.
 */
class NativeBridgeStubTest {

    private val bridge = NativeBridge()

    @Test
    fun getReversingState_returnsFalse() {
        assertFalse(bridge.getReversingState())
    }

    @Test
    fun getAccState_returnsUnknown() {
        assertEquals("Unknown", bridge.getAccState())
    }

    @Test
    fun openDsp_isNoOp_andDoesNotThrow() {
        bridge.openDsp()
    }

    @Test
    fun wheelKeyEvent_serialisesToContractWireShape() {
        val json = WheelKeyPush.toJson(
            WheelKeyEvent(WheelKey.VolumeUp, "2026-06-22T12:00:00Z"),
        )
        assertEquals(
            """{"key":"VolumeUp","timestampUtc":"2026-06-22T12:00:00Z"}""",
            json,
        )
    }
}
