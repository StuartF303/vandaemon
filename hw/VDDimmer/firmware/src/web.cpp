#include "web.h"
#include "settings.h"
#include "store.h"
#include "pwm.h"
#include "control.h"
#include "net.h"
#include "status.h"
#include "strips.h"
#include "board_pins.h"

#include <WebServer.h>
#include <uri/UriBraces.h>
#include <ArduinoJson.h>

// The lights page, generated from src/web/index.html by tools/embed_web.py at
// build time. If the generator has not run, / says so instead of 404ing.
#if __has_include("web_page.gen.h")
#include "web_page.gen.h"
#define HAVE_WEB_PAGE 1
#else
#define HAVE_WEB_PAGE 0
#endif

static WebServer *s_server   = nullptr;
static uint32_t   s_rebootAt = 0;

static constexpr size_t BODY_MAX     = 512;
static constexpr size_t STATUS_BYTES = 512;
static constexpr size_t DEVNAME_MAX  = sizeof(g_settings.deviceName) - 1;   // 39
static constexpr size_t HOST_MAX     = sizeof(g_settings.mqttHost) - 1;
static constexpr size_t USER_MAX     = sizeof(g_settings.mqttUser) - 1;
static constexpr size_t PASS_MAX     = sizeof(g_settings.mqttPass) - 1;

// ----------------------------------------------------------------- replies

static void sendJson(int code, const JsonDocument &doc) {
    String out;
    serializeJson(doc, out);
    s_server->sendHeader("Cache-Control", "no-store");
    s_server->send(code, "application/json", out);
}

static void sendError(int code, const char *msg) {
    JsonDocument doc;
    doc["error"] = msg;
    sendJson(code, doc);
}

static void sendOk() {
    JsonDocument doc;
    doc["ok"] = true;
    sendJson(200, doc);
}

// Parse the request body as JSON. Returns true and fills doc, or has already
// sent the error reply and returns false.
static bool readBody(JsonDocument &doc) {
    if (!s_server->hasArg("plain")) {
        sendError(400, "bad json");
        return false;
    }
    const String &body = s_server->arg("plain");
    if (body.length() > BODY_MAX) {
        sendError(413, "too large");
        return false;
    }
    if (deserializeJson(doc, body) != DeserializationError::Ok) {
        sendError(400, "bad json");
        return false;
    }
    return true;
}

// A JSON value that is a string of at most maxLen bytes. Missing or wrong type
// returns false; the caller decides whether that is an error.
static bool strField(JsonVariantConst v, size_t maxLen, const char *&out) {
    if (!v.is<const char *>()) return false;
    const char *s = v.as<const char *>();
    if (s == nullptr || strlen(s) > maxLen) return false;
    out = s;
    return true;
}

// Strict unsigned decimal: digits only, no sign, no whitespace.
static bool parseUInt(const String &s, long &out) {
    if (s.length() == 0 || s.length() > 9) return false;
    long v = 0;
    for (size_t i = 0; i < s.length(); i++) {
        char c = s[i];
        if (c < '0' || c > '9') return false;
        v = v * 10 + (c - '0');
    }
    out = v;
    return true;
}

// ---------------------------------------------------------------- handlers

static void handlePage() {
    s_server->sendHeader("Cache-Control", "no-cache");
#if HAVE_WEB_PAGE
    s_server->send_P(200, "text/html; charset=utf-8", WEB_PAGE, WEB_PAGE_LEN);
#else
    s_server->send(200, "text/plain", "VANDIMMER web: page not built (tools/embed_web.py did not run)");
#endif
}

static void handleState() {
    JsonDocument doc;
    doc["deviceId"]   = g_settings.deviceId;
    doc["deviceName"] = g_settings.deviceName;
    JsonArray levels = doc["levels"].to<JsonArray>();
    JsonArray names  = doc["names"].to<JsonArray>();
    for (uint8_t i = 0; i < PWM_CHANNELS; i++) {
        levels.add(pwm_get(i));
        names.add(g_settings.chName[i]);
    }
    sendJson(200, doc);
}

static void handleStatus() {
    char buf[STATUS_BYTES];
    size_t n = status_json(buf, sizeof(buf));
    if (n == 0) {
        sendError(500, "status too large");
        return;
    }
    s_server->sendHeader("Cache-Control", "no-store");
    s_server->send(200, "application/json", buf);
}

static void handleChannel() {
    long ch;
    if (!parseUInt(s_server->pathArg(0), ch) || ch >= PWM_CHANNELS) {
        sendError(400, "bad channel");
        return;
    }
    JsonDocument doc;
    if (!readBody(doc)) return;
    JsonVariantConst lv = doc["level"];
    if (!lv.is<long>() || lv.is<const char *>()) {
        sendError(400, "bad level");
        return;
    }
    long level = lv.as<long>();
    if (level < 0 || level > 255) {
        sendError(400, "bad level");
        return;
    }
    ctl_setLevel((uint8_t)ch, (uint8_t)level);

    JsonDocument reply;
    reply["ok"]    = true;
    reply["level"] = level;
    sendJson(200, reply);
}

