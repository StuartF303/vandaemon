// =============================================================================
// VANDIMMER-4CH+2A firmware
//
// Board: hw/VDDimmer, Rev A, git tag `ordered-revA` (37c12ab).
// Pin map: src/board_pins.h -- netlist-derived, do not trust any other source.
// MQTT contract: src/net.cpp -- matches VanDaemon's MqttLedDimmerPlugin.
//
// The board has NO USB power path. It must have 12 V applied to run, flash or
// enumerate; USB-C carries data and ESD protection only.
// =============================================================================

#include <Arduino.h>
#include "board_pins.h"
#include "settings.h"
#include "store.h"
#include "pwm.h"
#include "strips.h"
#include "buttons.h"
#include "telemetry.h"
#include "portal.h"
#include "net.h"

// Levels to come back to when BTN1 toggles everything back on.
static uint8_t s_lastOnLevels[PWM_CHANNELS] = {0};
static bool    s_allOff = true;

static const uint8_t PRESETS[] = {64, 128, 191, 255};
static uint8_t s_presetIndex = 0;

static void captureLevels() {
    bool any = false;
    for (uint8_t i = 0; i < PWM_CHANNELS; i++) {
        s_lastOnLevels[i] = pwm_get(i);
        if (s_lastOnLevels[i] > 0) any = true;
    }
    if (!any) {
        for (uint8_t i = 0; i < PWM_CHANNELS; i++) s_lastOnLevels[i] = 255;
    }
}

static void persistAndPublish() {
    store_stageLevels(pwm_levels(), PWM_CHANNELS);
    store_markLevelsDirty();
    for (uint8_t i = 0; i < PWM_CHANNELS; i++) net_publishChannelState(i);
}

static void handleButton(ButtonEvent ev) {
    switch (ev) {
        case BTN1_SHORT:
            status_blink(STATUS_BUTTON, 400);
            if (s_allOff) {
                for (uint8_t i = 0; i < PWM_CHANNELS; i++) pwm_set(i, s_lastOnLevels[i]);
                s_allOff = false;
            } else {
                captureLevels();
                pwm_allOff();
                s_allOff = true;
            }
            persistAndPublish();
            break;

        case BTN2_SHORT:
            status_blink(STATUS_BUTTON, 400);
            pwm_setAll(PRESETS[s_presetIndex]);
            s_presetIndex = (s_presetIndex + 1) % (sizeof(PRESETS) / sizeof(PRESETS[0]));
            s_allOff = false;
            persistAndPublish();
            break;

        case BTN_BOTH_LONG:
            Serial.println("[btn] both held: clearing WiFi credentials");
            status_set(STATUS_PORTAL);
            store_saveLevelsNow(pwm_levels(), PWM_CHANNELS);
            net_forgetWifiAndReboot();
            break;

        default:
            break;
    }
}

