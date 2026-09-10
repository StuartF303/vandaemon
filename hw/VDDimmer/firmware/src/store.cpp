#include "store.h"
#include <Preferences.h>
#include <esp_mac.h>

Settings g_settings;

static Preferences s_prefs;
static char        s_macSuffix[8] = {0};
static bool        s_levelsDirty  = false;
static uint32_t    s_lastFlush    = 0;
static uint8_t     s_pendingPwm[8] = {0};
static uint8_t     s_pendingCount  = 0;

static constexpr char NS[] = "vandimmer";
static constexpr uint32_t FLUSH_INTERVAL_MS = 5000;

const char *store_macSuffix() { return s_macSuffix; }

static void computeMacSuffix() {
    uint8_t mac[6] = {0};
    // Derived from the factory MAC rather than stored, so an NVS erase does not
    // change deviceId and orphan the controls VanDaemon has already persisted.
    //
    // Read the eFuse directly. WiFi.macAddress() returns all zeros until the
    // WiFi driver has started, and store_begin() runs long before net_begin() --
    // which made every board call itself vandimmer-000000 and collide on MQTT
    // topics, hostname and OTA. Observed on the first Rev A board, 2026-09-10.
    esp_efuse_mac_get_default(mac);
    snprintf(s_macSuffix, sizeof(s_macSuffix), "%02x%02x%02x", mac[3], mac[4], mac[5]);
}

static void getStr(const char *key, char *dst, size_t len, const char *fallback) {
    String v = s_prefs.getString(key, fallback);
    strlcpy(dst, v.c_str(), len);
}

void store_begin() {
    computeMacSuffix();
    s_prefs.begin(NS, false);

    char defId[32], defName[40];
    snprintf(defId,   sizeof(defId),   "vandimmer-%s", s_macSuffix);
    snprintf(defName, sizeof(defName), "VANDIMMER %s", s_macSuffix);

    getStr("devid",   g_settings.deviceId,   sizeof(g_settings.deviceId),   defId);
    getStr("devname", g_settings.deviceName, sizeof(g_settings.deviceName), defName);
    getStr("ssid",    g_settings.wifiSsid,   sizeof(g_settings.wifiSsid),   "");
    getStr("wpass",   g_settings.wifiPass,   sizeof(g_settings.wifiPass),   "");
    getStr("mqhost",  g_settings.mqttHost,   sizeof(g_settings.mqttHost),   "");
    getStr("mquser",  g_settings.mqttUser,   sizeof(g_settings.mqttUser),   "");
    getStr("mqpass",  g_settings.mqttPass,   sizeof(g_settings.mqttPass),   "");
    getStr("base",    g_settings.baseTopic,  sizeof(g_settings.baseTopic),  "vandaemon/leddimmer");

    g_settings.mqttPort = s_prefs.getUShort("mqport", 1883);

    for (uint8_t i = 0; i < STRIP_COUNT; i++) {
        char k[12];
        snprintf(k, sizeof(k), "s%ulen", (unsigned)i);
        g_settings.strip[i].length = s_prefs.getUShort(k, 30);
        if (g_settings.strip[i].length > STRIP_MAX_PIXELS) {
            g_settings.strip[i].length = STRIP_MAX_PIXELS;
        }
        snprintf(k, sizeof(k), "s%usup5", (unsigned)i);
        g_settings.strip[i].supply5v = s_prefs.getBool(k, true);
    }

    g_settings.exposeStripsAsChannels = s_prefs.getBool("expstrip", true);
    g_settings.restoreOnBoot          = s_prefs.getBool("restore", true);
    g_settings.pwmFreqHz              = s_prefs.getUShort("pwmfreq", 1200);
    g_settings.gammaCorrect           = s_prefs.getBool("gamma", true);

    // A malformed frequency would silently produce a board that flickers, so
    // clamp to the range the spec allows rather than trusting NVS.
    if (g_settings.pwmFreqHz < 200)   g_settings.pwmFreqHz = 200;
    if (g_settings.pwmFreqHz > 20000) g_settings.pwmFreqHz = 20000;
}

