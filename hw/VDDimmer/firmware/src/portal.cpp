#include "portal.h"
#include "settings.h"
#include "store.h"
#include "strips.h"
#include <WiFi.h>
#include <WebServer.h>
#include <DNSServer.h>

static WebServer *s_server = nullptr;
static DNSServer *s_dns    = nullptr;
static bool       s_active = false;
static uint32_t   s_rebootAt = 0;

// What the radio could hear when the portal started. Recorded because you
// configure this board from a phone or tablet, where the serial log is not
// available -- and because "SSID not found" has two very different causes that
// only a scan can tell apart: a router problem, or no antenna.
//
// U1 is an ESP32-S3-WROOM-1U: it has NO PCB antenna, only a U.FL connector.
// With nothing fitted the receiver is deaf and every scan comes back empty.
struct ScanEntry {
    char    ssid[33];
    int8_t  rssi;
    uint8_t channel;
    bool    open;
};
static constexpr uint8_t SCAN_MAX = 16;
static ScanEntry s_scan[SCAN_MAX];
static uint8_t   s_scanCount = 0;
static bool      s_scanRan   = false;

static void scanNetworks() {
    // Must be in station mode to scan; portal_begin() switches to AP after.
    WiFi.mode(WIFI_STA);
    WiFi.disconnect(false, false);
    delay(100);

    int n = WiFi.scanNetworks(false, true);   // blocking, include hidden
    s_scanCount = 0;
    s_scanRan   = true;

    if (n < 0) n = 0;
    for (int i = 0; i < n && s_scanCount < SCAN_MAX; i++) {
        ScanEntry &e = s_scan[s_scanCount];
        String ss = WiFi.SSID(i);
        if (ss.length() == 0) ss = "(hidden)";
        strlcpy(e.ssid, ss.c_str(), sizeof(e.ssid));
        e.rssi    = (int8_t)WiFi.RSSI(i);
        e.channel = (uint8_t)WiFi.channel(i);
        e.open    = (WiFi.encryptionType(i) == WIFI_AUTH_OPEN);
        s_scanCount++;
    }
    WiFi.scanDelete();

    Serial.printf("[scan] %d network(s) visible\n", n);
    for (uint8_t i = 0; i < s_scanCount; i++) {
        Serial.printf("[scan]   %-32s ch%-3u %4d dBm%s\n",
                      s_scan[i].ssid, (unsigned)s_scan[i].channel,
                      (int)s_scan[i].rssi, s_scan[i].open ? "  open" : "");
    }
    if (n == 0) {
        Serial.println("[scan] NOTHING HEARD. U1 is a WROOM-1U with no PCB antenna --");
        Serial.println("[scan] check that an antenna is fitted to the U.FL connector.");
    }
}

static String htmlEscape(const char *s) {
    String out;
    for (const char *p = s; *p; p++) {
        switch (*p) {
            case '&': out += "&amp;";  break;
            case '<': out += "&lt;";   break;
            case '>': out += "&gt;";   break;
            case '"': out += "&quot;"; break;
            default:  out += *p;
        }
    }
    return out;
}

static String field(const char *label, const char *name, const char *value,
                    const char *type = "text") {
    String s = "<label>";
    s += label;
    s += "<input type=\"";
    s += type;
    s += "\" name=\"";
    s += name;
    s += "\" value=\"";
    s += htmlEscape(value);
    s += "\"></label>";
    return s;
}

static String checkbox(const char *label, const char *name, bool value) {
    String s = "<label class=\"cb\"><input type=\"checkbox\" name=\"";
    s += name;
    s += "\"";
    if (value) s += " checked";
    s += "> ";
    s += label;
    s += "</label>";
    return s;
}

