package com.vandaemon.shell

import androidx.test.core.app.ActivityScenario
import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith

/**
 * SC-002 / FR-002 / FR-003: the bundled Blazor UI loads in the WebView from the virtual
 * app-assets origin, and the framework assets are served. Runs on a current-WebView
 * emulator — this proves the seam on a modern WebView; it does NOT stand in for the FYT
 * unit's (older) WebView, which remains the human-run SC-006 (Constitution §XIII.2-3).
 */
@RunWith(AndroidJUnit4::class)
class AssetLoadTest {

    @Test
    fun page_loadsFromVirtualOrigin_withKnownTitleAndRoot() {
        ActivityScenario.launch(MainActivity::class.java).use { scenario ->
            assertTrue(
                "document never reached readyState=complete",
                WebViewProbe.waitUntilTrue(scenario, "document.readyState === 'complete'"),
            )

            // Served from the virtual app-assets origin, not file:// (FR-002).
            assertEquals(
                "\"https://appassets.androidplatform.net\"",
                WebViewProbe.eval(scenario, "document.location.origin"),
            )

            // Known document title from the bundled index.html.
            assertEquals(
                "\"VanDaemon Control System\"",
                WebViewProbe.eval(scenario, "document.title"),
            )

            // Known root element present (SC-002).
            assertEquals(
                "\"true\"",
                WebViewProbe.eval(scenario, "(document.getElementById('app') !== null).toString()"),
            )
        }
    }

    @Test
    fun frameworkAsset_isServedWithCorrectMime() {
        ActivityScenario.launch(MainActivity::class.java).use { scenario ->
            WebViewProbe.waitUntilTrue(scenario, "document.readyState === 'complete'")

            // Fetch a stable framework asset (_framework/dotnet.js, always present in the
            // .NET publish output) to prove the asset handler serves the framework dir with
            // the right MIME through the real WebView (FR-003). A miss would surface as a
            // network error (the handler returns null -> WebViewAssetLoader falls through).
            WebViewProbe.eval(
                scenario,
                """
                window.__fwProbe = 'pending';
                fetch('/_framework/dotnet.js')
                  .then(function (r) { window.__fwProbe = r.status + '|' + (r.headers.get('content-type') || ''); })
                  .catch(function (e) { window.__fwProbe = 'error:' + e; });
                """.trimIndent(),
            )
            assertTrue(
                "framework asset fetch did not resolve",
                WebViewProbe.waitUntilTrue(scenario, "window.__fwProbe !== 'pending'", 15_000),
            )
            val probe = WebViewProbe.eval(scenario, "window.__fwProbe")
            assertTrue(
                "expected 200 + javascript content-type, got $probe",
                probe.contains("200") && probe.contains("javascript"),
            )
        }
    }
}