void setup() {
    // FIRST, before anything else. U4's outputs follow their inputs at all times
    // (its /OE pins are strapped to GND), and those inputs float while the ESP32
    // is in reset -- so the channels are only definitively off once this returns.
    pwm_preinit();

    Serial.begin(115200);
    delay(50);
    Serial.printf("\nVANDIMMER-4CH+2A  fw %s  variant %s\n", FW_VERSION, FW_VARIANT);
    Serial.printf("[build] %s\n", BUILD_ID);

    store_begin();
    Serial.printf("[cfg] deviceId=%s name='%s' channels=%u\n",
                  g_settings.deviceId, g_settings.deviceName,
                  (unsigned)(g_settings.exposeStripsAsChannels
                                 ? PWM_CHANNELS + STRIP_COUNT
                                 : PWM_CHANNELS));

    pwm_begin(g_settings.pwmFreqHz, g_settings.gammaCorrect);
    Serial.printf("[pwm] %u Hz, 12-bit, gamma %s\n",
                  (unsigned)g_settings.pwmFreqHz,
                  g_settings.gammaCorrect ? "on" : "off");

    strips_begin();
    buttons_begin();
    telemetry_begin();

    if (g_settings.restoreOnBoot) {
        uint8_t levels[PWM_CHANNELS];
        store_loadLevels(levels, PWM_CHANNELS);
        for (uint8_t i = 0; i < PWM_CHANNELS; i++) {
            pwm_set(i, levels[i]);
            if (levels[i] > 0) s_allOff = false;
        }
        for (uint8_t i = 0; i < STRIP_COUNT; i++) {
            uint8_t brightness;
            bool on;
            uint32_t rgb = store_loadStripColour(i, brightness, on);
            StripState st;
            st.r = (rgb >> 16) & 0xFF;
            st.g = (rgb >> 8) & 0xFF;
            st.b = rgb & 0xFF;
            st.brightness = brightness;
            st.on = on;
            strips_set(i, st);
        }
    }
    captureLevels();

    Serial.printf("[adc] vin %.2f V, board %.1f C\n",
                  telemetry_vinVolts(), telemetry_tempC());

    net_begin();
}

// ---------------------------------------------------------------------------
// Serial console
//
// The board is MQTT-only once configured, so with no WiFi credentials there is
// no way to drive it at all -- and on Rev A there is no way back into the
// portal either, because BTN1 is the unrouted J11.1 pad and `forget-wifi`
// needs the broker. USB is present on the bench regardless, so expose the few
// commands that matter over it.
//
//   all <0-255>        every PWM channel
//   ch <0-3> <0-255>   one channel
//   off                all channels off
//   bright <0-255>     status LED brightness
//   portal             clear WiFi credentials and reboot into the portal
//   ?                  this list
// ---------------------------------------------------------------------------
static void serialConsole() {
    static char buf[48];
    static uint8_t len = 0;

    while (Serial.available()) {
        char c = (char)Serial.read();
        if (c == '\r') continue;
        if (c != '\n') {
            if (len < sizeof(buf) - 1) buf[len++] = c;
            continue;
        }
        buf[len] = '\0';
        len = 0;
        if (buf[0] == '\0') continue;

        unsigned a = 0, b = 0;
        if (!strcmp(buf, "?")) {
            Serial.println("[con] all <0-255> | ch <0-3> <0-255> | off | "
                           "bright <0-255> | portal");
        } else if (sscanf(buf, "all %u", &a) == 1) {
            pwm_setAll((uint8_t)(a > 255 ? 255 : a));
            s_allOff = false;
            persistAndPublish();
            Serial.printf("[con] all channels = %u\n", a > 255 ? 255 : a);
        } else if (sscanf(buf, "ch %u %u", &a, &b) == 2 && a < PWM_CHANNELS) {
            pwm_set((uint8_t)a, (uint8_t)(b > 255 ? 255 : b));
            s_allOff = false;
            persistAndPublish();
            Serial.printf("[con] channel %u = %u\n", a, b > 255 ? 255 : b);
        } else if (!strcmp(buf, "off")) {
            pwm_allOff();
            s_allOff = true;
            persistAndPublish();
            Serial.println("[con] all channels off");
        } else if (sscanf(buf, "bright %u", &a) == 1) {
            g_settings.statusBrightness = (uint8_t)(a > 255 ? 255 : a);
            store_saveSettings();
            Serial.printf("[con] status brightness = %u\n",
                          g_settings.statusBrightness);
        } else if (!strcmp(buf, "portal")) {
            Serial.println("[con] clearing WiFi credentials, rebooting");
            delay(100);
            net_forgetWifiAndReboot();
        } else {
            Serial.printf("[con] ? unknown: '%s'\n", buf);
        }
    }
}

void loop() {
    serialConsole();
    portal_tick();
    handleButton(buttons_poll());
    telemetry_tick();
    net_tick();
    strips_tick();
    store_tick();
    delay(2);
}