void store_saveSettings() {
    s_prefs.putString("devid",   g_settings.deviceId);
    s_prefs.putString("devname", g_settings.deviceName);
    s_prefs.putString("ssid",    g_settings.wifiSsid);
    s_prefs.putString("wpass",   g_settings.wifiPass);
    s_prefs.putString("mqhost",  g_settings.mqttHost);
    s_prefs.putString("mquser",  g_settings.mqttUser);
    s_prefs.putString("mqpass",  g_settings.mqttPass);
    s_prefs.putString("base",    g_settings.baseTopic);
    s_prefs.putUShort("mqport",  g_settings.mqttPort);

    for (uint8_t i = 0; i < STRIP_COUNT; i++) {
        char k[12];
        snprintf(k, sizeof(k), "s%ulen", (unsigned)i);
        s_prefs.putUShort(k, g_settings.strip[i].length);
        snprintf(k, sizeof(k), "s%usup5", (unsigned)i);
        s_prefs.putBool(k, g_settings.strip[i].supply5v);
    }

    s_prefs.putBool("expstrip", g_settings.exposeStripsAsChannels);
    s_prefs.putBool("restore",  g_settings.restoreOnBoot);
    s_prefs.putUShort("pwmfreq", g_settings.pwmFreqHz);
    s_prefs.putBool("gamma",    g_settings.gammaCorrect);
}

void store_clearWifi() {
    s_prefs.remove("ssid");
    s_prefs.remove("wpass");
}

void store_factoryReset() {
    s_prefs.clear();
}

void store_loadLevels(uint8_t *pwm, uint8_t pwmCount) {
    for (uint8_t i = 0; i < pwmCount; i++) {
        char k[8];
        snprintf(k, sizeof(k), "ch%u", (unsigned)i);
        pwm[i] = s_prefs.getUChar(k, 0);
    }
}

void store_saveLevelsNow(const uint8_t *pwm, uint8_t pwmCount) {
    for (uint8_t i = 0; i < pwmCount && i < sizeof(s_pendingPwm); i++) {
        char k[8];
        snprintf(k, sizeof(k), "ch%u", (unsigned)i);
        if (s_prefs.getUChar(k, 0xFF) != pwm[i]) {
            s_prefs.putUChar(k, pwm[i]);
        }
    }
    s_levelsDirty = false;
}

void store_markLevelsDirty() {
    s_levelsDirty = true;
}

// main.cpp hands the current levels over through this shim so store_tick() can
// stay ignorant of the pwm module.
void store_stageLevels(const uint8_t *pwm, uint8_t pwmCount) {
    s_pendingCount = pwmCount < sizeof(s_pendingPwm) ? pwmCount : sizeof(s_pendingPwm);
    memcpy(s_pendingPwm, pwm, s_pendingCount);
}

void store_tick() {
    if (!s_levelsDirty) return;
    if (millis() - s_lastFlush < FLUSH_INTERVAL_MS) return;
    s_lastFlush = millis();
    store_saveLevelsNow(s_pendingPwm, s_pendingCount);
}

uint32_t store_loadStripColour(uint8_t index, uint8_t &brightness, bool &on) {
    char k[12];
    snprintf(k, sizeof(k), "s%urgb", (unsigned)index);
    uint32_t rgb = s_prefs.getUInt(k, 0xFFFFFF);
    snprintf(k, sizeof(k), "s%ubri", (unsigned)index);
    brightness = s_prefs.getUChar(k, 0);
    snprintf(k, sizeof(k), "s%uon", (unsigned)index);
    on = s_prefs.getBool(k, false);
    return rgb;
}

void store_saveStripColour(uint8_t index, uint32_t rgb, uint8_t brightness, bool on) {
    char k[12];
    snprintf(k, sizeof(k), "s%urgb", (unsigned)index);
    s_prefs.putUInt(k, rgb);
    snprintf(k, sizeof(k), "s%ubri", (unsigned)index);
    s_prefs.putUChar(k, brightness);
    snprintf(k, sizeof(k), "s%uon", (unsigned)index);
    s_prefs.putBool(k, on);
}
