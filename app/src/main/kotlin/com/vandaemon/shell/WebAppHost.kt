package com.vandaemon.shell

import android.annotation.SuppressLint
import android.content.Context
import android.util.Log
import android.webkit.ConsoleMessage
import android.webkit.WebChromeClient
import android.webkit.WebResourceError
import android.webkit.WebResourceRequest
import android.webkit.WebResourceResponse
import android.webkit.WebView
import android.webkit.WebViewClient
import androidx.webkit.WebViewAssetLoader
import com.vandaemon.shell.bridge.NativeBridge

/**
 * Configures a [WebView] to host the bundled VanDaemon Blazor WASM UI:
 *  - serves it from APK assets via [WebViewAssetLoader] on a virtual same-origin origin
 *    (offline, no server — FR-002), and
 *  - injects the native bridge ([NativeBridge]) as `window.VanDaemonNativeBridge` (FR-004).
 *
 * Thin host/transport only — no application logic (Constitution §XII.4).
 */
class WebAppHost(context: Context) {

    private val assetLoader: WebViewAssetLoader = WebViewAssetLoader.Builder()
        .addPathHandler("/", WwwAssetPathHandler(context.applicationContext))
        .build()

    /**
     * Virtual same-origin start URL — the origin ROOT, not `/index.html`. The handler serves
     * `index.html` for the empty path, and loading `/` lets the Blazor router match its `@page "/"`
     * (the Dashboard) instead of treating `/index.html` as an unknown route.
     */
    val startUrl: String = "https://$VIRTUAL_HOST/"

    @SuppressLint("SetJavaScriptEnabled")
    fun configure(webView: WebView, bridge: NativeBridge = NativeBridge()) {
        webView.settings.apply {
            javaScriptEnabled = true   // required to run Blazor WASM + JS-interop
            domStorageEnabled = true
            allowFileAccess = false    // assets come only via the loader (same-origin)
            allowContentAccess = false
        }
        webView.addJavascriptInterface(bridge, BRIDGE_JS_NAME)
        webView.webViewClient = object : WebViewClient() {
            override fun shouldInterceptRequest(
                view: WebView,
                request: WebResourceRequest,
            ): WebResourceResponse? {
                val response = assetLoader.shouldInterceptRequest(request.url)
                if (response == null) {
                    Log.w(TAG, "asset MISS (not intercepted): ${request.method} ${request.url}")
                }
                return response
            }

            override fun onReceivedError(
                view: WebView,
                request: WebResourceRequest,
                error: WebResourceError,
            ) {
                Log.e(TAG, "loadError: ${request.url} -> ${error.errorCode} ${error.description}")
            }

            override fun onReceivedHttpError(
                view: WebView,
                request: WebResourceRequest,
                errorResponse: WebResourceResponse,
            ) {
                Log.e(TAG, "httpError: ${request.url} -> ${errorResponse.statusCode}")
            }
        }
        webView.webChromeClient = object : WebChromeClient() {
            override fun onConsoleMessage(message: ConsoleMessage): Boolean {
                Log.i(
                    TAG,
                    "console[${message.messageLevel()}] ${message.message()} " +
                        "(${message.sourceId()}:${message.lineNumber()})",
                )
                return true
            }
        }
    }

    companion object {
        const val VIRTUAL_HOST = "appassets.androidplatform.net"
        const val BRIDGE_JS_NAME = "VanDaemonNativeBridge"
        private const val TAG = "VanDaemonShell"
    }
}
