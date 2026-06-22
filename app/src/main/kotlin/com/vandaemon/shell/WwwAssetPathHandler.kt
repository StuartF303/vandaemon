package com.vandaemon.shell

import android.content.Context
import android.webkit.WebResourceResponse
import androidx.webkit.WebViewAssetLoader
import java.io.IOException

/**
 * Serves the bundled Blazor app from APK assets under `assets/www/`, returning correct
 * MIME types — notably `application/wasm` — so the WebView's fetch + streaming WASM
 * compilation behave (FR-003).
 *
 * Mapped at the virtual-origin root (`/`), so the published `index.html`'s `<base href="/">`
 * and its relative `_framework/...` references resolve cleanly: every request path is
 * prefixed into `www/`. The empty path resolves to `index.html`.
 *
 * Returning `null` lets [WebViewAssetLoader] surface a 404 for unknown paths.
 */
class WwwAssetPathHandler(private val context: Context) : WebViewAssetLoader.PathHandler {

    override fun handle(path: String): WebResourceResponse? {
        val relative = path.trimStart('/').ifEmpty { "index.html" }
        val assetPath = "www/$relative"
        return try {
            val stream = context.assets.open(assetPath)
            WebResourceResponse(mimeOf(relative), null, stream)
        } catch (e: IOException) {
            null
        }
    }

    companion object {
        /** Maps a file name to the MIME type the WebView needs (notably application/wasm). */
        fun mimeOf(name: String): String = when {
            name.endsWith(".html") -> "text/html"
            name.endsWith(".js") || name.endsWith(".mjs") -> "text/javascript"
            name.endsWith(".css") -> "text/css"
            name.endsWith(".wasm") -> "application/wasm"
            name.endsWith(".json") -> "application/json"
            name.endsWith(".woff2") -> "font/woff2"
            name.endsWith(".woff") -> "font/woff"
            name.endsWith(".ttf") -> "font/ttf"
            name.endsWith(".png") -> "image/png"
            name.endsWith(".jpg") || name.endsWith(".jpeg") -> "image/jpeg"
            name.endsWith(".svg") -> "image/svg+xml"
            name.endsWith(".ico") -> "image/x-icon"
            name.endsWith(".webmanifest") -> "application/manifest+json"
            // .dll, .dat, .blat, .pdb and anything else: raw bytes.
            else -> "application/octet-stream"
        }
    }
}
