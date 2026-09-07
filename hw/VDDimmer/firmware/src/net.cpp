#include "net.h"
#include "settings.h"
#include "store.h"
#include "pwm.h"
#include "strips.h"
#include "telemetry.h"
#include "portal.h"

#include <WiFi.h>
#include <ArduinoOTA.h>
#include <PubSubClient.h>
#include <ArduinoJson.h>
#include <math.h>

// =============================================================================
// MQTT contract
// =============================================================================
// This matches VanDaemon's MqttLedDimmerPlugin as it is actually implemented,
// not as the old 8-channel README describes it. Four details are load-bearing:
//
//  1. The `config` message is MANDATORY. If the plugin only ever sees
//     status=online it records the device with Channels = 0, and
//     MqttLedDimmerService loops `channel < Channels` -- zero controls are
//     registered and nothing appears in the UI.
//
//  2. `config` and `status` must be RETAINED. The plugin subscribes when it
//     connects; a backend that starts after the board would otherwise never
//     hear the announcement.
//
//  3. `channel/{N}/state` must be a BARE INTEGER 0-255. The handler is
//     int.TryParse, so JSON there is silently dropped.
//
//  4. Channel numbers on the wire are 0-BASED. channel/0 is physical channel 1.
//
// One further hazard: HandleConfigMessage REPLACES the plugin's device record,
// which wipes its cached channel states. Retained /state messages that arrive
// before /config are therefore discarded. Everything is republished right after
// config, and again with every heartbeat, so the backend converges regardless
// of the order the broker delivers retained messages in.
// =============================================================================

static constexpr uint32_t WIFI_TIMEOUT_MS     = 20000;
static constexpr uint32_t MQTT_RETRY_MS       = 5000;
static constexpr uint32_t HEARTBEAT_MS        = 60000;
static constexpr uint16_t MQTT_BUFFER_BYTES   = 512;

static WiFiClient   s_wifiClient;
static PubSubClient s_mqtt(s_wifiClient);

static char s_prefix[128] = {0};     // "{base}/{deviceId}"
static bool s_wifiUp = false;
static uint32_t s_lastMqttTry = 0;
static uint32_t s_lastHeartbeat = 0;
static uint32_t s_wifiRetryAt = 0;

uint8_t net_declaredChannels() {
    return g_settings.exposeStripsAsChannels ? (PWM_CHANNELS + STRIP_COUNT)
                                             : PWM_CHANNELS;
}

static void topic(char *dst, size_t len, const char *suffix) {
    snprintf(dst, len, "%s/%s", s_prefix, suffix);
}

// -----------------------------------------------------------------------------
// Publishing
// -----------------------------------------------------------------------------

static void publishConfig() {
    JsonDocument doc;
    doc["deviceId"]   = g_settings.deviceId;
    doc["deviceName"] = g_settings.deviceName;
    doc["channels"]   = net_declaredChannels();
    doc["version"]    = FW_VERSION;
    doc["variant"]    = FW_VARIANT;

    char payload[256];
    size_t n = serializeJson(doc, payload, sizeof(payload));

    char t[160];
    topic(t, sizeof(t), "config");
    s_mqtt.publish(t, (const uint8_t *)payload, n, true);
}

void net_publishChannelState(uint8_t channel) {
    if (!s_mqtt.connected()) return;
    if (channel >= net_declaredChannels()) return;

    uint8_t value;
    if (channel < PWM_CHANNELS) {
        value = pwm_get(channel);
    } else {
        const StripState &st = strips_get(channel - PWM_CHANNELS);
        value = st.on ? st.brightness : 0;
    }

    char t[160], payload[8];
    snprintf(t, sizeof(t), "%s/channel/%u/state", s_prefix, (unsigned)channel);
    // Bare integer, not JSON -- see note 3 above.
    snprintf(payload, sizeof(payload), "%u", (unsigned)value);
    s_mqtt.publish(t, payload, true);
}