static const char PORTAL_HEAD[] =
    "<!doctype html><meta name=viewport content=\"width=device-width,initial-scale=1\">"
    "<title>VANDIMMER setup</title><style>"
    "body{font:16px system-ui;margin:0;padding:1.2rem;background:#111;color:#eee}"
    "h1{font-size:1.2rem}h2{font-size:.95rem;color:#8bc;margin:1.4rem 0 .4rem}"
    "label{display:block;margin:.5rem 0}label.cb{display:flex;gap:.5rem;align-items:center}"
    "input[type=text],input[type=password],input[type=number]{width:100%;box-sizing:border-box;"
    "padding:.5rem;margin-top:.2rem;background:#222;color:#eee;border:1px solid #444;border-radius:4px}"
    "button{margin-top:1.2rem;padding:.7rem 1.2rem;font-size:1rem;background:#2a7;color:#000;"
    "border:0;border-radius:4px;width:100%}small{color:#999}"
    "ul.aps{list-style:none;padding:0;margin:.2rem 0}"
    "ul.aps li{padding:.35rem 0;border-bottom:1px solid #333}"
    "ul.aps a{color:#8bc;text-decoration:none}"
    "p.warn{background:#402; border:1px solid #a44; padding:.6rem; border-radius:4px}"
    "</style>"
    "<h1>VANDIMMER-4CH+2A</h1><form method=post action=/save>";

static void handleRoot() {
    String p = PORTAL_HEAD;

    p += "<h2>Identity</h2>";
    p += field("Device ID (unique, MQTT topic level)", "devid", g_settings.deviceId);
    p += field("Display name", "devname", g_settings.deviceName);

    p += "<h2>WiFi</h2>";

    // Show what the radio can actually hear. Tapping a row fills the SSID box,
    // which removes the commonest configuration mistake -- a typo, or picking a
    // band this radio cannot use. The ESP32-S3 is 2.4 GHz only, so anything
    // broadcasting solely on 5 GHz will simply not appear here.
    if (s_scanRan) {
        if (s_scanCount == 0) {
            p += "<p class=\"warn\"><b>No networks visible.</b> This module "
                 "(ESP32-S3-WROOM-1U) has no PCB antenna &mdash; check that an "
                 "antenna is fitted to the U.FL connector on U1.</p>";
        } else {
            p += "<p><small>Visible networks &mdash; tap to fill in:</small></p><ul class=\"aps\">";
            for (uint8_t i = 0; i < s_scanCount; i++) {
                p += "<li><a href=\"#\" onclick=\"document.getElementsByName('ssid')[0].value=this.dataset.s;return false\" data-s=\"";
                p += htmlEscape(s_scan[i].ssid);
                p += "\">";
                p += htmlEscape(s_scan[i].ssid);
                p += "</a> <small>ch";
                p += String((unsigned)s_scan[i].channel);
                p += ", ";
                p += String((int)s_scan[i].rssi);
                p += " dBm";
                p += s_scan[i].open ? ", open" : "";
                p += "</small></li>";
            }
            p += "</ul>";
        }
    }

    p += field("SSID", "ssid", g_settings.wifiSsid);
    p += field("Password (blank = unchanged)", "wpass", "", "password");

    p += "<h2>MQTT</h2>";
    p += field("Broker host", "mqhost", g_settings.mqttHost);
    p += field("Port", "mqport", String(g_settings.mqttPort).c_str(), "number");
    p += field("Username", "mquser", g_settings.mqttUser);
    p += field("Password (blank = unchanged)", "mqpass", "", "password");
    p += field("Base topic", "base", g_settings.baseTopic);

    p += "<h2>Addressable outputs</h2>";
    for (uint8_t i = 0; i < STRIP_COUNT; i++) {
        char lbl[64], nm[12];
        snprintf(lbl, sizeof(lbl), "Strip %u length (pixels)", (unsigned)(i + 1));
        snprintf(nm, sizeof(nm), "s%ulen", (unsigned)i);
        p += field(lbl, nm, String(g_settings.strip[i].length).c_str(), "number");

        snprintf(lbl, sizeof(lbl), "Strip %u fed from 5 V (J%u shunt on pins 2-3)",
                 (unsigned)(i + 1), (unsigned)(9 + i));
        snprintf(nm, sizeof(nm), "s%usup5", (unsigned)i);
        p += checkbox(lbl, nm, g_settings.strip[i].supply5v);
    }
    p += "<small>Set the 5 V box to match the jumper. It only drives the power "
         "cap, but a 5 V strip with the box clear can trip the polyfuse.</small>";

    p += "<h2>Behaviour</h2>";
    p += field("PWM frequency (Hz, 200-20000)", "pwmfreq",
               String(g_settings.pwmFreqHz).c_str(), "number");
    p += checkbox("Gamma-corrected dimming", "gamma", g_settings.gammaCorrect);
    p += checkbox("Restore last levels at power-up", "restore", g_settings.restoreOnBoot);
    p += checkbox("Expose strips as channels 5 and 6", "expstrip",
                  g_settings.exposeStripsAsChannels);
    p += "<small>Turn the last one off once VanDaemon has a dedicated "
         "colour-strip module, or the same hardware gets two sets of controls.</small>";

    p += "<button type=submit>Save and reboot</button></form>";
    s_server->send(200, "text/html", p);
}

