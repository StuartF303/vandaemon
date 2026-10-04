package com.vandaemon.shell.bridge

import android.webkit.WebView

/**
 * Native→UI event push for `INativeBridge.WheelKeyPressed`.
 *
 * Serialises a [WheelKeyEvent] to the wire shape defined in
 * specs/005-launcher-shell/contracts/js-interop-bridge.md and delivers it to the page's
 * event hook (`window.VanDaemonBridgeEvents.onWheelKey`) via `evaluateJavascript`.
 *
 * In the first pass this is raised ONLY when explicitly invoked (e.g. by an instrumented
 * test) — there is no real wheel-key source yet (FR-005, needs-reverse-engineering).
 */
object WheelKeyPush {

    /** Wire JSON: `{"key":"<WheelKey name>","timestampUtc":"<ISO-8601 UTC>"}`. */
    fun toJson(event: WheelKeyEvent): String =
        """{"key":"${event.key.name}","timestampUtc":"${event.timestampUtc}"}"""

    /** Pushes the event to the page on the WebView's thread. No-op if the hook is absent. */
    fun push(webView: WebView, event: WheelKeyEvent) {
        val js = "if (window.VanDaemonBridgeEvents && window.VanDaemonBridgeEvents.onWheelKey) " +
            "{ window.VanDaemonBridgeEvents.onWheelKey(${toJson(event)}); }"
        webView.post { webView.evaluateJavascript(js, null) }
    }
}
