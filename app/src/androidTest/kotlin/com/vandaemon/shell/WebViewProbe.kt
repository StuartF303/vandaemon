package com.vandaemon.shell

import android.webkit.WebView
import androidx.test.core.app.ActivityScenario
import java.util.concurrent.CountDownLatch
import java.util.concurrent.TimeUnit

/**
 * Test-only helper to drive the hosted WebView from instrumented tests: locate it by tag,
 * run JS on the UI thread, and poll for conditions. Keeps the tests free of timing glue.
 */
object WebViewProbe {

    /** Runs [js] on the UI thread and returns its JSON-encoded result (or "null" on timeout). */
    fun eval(scenario: ActivityScenario<MainActivity>, js: String, timeoutMs: Long = 10_000): String {
        val latch = CountDownLatch(1)
        val box = arrayOfNulls<String>(1)
        scenario.onActivity { activity ->
            val webView = activity.window.decorView
                .findViewWithTag<WebView>(MainActivity.WEBVIEW_TAG)
            webView.evaluateJavascript(js) { value ->
                box[0] = value
                latch.countDown()
            }
        }
        latch.await(timeoutMs, TimeUnit.MILLISECONDS)
        return box[0] ?: "null"
    }

    /** Polls until the JS boolean expression [boolExpr] is true, or times out. */
    fun waitUntilTrue(
        scenario: ActivityScenario<MainActivity>,
        boolExpr: String,
        timeoutMs: Long = 30_000,
    ): Boolean {
        val deadline = System.currentTimeMillis() + timeoutMs
        while (System.currentTimeMillis() < deadline) {
            if (eval(scenario, "($boolExpr).toString()") == "\"true\"") return true
            Thread.sleep(250)
        }
        return false
    }

    /** Runs [block] against the WebView on the UI thread (e.g. to push a native event). */
    fun onWebView(scenario: ActivityScenario<MainActivity>, block: (WebView) -> Unit) {
        scenario.onActivity { activity ->
            val webView = activity.window.decorView
                .findViewWithTag<WebView>(MainActivity.WEBVIEW_TAG)
            block(webView)
        }
    }
}