static void copyArg(const char *name, char *dst, size_t len, bool keepIfBlank) {
    if (!s_server->hasArg(name)) return;
    String v = s_server->arg(name);
    if (keepIfBlank && v.length() == 0) return;   // blank password = unchanged
    strlcpy(dst, v.c_str(), len);
}

static void handleSave() {
    copyArg("devid",   g_settings.deviceId,   sizeof(g_settings.deviceId),   true);
    copyArg("devname", g_settings.deviceName, sizeof(g_settings.deviceName), true);
    copyArg("ssid",    g_settings.wifiSsid,   sizeof(g_settings.wifiSsid),   false);
    copyArg("wpass",   g_settings.wifiPass,   sizeof(g_settings.wifiPass),   true);
    copyArg("mqhost",  g_settings.mqttHost,   sizeof(g_settings.mqttHost),   false);
    copyArg("mquser",  g_settings.mqttUser,   sizeof(g_settings.mqttUser),   false);
    copyArg("mqpass",  g_settings.mqttPass,   sizeof(g_settings.mqttPass),   true);
    copyArg("base",    g_settings.baseTopic,  sizeof(g_settings.baseTopic),  true);

    if (s_server->hasArg("mqport")) {
        g_settings.mqttPort = (uint16_t)s_server->arg("mqport").toInt();
        if (g_settings.mqttPort == 0) g_settings.mqttPort = 1883;
    }

    for (uint8_t i = 0; i < STRIP_COUNT; i++) {
        char nm[12];
        snprintf(nm, sizeof(nm), "s%ulen", (unsigned)i);
        if (s_server->hasArg(nm)) {
            long v = s_server->arg(nm).toInt();
            if (v < 0) v = 0;
            if (v > STRIP_MAX_PIXELS) v = STRIP_MAX_PIXELS;
            g_settings.strip[i].length = (uint16_t)v;
        }
        snprintf(nm, sizeof(nm), "s%usup5", (unsigned)i);
        g_settings.strip[i].supply5v = s_server->hasArg(nm);
    }

    if (s_server->hasArg("pwmfreq")) {
        long f = s_server->arg("pwmfreq").toInt();
        if (f < 200) f = 200;
        if (f > 20000) f = 20000;
        g_settings.pwmFreqHz = (uint16_t)f;
    }
    g_settings.gammaCorrect           = s_server->hasArg("gamma");
    g_settings.restoreOnBoot          = s_server->hasArg("restore");
    g_settings.exposeStripsAsChannels = s_server->hasArg("expstrip");

    store_saveSettings();

    s_server->send(200, "text/html",
                   "<!doctype html><meta name=viewport content=\"width=device-width,initial-scale=1\">"
                   "<body style=\"font:16px system-ui;background:#111;color:#eee;padding:2rem\">"
                   "Saved. Rebooting.");
    s_rebootAt = millis() + 800;
}

void portal_begin() {
    char ap[32];
    snprintf(ap, sizeof(ap), "VANDIMMER-%s", store_macSuffix());

    scanNetworks();          // while still in station mode

    WiFi.mode(WIFI_AP);
    WiFi.softAP(ap);

    s_dns = new DNSServer();
    s_dns->start(53, "*", WiFi.softAPIP());

    s_server = new WebServer(80);
    s_server->on("/", handleRoot);
    s_server->on("/save", HTTP_POST, handleSave);
    s_server->onNotFound(handleRoot);   // captive-portal catch-all
    s_server->begin();

    s_active = true;
    status_set(STATUS_PORTAL);

    Serial.printf("[portal] AP '%s' at %s\n", ap, WiFi.softAPIP().toString().c_str());
}

void portal_tick() {
    if (!s_active) return;
    s_dns->processNextRequest();
    s_server->handleClient();
    if (s_rebootAt && millis() > s_rebootAt) ESP.restart();
}

bool portal_active() { return s_active; }
