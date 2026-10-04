package com.vandaemon.shell.bridge

import android.webkit.JavascriptInterface
import org.junit.Assert.assertEquals
import org.junit.Test

/**
 * FR-006 / SC-004: the Kotlin bridge surface MUST match the 004 `INativeBridge` contract
 * exactly. The `expected*` lists below are the authoritative surface transcribed from
 * specs/004-plugin-architecture/contracts/INativeBridge.md. Any add / remove / rename —
 * on either side — fails this test, and therefore the build (Constitution §XI.4).
 */
class BridgeContractDriftTest {

    private val expectedMembers =
        listOf("getReversingState", "getAccState", "openDsp", "wheelKeyPressed")
    private val expectedAccStates =
        listOf("Unknown", "Off", "On")
    private val expectedWheelKeys =
        listOf("Unknown", "VolumeUp", "VolumeDown", "Next", "Previous", "Voice", "ModeSwitch")

    @Test
    fun contractConstants_matchExpectedSurface() {
        assertEquals(expectedMembers, BridgeContract.members)
        assertEquals(expectedAccStates, BridgeContract.accStates)
        assertEquals(expectedWheelKeys, BridgeContract.wheelKeys)
    }

    @Test
    fun accStateEnum_matchesContractInOrder() {
        assertEquals(expectedAccStates, AccState.entries.map { it.name })
    }

    @Test
    fun wheelKeyEnum_matchesContractInOrder() {
        assertEquals(expectedWheelKeys, WheelKey.entries.map { it.name })
    }

    /**
     * FR-007 / contract guarantee G4: the injected object exposes EXACTLY the three
     * callable bridge members and no extra capability. (`wheelKeyPressed` is the
     * native→UI event, delivered via WheelKeyPush, not a @JavascriptInterface method.)
     */
    @Test
    fun nativeBridge_exposesExactlyTheThreeCallableMembers_andNoExtras() {
        val callable = NativeBridge::class.java.declaredMethods
            .filter { it.isAnnotationPresent(JavascriptInterface::class.java) }
            .map { it.name }
            .sorted()
        assertEquals(listOf("getAccState", "getReversingState", "openDsp"), callable)
    }
}