void net_publishStripState(uint8_t index) {
    if (!s_mqtt.connected() || index >= STRIP_COUNT) return;
    const StripState &st = strips_get(index);

    JsonDocument doc;
    doc["on"]         = st.on;
    doc["brightness"] = st.brightness;
    JsonArray rgb = doc["rgb"].to<JsonArray>();
    rgb.add(st.r);
    rgb.add(st.g);
    rgb.add(st.b);
    doc["length"]   = g_settings.strip[index].length;
    doc["supply5v"] = g_settings.strip[index].supply5v;

    char payload[192];
    size_t n = serializeJson(doc, payload, sizeof(payload));

    char t[160];
    snprintf(t, sizeof(t), "%s/addr/%u/state", s_prefix, (unsigned)(index + 1));
    s_mqtt.publish(t, (const uint8_t *)payload, n, true);
}

void net_publishAllStates() {
    for (uint8_t i = 0; i < net_declaredChannels(); i++) net_publishChannelState(i);
    for (uint8_t i = 0; i < STRIP_COUNT; i++) net_publishStripState(i);
}

static void publishHeartbeat() {
    JsonDocument doc;
    doc["uptime"]   = (uint32_t)(millis() / 1000);
    doc["freeHeap"] = (uint32_t)ESP.getFreeHeap();
    doc["rssi"]     = WiFi.RSSI();

    char payload[128];
    size_t n = serializeJson(doc, payload, sizeof(payload));

    char t[160];
    topic(t, sizeof(t), "heartbeat");
    s_mqtt.publish(t, (const uint8_t *)payload, n, false);
}

static void publishTelemetry() {
    JsonDocument doc;
    doc["vin"]      = roundf(telemetry_vinVolts() * 100.0f) / 100.0f;
    doc["tempC"]    = roundf(telemetry_tempC() * 10.0f) / 10.0f;
    doc["derate"]   = telemetry_derate();
    doc["overTemp"] = telemetry_overTemp();

    char payload[160];
    size_t n = serializeJson(doc, payload, sizeof(payload));

    char t[160];
    topic(t, sizeof(t), "telemetry");
    s_mqtt.publish(t, (const uint8_t *)payload, n, true);
}

// -----------------------------------------------------------------------------
// Inbound
// -----------------------------------------------------------------------------

// The backend sends a bare integer; the old README also documented
// {"brightness":N}. Accept both, reject anything else.
static bool parseBrightness(const char *body, uint8_t &out) {
    while (*body == ' ' || *body == '\t') body++;

    long v;
    if (*body == '{') {
        JsonDocument doc;
        if (deserializeJson(doc, body) != DeserializationError::Ok) return false;
        if (doc["brightness"].isNull()) return false;
        v = doc["brightness"].as<long>();
    } else {
        char *end = nullptr;
        v = strtol(body, &end, 10);
        if (end == body) return false;
    }

    if (v < 0) v = 0;
    if (v > 255) v = 255;
    out = (uint8_t)v;
    return true;
}

static void handleChannelSet(uint8_t channel, const char *body) {
    uint8_t value;
    if (!parseBrightness(body, value)) {
        Serial.printf("[mqtt] channel %u: unparsable payload '%s'\n",
                      (unsigned)channel, body);
        return;
    }

    if (channel < PWM_CHANNELS) {
        pwm_set(channel, value);
        store_stageLevels(pwm_levels(), PWM_CHANNELS);
        store_markLevelsDirty();
    } else if (g_settings.exposeStripsAsChannels &&
               channel < PWM_CHANNELS + STRIP_COUNT) {
        uint8_t index = channel - PWM_CHANNELS;
        strips_setBrightness(index, value);
        const StripState &st = strips_get(index);
        store_saveStripColour(index,
                              ((uint32_t)st.r << 16) | ((uint32_t)st.g << 8) | st.b,
                              st.brightness, st.on);
        net_publishStripState(index);
    } else {
        return;
    }

    net_publishChannelState(channel);
}

