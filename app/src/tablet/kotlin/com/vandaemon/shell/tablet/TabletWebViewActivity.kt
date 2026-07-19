package com.vandaemon.shell.tablet

import android.annotation.SuppressLint
import android.app.Activity
import android.os.Bundle
import android.view.WindowManager
import android.webkit.WebResourceError
import android.webkit.WebResourceRequest
import android.webkit.WebView
import android.webkit.WebViewClient

/**
 * Hosts the live Pi-served VanDaemon UI (FR-001). Thin: load the URL, keep the screen awake while
 * foreground (FR-012), and on a main-frame load failure (LAN drop) drop back to
 * [ConnectionActivity], which auto-recovers when the controller returns (FR-011).
 *
 * No native bridge is injected here — on a tablet the served UI's NativeBridgeFactory falls back to
 * its stub (FR-013). The vehicle bridge is a deferred head-unit capability.
 */
class TabletWebViewActivity : Activity() {

    private var webView: WebView? = null

    @SuppressLint("SetJavaScriptEnabled")
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        window.addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)

        val url = intent.getStringExtra(EXTRA_URL)
        if (url.isNullOrBlank()) {
            finish()
            return
        }

        val wv = WebView(this).apply {
            settings.javaScriptEnabled = true   // Blazor WASM needs JS
            settings.domStorageEnabled = true
            webViewClient = object : WebViewClient() {
                override fun onReceivedError(
                    view: WebView,
                    request: WebResourceRequest,
                    error: WebResourceError,
                ) {
                    // Only a main-frame failure means we lost the controller; ignore sub-resource errors.
                    if (request.isForMainFrame) finish()
                }
            }
        }
        webView = wv
        setContentView(wv)
        wv.loadUrl(url)
    }

    override fun onDestroy() {
        webView?.destroy()
        webView = null
        super.onDestroy()
    }

    companion object {
        const val EXTRA_URL = "url"
    }
}
