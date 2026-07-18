package com.vandaemon.shell.tablet

import android.content.Context

/**
 * The controller address (host + port) the tablet connects to — the only state the tablet client
 * owns (FR-010). Defaults to the appliance name on the van LAN (FR-009); owner-editable and persisted
 * natively, because when the Pi is unreachable the served UI can't be loaded to configure itself.
 */
class ControllerPrefs(context: Context) {

    private val prefs = context.getSharedPreferences(PREFS, Context.MODE_PRIVATE)

    var host: String
        get() = prefs.getString(KEY_HOST, DEFAULT_HOST) ?: DEFAULT_HOST
        set(value) { prefs.edit().putString(KEY_HOST, value.trim()).apply() }

    var port: Int
        get() = prefs.getInt(KEY_PORT, DEFAULT_PORT)
        set(value) { prefs.edit().putInt(KEY_PORT, value).apply() }

    /** Base URL of the controller, e.g. http://vandaemon.local:8080 */
    val baseUrl: String get() = "http://$host:$port"

    companion object {
        const val DEFAULT_HOST = "vandaemon.local"
        const val DEFAULT_PORT = 8080
        private const val PREFS = "vandaemon_controller"
        private const val KEY_HOST = "host"
        private const val KEY_PORT = "port"
    }
}
