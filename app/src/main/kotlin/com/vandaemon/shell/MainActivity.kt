package com.vandaemon.shell

import android.app.Activity
import android.os.Bundle
import android.webkit.WebView

/**
 * Thin WebView host activity. Lifecycle only: create → load → destroy.
 *
 * Ordinary launcher activity — declares no HOME/DEFAULT category (FR-009) and holds no
 * application logic (Constitution §XII.4). The hosted Blazor UI carries all behaviour.
 */
class MainActivity : Activity() {

    private lateinit var webView: WebView
    private lateinit var host: WebAppHost

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        webView = WebView(this).apply {
            // Tag lets instrumented tests locate the WebView without a resource id.
            tag = WEBVIEW_TAG
        }
        host = WebAppHost(this)
        host.configure(webView)
        setContentView(webView)
        webView.loadUrl(host.startUrl)
    }

    override fun onDestroy() {
        webView.destroy()
        super.onDestroy()
    }

    companion object {
        const val WEBVIEW_TAG = "vandaemon-webview"
    }
}
