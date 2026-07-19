package com.vandaemon.shell.tablet

import java.net.HttpURLConnection
import java.net.URL

/**
 * Decides whether the VanDaemon controller is actually serving — the UI responds, not merely that the
 * host pings (FR-008). Probes {baseUrl}/health (the appliance's nginx proxies /health to the API).
 *
 * v1 relies on the OS resolving `vandaemon.local` (or a configured IP). mDNS service discovery via
 * NsdManager is a deferred fast-follow if OS resolution proves unreliable in testing.
 */
object ControllerResolver {

    /** Blocking; call OFF the UI thread. True iff GET {baseUrl}/health returns 2xx within the timeout. */
    fun isControllerUp(baseUrl: String, timeoutMs: Int = 3000): Boolean {
        var conn: HttpURLConnection? = null
        return try {
            conn = (URL("$baseUrl/health").openConnection() as HttpURLConnection).apply {
                connectTimeout = timeoutMs
                readTimeout = timeoutMs
                requestMethod = "GET"
                instanceFollowRedirects = true
            }
            conn.responseCode in 200..299
        } catch (e: Exception) {
            false
        } finally {
            conn?.disconnect()
        }
    }
}