static void handleStripSet(uint8_t index, const char *body) {
    if (index >= STRIP_COUNT) return;

    JsonDocument doc;
    if (deserializeJson(doc, body) != DeserializationError::Ok) {
        // Tolerate a bare integer here too, treating it as master brightness.
        uint8_t value;
        if (!parseBrightness(body, value)) return;
        strips_setBrightness(index, value);
    } else {
        StripState st = strips_get(index);
        if (!doc["brightness"].isNull()) {
            long b = doc["brightness"].as<long>();
            st.brightness = (uint8_t)constrain(b, 0L, 255L);
        }
        if (!doc["rgb"].isNull() && doc["rgb"].is<JsonArray>()) {
            JsonArray a = doc["rgb"].as<JsonArray>();
            if (a.size() >= 3) {
                st.r = (uint8_t)constrain(a[0].as<long>(), 0L, 255L);
                st.g = (uint8_t)constrain(a[1].as<long>(), 0L, 255L);
                st.b = (uint8_t)constrain(a[2].as<long>(), 0L, 255L);
            }
        }
        st.on = doc["on"].isNull() ? (st.brightness > 0) : doc["on"].as<bool>();
        strips_set(index, st);
    }

    const StripState &st = strips_get(index);
    store_saveStripColour(index,
                          ((uint32_t)st.r << 16) | ((uint32_t)st.g << 8) | st.b,
                          st.brightness, st.on);

    net_publishStripState(index);
    if (g_settings.exposeStripsAsChannels) {
        net_publishChannelState(PWM_CHANNELS + index);
    }
}

static void handleCommand(const char *body) {
    if (!strcmp(body, "reboot")) {
        Serial.println("[mqtt] reboot requested");
        delay(200);
        ESP.restart();
    } else if (!strcmp(body, "identify")) {
        status_blink(STATUS_BUTTON, 3000);
    } else if (!strcmp(body, "republish")) {
        publishConfig();
        net_publishAllStates();
    } else if (!strcmp(body, "forget-wifi")) {
        net_forgetWifiAndReboot();
    }
}

static void onMessage(char *topicIn, byte *payload, unsigned int length) {
    char body[256];
    unsigned int n = length < sizeof(body) - 1 ? length : sizeof(body) - 1;
    memcpy(body, payload, n);
    body[n] = '\0';

    size_t plen = strlen(s_prefix);
    if (strncmp(topicIn, s_prefix, plen) != 0 || topicIn[plen] != '/') return;
    const char *rest = topicIn + plen + 1;

    unsigned index = 0;
    if (sscanf(rest, "channel/%u/set", &index) == 1) {
        handleChannelSet((uint8_t)index, body);
    } else if (sscanf(rest, "addr/%u/set", &index) == 1 && index >= 1) {
        handleStripSet((uint8_t)(index - 1), body);   // addr topics are 1-based
    } else if (!strcmp(rest, "cmd")) {
        handleCommand(body);
    }
}

// -----------------------------------------------------------------------------
// Connection management
// -----------------------------------------------------------------------------

static bool mqttConnect() {
    if (g_settings.mqttHost[0] == '\0') return false;

    char willTopic[160];
    topic(willTopic, sizeof(willTopic), "status");

    const char *user = g_settings.mqttUser[0] ? g_settings.mqttUser : nullptr;
    const char *pass = g_settings.mqttPass[0] ? g_settings.mqttPass : nullptr;

    // Last will: a board that drops off the network marks itself offline.
    bool ok = s_mqtt.connect(g_settings.deviceId, user, pass,
                             willTopic, 1, true, "offline", true);
    if (!ok) {
        Serial.printf("[mqtt] connect failed, state %d\n", s_mqtt.state());
        return false;
    }

    Serial.printf("[mqtt] connected to %s:%u as %s\n",
                  g_settings.mqttHost, g_settings.mqttPort, g_settings.deviceId);

    s_mqtt.publish(willTopic, "online", true);
    publishConfig();
    net_publishAllStates();
    publishTelemetry();

    char sub[160];
    snprintf(sub, sizeof(sub), "%s/channel/+/set", s_prefix);
    s_mqtt.subscribe(sub, 1);              // the backend publishes at QoS 1
    snprintf(sub, sizeof(sub), "%s/addr/+/set", s_prefix);
    s_mqtt.subscribe(sub, 1);
    snprintf(sub, sizeof(sub), "%s/cmd", s_prefix);
    s_mqtt.subscribe(sub, 1);

    status_set(STATUS_READY);
    s_lastHeartbeat = millis();
    return true;
}

