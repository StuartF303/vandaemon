package com.vandaemon.shell

import org.junit.Assert.assertEquals
import org.junit.Test

/**
 * FR-003: the asset handler returns the MIME types the WebView needs — above all
 * `application/wasm` for streaming WASM compilation. Pure JVM test of the mapping.
 */
class AssetMimeTest {

    @Test
    fun wasm_mapsToApplicationWasm() {
        assertEquals("application/wasm", WwwAssetPathHandler.mimeOf("dotnet.native.wasm"))
    }

    @Test
    fun webAssets_mapToExpectedTypes() {
        assertEquals("text/html", WwwAssetPathHandler.mimeOf("index.html"))
        assertEquals("text/javascript", WwwAssetPathHandler.mimeOf("blazor.webassembly.js"))
        assertEquals("text/css", WwwAssetPathHandler.mimeOf("app.css"))
        assertEquals("application/json", WwwAssetPathHandler.mimeOf("blazor.boot.json"))
    }

    @Test
    fun unknownAndBinaryAssets_fallBackToOctetStream() {
        assertEquals("application/octet-stream", WwwAssetPathHandler.mimeOf("VanDaemon.Web.dll"))
        assertEquals("application/octet-stream", WwwAssetPathHandler.mimeOf("something.dat"))
    }
}
