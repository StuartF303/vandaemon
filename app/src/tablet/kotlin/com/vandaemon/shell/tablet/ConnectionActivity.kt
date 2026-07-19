package com.vandaemon.shell.tablet

import android.app.Activity
import android.content.Intent
import android.os.Bundle
import android.os.Handler
import android.os.Looper
import android.widget.Button
import android.widget.EditText
import android.widget.TextView
import com.vandaemon.shell.R
import kotlin.concurrent.thread

/**
 * Native connection/waiting screen — the app's entry point and home activity (FR-002, FR-005).
 *
 * Because VanDaemon UI is the home app, this is the first thing shown on power-up, often before the
 * Pi is up. It NEVER shows a blank/broken page: it polls the controller (probing that the UI actually
 * responds, FR-008), auto-advances to the WebView when reachable (FR-006), offers manual Retry
 * (FR-007), and lets the owner correct the address (FR-010). Returning here after a LAN drop
 * auto-recovers (FR-011).
 */
class ConnectionActivity : Activity() {

    private lateinit var prefs: ControllerPrefs
    private lateinit var statusText: TextView
    private lateinit var addressField: EditText
    private lateinit var retryButton: Button

    private val handler = Handler(Looper.getMainLooper())
    private var polling = false
    private var attempt = 0

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        prefs = ControllerPrefs(this)
        setContentView(R.layout.activity_connection)

        statusText = findViewById(R.id.status_text)
        addressField = findViewById(R.id.address_field)
        retryButton = findViewById(R.id.retry_button)

        addressField.setText("${prefs.host}:${prefs.port}")
        retryButton.setOnClickListener { applyAddressAndRetry() }
    }

    override fun onResume() {
        super.onResume()
        // Restart polling whenever we come to the foreground — including after the WebView drops back
        // here on a LAN failure (FR-011).
        attempt = 0
        startPolling()
    }

    override fun onPause() {
        super.onPause()
        polling = false
        handler.removeCallbacksAndMessages(null)
    }

    private fun applyAddressAndRetry() {
        val (h, p) = parseHostPort(addressField.text.toString())
        prefs.host = h
        prefs.port = p
        addressField.setText("$h:$p")
        attempt = 0
        polling = false
        handler.removeCallbacksAndMessages(null)
        startPolling()
    }

    private fun startPolling() {
        if (polling) return
        polling = true
        pollOnce()
    }

    private fun pollOnce() {
        if (!polling) return
        val baseUrl = prefs.baseUrl
        statusText.text = getString(R.string.connecting_to, baseUrl)
        thread {
            val up = ControllerResolver.isControllerUp(baseUrl)
            handler.post {
                if (!polling) return@post
                if (up) {
                    polling = false
                    launchUi(baseUrl)
                } else {
                    attempt++
                    statusText.text = getString(R.string.waiting_for, baseUrl)
                    handler.postDelayed({ pollOnce() }, backoffMs(attempt))
                }
            }
        }
    }

    private fun launchUi(baseUrl: String) {
        startActivity(
            Intent(this, TabletWebViewActivity::class.java)
                .putExtra(TabletWebViewActivity.EXTRA_URL, baseUrl)
        )
    }

    /** Exponential backoff, 2s → 16s cap, so a booting/absent Pi is retried without hammering. */
    private fun backoffMs(n: Int): Long = minOf(1000L * (1L shl minOf(n, 4)), 16000L)

    /** Parses "host", "host:port", or a pasted URL into (host, port); missing port → default. */
    private fun parseHostPort(raw: String): Pair<String, Int> {
        val cleaned = raw.trim()
            .removePrefix("http://")
            .removePrefix("https://")
            .trimEnd('/')
        val idx = cleaned.lastIndexOf(':')
        return if (idx > 0) {
            val h = cleaned.substring(0, idx).ifBlank { ControllerPrefs.DEFAULT_HOST }
            val p = cleaned.substring(idx + 1).toIntOrNull() ?: ControllerPrefs.DEFAULT_PORT
            h to p
        } else {
            cleaned.ifBlank { ControllerPrefs.DEFAULT_HOST } to ControllerPrefs.DEFAULT_PORT
        }
    }
}