static void handleNames() {
    JsonDocument doc;
    if (!readBody(doc)) return;

    const char *devName;
    if (!strField(doc["deviceName"], DEVNAME_MAX, devName)) {
        sendError(400, "bad deviceName");
        return;
    }
    JsonArrayConst names = doc["names"].as<JsonArrayConst>();
    if (names.isNull() || names.size() != PWM_CHANNELS) {
        sendError(400, "names must have 4 entries");
        return;
    }
    const char *nm[PWM_CHANNELS];
    for (uint8_t i = 0; i < PWM_CHANNELS; i++) {
        if (!strField(names[i], CH_NAME_MAX, nm[i])) {
            sendError(400, "name too long");
            return;
        }
    }

    // Validated in full before anything is written, so a bad request changes
    // nothing.
    strlcpy(g_settings.deviceName, devName, sizeof(g_settings.deviceName));
    for (uint8_t i = 0; i < PWM_CHANNELS; i++) {
        strlcpy(g_settings.chName[i], nm[i], sizeof(g_settings.chName[i]));
    }
    store_saveSettings();
    net_republishConfig();
    Serial.printf("[web] names saved: '%s' / %s | %s | %s | %s\n",
                  g_settings.deviceName, g_settings.chName[0], g_settings.chName[1],
                  g_settings.chName[2], g_settings.chName[3]);
    sendOk();
}

static void handleMqtt() {
    JsonDocument doc;
    if (!readBody(doc)) return;

    const char *host;
    if (!strField(doc["host"], HOST_MAX, host) || host[0] == '\0') {
        sendError(400, "bad host");
        return;
    }
    long port = 1883;
    if (!doc["port"].isNull()) {
        if (!doc["port"].is<long>()) {
            sendError(400, "bad port");
            return;
        }
        port = doc["port"].as<long>();
        if (port < 1 || port > 65535) {
            sendError(400, "bad port");
            return;
        }
    }
    const char *user = "";
    if (!doc["user"].isNull() && !strField(doc["user"], USER_MAX, user)) {
        sendError(400, "bad user");
        return;
    }
    const char *pass = nullptr;   // null or blank = keep the stored one
    if (!doc["pass"].isNull() && !strField(doc["pass"], PASS_MAX, pass)) {
        sendError(400, "bad pass");
        return;
    }

    strlcpy(g_settings.mqttHost, host, sizeof(g_settings.mqttHost));
    g_settings.mqttPort = (uint16_t)port;
    strlcpy(g_settings.mqttUser, user, sizeof(g_settings.mqttUser));
    if (pass && pass[0]) strlcpy(g_settings.mqttPass, pass, sizeof(g_settings.mqttPass));
    store_saveSettings();
    net_mqttReconfigure();
    Serial.printf("[web] broker -> %s:%u\n", g_settings.mqttHost, (unsigned)g_settings.mqttPort);
    sendOk();
}

static void handleIdentify() {
    status_blink(STATUS_BUTTON, 5000);
    sendOk();
}

static void handleReboot() {
    store_saveLevelsNow(pwm_levels(), PWM_CHANNELS);
    sendOk();
    s_rebootAt = millis() + 500;
    Serial.println("[web] reboot requested");
}

static void handleNotImplemented() {
    sendError(501, "phase 2");
}

static void handleNotFound() {
    sendError(404, "not found");
}

// ---------------------------------------------------------------- lifecycle

void web_begin() {
    if (s_server) return;
    s_server = new WebServer(80);

    s_server->on("/", HTTP_GET, handlePage);
    s_server->on("/api/state", HTTP_GET, handleState);
    s_server->on("/api/status", HTTP_GET, handleStatus);
    s_server->on("/status.json", HTTP_GET, handleStatus);
    s_server->on(UriBraces("/api/channel/{}"), HTTP_POST, handleChannel);
    s_server->on("/api/names", HTTP_POST, handleNames);
    s_server->on("/api/mqtt", HTTP_POST, handleMqtt);
    s_server->on("/api/identify", HTTP_POST, handleIdentify);
    s_server->on("/api/reboot", HTTP_POST, handleReboot);
    s_server->on("/api/wifi", handleNotImplemented);
    s_server->on("/api/wifi/scan", handleNotImplemented);
    s_server->onNotFound(handleNotFound);

    s_server->begin();
    Serial.printf("[web] serving on port 80 (page %s)\n", HAVE_WEB_PAGE ? "embedded" : "MISSING");
}

void web_tick() {
    if (!s_server) return;
    s_server->handleClient();
    if (s_rebootAt && millis() > s_rebootAt) ESP.restart();
}