static void wifiConnect() {
    WiFi.mode(WIFI_STA);
    WiFi.setHostname(g_settings.deviceId);
    WiFi.setSleep(false);            // MQTT latency matters more than the ~30 mA
    WiFi.begin(g_settings.wifiSsid, g_settings.wifiPass);

    status_set(STATUS_WIFI_WAIT);
    Serial.printf("[wifi] connecting to '%s'\n", g_settings.wifiSsid);

    uint32_t start = millis();
    while (WiFi.status() != WL_CONNECTED && millis() - start < WIFI_TIMEOUT_MS) {
        strips_tick();
        delay(50);
    }

    s_wifiUp = (WiFi.status() == WL_CONNECTED);
    if (s_wifiUp) {
        Serial.printf("[wifi] up, %s rssi %d\n",
                      WiFi.localIP().toString().c_str(), (int)WiFi.RSSI());
        status_set(STATUS_MQTT_WAIT);
    } else {
        Serial.println("[wifi] failed");
        status_set(STATUS_ERROR);
    }
}

void net_begin() {
    snprintf(s_prefix, sizeof(s_prefix), "%s/%s",
             g_settings.baseTopic, g_settings.deviceId);

    if (g_settings.wifiSsid[0] == '\0') {
        Serial.println("[net] no WiFi credentials, starting portal");
        portal_begin();
        return;
    }

    wifiConnect();
    if (!s_wifiUp) {
        // Credentials exist but the network is out of range -- keep retrying
        // rather than dropping into the portal, since the van may simply be
        // parked away from home. Two-button hold forces the portal.
        s_wifiRetryAt = millis() + 30000;
        return;
    }

    ArduinoOTA.setHostname(g_settings.deviceId);
    ArduinoOTA.begin();

    s_mqtt.setServer(g_settings.mqttHost, g_settings.mqttPort);
    s_mqtt.setBufferSize(MQTT_BUFFER_BYTES);
    s_mqtt.setCallback(onMessage);
    s_mqtt.setKeepAlive(30);
    mqttConnect();
}

void net_tick() {
    if (portal_active()) return;

    if (WiFi.status() != WL_CONNECTED) {
        if (s_wifiUp) {
            Serial.println("[wifi] lost");
            s_wifiUp = false;
            status_set(STATUS_ERROR);
            s_wifiRetryAt = millis() + 5000;
        }
        if (s_wifiRetryAt && millis() > s_wifiRetryAt) {
            s_wifiRetryAt = 0;
            net_begin();
        }
        return;
    }

    ArduinoOTA.handle();

    if (!s_mqtt.connected()) {
        if (millis() - s_lastMqttTry > MQTT_RETRY_MS) {
            s_lastMqttTry = millis();
            status_set(STATUS_MQTT_WAIT);
            mqttConnect();
        }
        return;
    }

    s_mqtt.loop();

    if (millis() - s_lastHeartbeat > HEARTBEAT_MS) {
        s_lastHeartbeat = millis();
        publishHeartbeat();
        publishTelemetry();
        // Cheap insurance against the plugin having dropped cached states when
        // it re-read our config message. See the note at the top of this file.
        net_publishAllStates();
    }

    if (telemetry_overTemp()) {
        status_set(STATUS_OVERTEMP);
    } else if (s_mqtt.connected()) {
        status_set(STATUS_READY);
    }
}

bool net_wifiUp() { return s_wifiUp; }
bool net_mqttUp() { return s_mqtt.connected(); }

void net_forgetWifiAndReboot() {
    store_clearWifi();
    delay(200);
    ESP.restart();
}
