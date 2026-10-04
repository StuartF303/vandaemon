package com.vandaemon.shell

import androidx.test.core.app.ActivityScenario
import androidx.test.ext.junit.runners.AndroidJUnit4
import com.vandaemon.shell.bridge.WheelKey
import com.vandaemon.shell.bridge.WheelKeyEvent
import com.vandaemon.shell.bridge.WheelKeyPush
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith

/**
 * SC-003 / FR-004: the INativeBridge round-trip works across a real WebView. Each member
 * called from the page reaches the injected Kotlin object and returns the stub value, and
 * a shell-pushed wheel-key event is delivered to the page. Stub values only — no vehicle.
 * (The unit's real WebView round-trip is the human-run SC-007.)
 */
@RunWith(AndroidJUnit4::class)
class BridgeRoundTripTest {

    @Test
    fun uiToNative_eachMemberReturnsItsStubValue() {
        ActivityScenario.launch(MainActivity::class.java).use { scenario ->
            WebViewProbe.waitUntilTrue(scenario, "document.readyState === 'complete'")
            assertTrue(
                "native bridge object was not injected",
                WebViewProbe.waitUntilTrue(scenario, "typeof window.VanDaemonNativeBridge !== 'undefined'"),
            )

            // getReversingState -> false (JS boolean)
            assertEquals(
                "false",
                WebViewProbe.eval(scenario, "window.VanDaemonNativeBridge.getReversingState()"),
            )
            // getAccState -> "Unknown" (JS string)
            assertEquals(
                "\"Unknown\"",
                WebViewProbe.eval(scenario, "window.VanDaemonNativeBridge.getAccState()"),
            )
            // openDsp -> no-op, must not throw
            assertEquals(
                "\"ok\"",
                WebViewProbe.eval(
                    scenario,
                    "(function () { window.VanDaemonNativeBridge.openDsp(); return 'ok'; })()",
                ),
            )
        }
    }

    /**
     * 008 / SC-004 support: the `window.vandaemonBridge` transport shim (the exact layer the C#
     * `JsInteropNativeBridge` drives) is present in the unit's WebView and forwards to the injected
     * native object. Proving the shim on-device is what makes 005 SC-007 observable; the C# selection
     * log then distinguishes native from stub. Requires the WASM assets to have been re-staged
     * (`build/publish-wasm-to-assets.ps1`) so `index.html` loads `js/vandaemon-bridge.js`.
     */
    @Test
    fun transportShim_isPresent_andForwardsToNative() {
        ActivityScenario.launch(MainActivity::class.java).use { scenario ->
            WebViewProbe.waitUntilTrue(scenario, "document.readyState === 'complete'")
            assertTrue(
                "vandaemon-bridge.js transport shim was not loaded",
                WebViewProbe.waitUntilTrue(scenario, "typeof window.vandaemonBridge !== 'undefined'"),
            )

            // hasNative() true -> the injected native object is reachable through the shim.
            assertEquals("true", WebViewProbe.eval(scenario, "window.vandaemonBridge.hasNative()"))
            // Forwarded stub values match the contract (js-interop-bridge.md).
            assertEquals("false", WebViewProbe.eval(scenario, "window.vandaemonBridge.getReversingState()"))
            assertEquals("\"Unknown\"", WebViewProbe.eval(scenario, "window.vandaemonBridge.getAccState()"))
        }
    }

    @Test
    fun nativeToUi_wheelKeyEvent_isDeliveredToThePage() {
        ActivityScenario.launch(MainActivity::class.java).use { scenario ->
            WebViewProbe.waitUntilTrue(scenario, "document.readyState === 'complete'")

            // Install the page-side event hook the shell pushes to.
            WebViewProbe.eval(
                scenario,
                """
                window.__lastWheel = null;
                window.VanDaemonBridgeEvents = {
                  onWheelKey: function (e) { window.__lastWheel = e; }
                };
                """.trimIndent(),
            )

            // Push the event from the native side.
            val event = WheelKeyEvent(WheelKey.Next, "2026-06-22T12:34:56Z")
            WebViewProbe.onWebView(scenario) { webView -> WheelKeyPush.push(webView, event) }

            assertTrue(
                "wheel-key event was not delivered to the page",
                WebViewProbe.waitUntilTrue(scenario, "window.__lastWheel !== null"),
            )
            assertEquals("\"Next\"", WebViewProbe.eval(scenario, "window.__lastWheel.key"))
            assertEquals(
                "\"2026-06-22T12:34:56Z\"",
                WebViewProbe.eval(scenario, "window.__lastWheel.timestampUtc"),
            )
        }
    }
}
