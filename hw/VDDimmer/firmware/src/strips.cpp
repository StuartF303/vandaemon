#include "strips.h"
#include "board_pins.h"
#include <NeoPixelBus.h>
#include <math.h>

// The ESP32-S3 has four RMT TX channels; three are used here, one is spare.
// RMT rather than bit-banging is what makes the strips immune to WiFi activity
// (see spec section 6.4) -- there are no interrupt-disable windows.
typedef NeoPixelBus<NeoGrbFeature, NeoEsp32Rmt0Ws2812xMethod> StatusBus;
typedef NeoPixelBus<NeoGrbFeature, NeoEsp32Rmt1Ws2812xMethod> Addr1Bus;
typedef NeoPixelBus<NeoGrbFeature, NeoEsp32Rmt2Ws2812xMethod> Addr2Bus;

static StatusBus *s_status = nullptr;
static Addr1Bus  *s_addr1  = nullptr;
static Addr2Bus  *s_addr2  = nullptr;

static StripState s_state[STRIP_COUNT];
static bool       s_dirty[STRIP_COUNT] = {true, true};

static StatusColour s_statusColour = STATUS_BOOT;
static StatusColour s_statusRestore = STATUS_BOOT;
static uint32_t     s_statusBlinkUntil = 0;
static uint32_t     s_lastStatusRender = 0;

// The 5 V rail is good for 2 A and the addressable outputs share it with a 2 A
// polyfuse each. Spec section 6.5 puts that at ~33 LEDs at full white. When the
// jumper is in the 5 V position the firmware holds the estimated draw under
// this, so a long strip dims rather than tripping the fuse behind a wall panel.
static constexpr uint32_t STRIP_5V_BUDGET_MA = 1800;
static constexpr float    LED_MA_PER_CHANNEL = 20.0f / 255.0f;

static RgbColor statusRgb(StatusColour c) {
    switch (c) {
        case STATUS_BOOT:      return RgbColor(12, 12, 12);
        case STATUS_PORTAL:    return RgbColor(16, 0, 16);
        case STATUS_WIFI_WAIT: return RgbColor(0, 0, 20);
        case STATUS_MQTT_WAIT: return RgbColor(0, 14, 14);
        case STATUS_READY:     return RgbColor(0, 16, 0);
        case STATUS_BUTTON:    return RgbColor(16, 16, 0);
        case STATUS_OVERTEMP:  return RgbColor(24, 6, 0);
        case STATUS_ERROR:     return RgbColor(24, 0, 0);
    }
    return RgbColor(0, 0, 0);
}

// Scales the requested colour by master brightness, then by a power cap if the
// output is running off the 5 V rail.
static void computeStripColour(uint8_t index, RgbColor &out, uint16_t length) {
    const StripState &st = s_state[index];
    float master = st.on ? (float)st.brightness / 255.0f : 0.0f;

    float r = (float)st.r * master;
    float g = (float)st.g * master;
    float b = (float)st.b * master;

    if (g_settings.strip[index].supply5v && length > 0) {
        float mA = (r + g + b) * LED_MA_PER_CHANNEL * (float)length;
        if (mA > (float)STRIP_5V_BUDGET_MA) {
            float scale = (float)STRIP_5V_BUDGET_MA / mA;
            r *= scale;
            g *= scale;
            b *= scale;
        }
    }

    out = RgbColor((uint8_t)lroundf(r), (uint8_t)lroundf(g), (uint8_t)lroundf(b));
}

template <class BusT>
static void renderStrip(BusT *bus, uint8_t index) {
    if (!bus) return;
    uint16_t len = g_settings.strip[index].length;
    RgbColor colour;
    computeStripColour(index, colour, len);
    for (uint16_t i = 0; i < len; i++) bus->SetPixelColor(i, colour);
    bus->Show();
}

void strips_begin() {
    s_status = new StatusBus(1, PIN_STATUS_DIN);
    s_status->Begin();
    status_set(STATUS_BOOT);

    // ADDR_CLK (IO48) is deliberately left unconfigured -- R11/R12 are DNP, so
    // the outputs are single-wire. See board_pins.h.

    if (g_settings.strip[0].length > 0) {
        s_addr1 = new Addr1Bus(g_settings.strip[0].length, PIN_ADDR1_DIN);
        s_addr1->Begin();
        s_addr1->ClearTo(RgbColor(0, 0, 0));
        s_addr1->Show();
    }
    if (g_settings.strip[1].length > 0) {
        s_addr2 = new Addr2Bus(g_settings.strip[1].length, PIN_ADDR2_DIN);
        s_addr2->Begin();
        s_addr2->ClearTo(RgbColor(0, 0, 0));
        s_addr2->Show();
    }
}

void strips_tick() {
    if (s_statusBlinkUntil && millis() > s_statusBlinkUntil) {
        s_statusBlinkUntil = 0;
        s_statusColour = s_statusRestore;
        s_lastStatusRender = 0;
    }
    if (millis() - s_lastStatusRender > 250) {
        s_lastStatusRender = millis();
        if (s_status && s_status->CanShow()) {
            s_status->SetPixelColor(0, statusRgb(s_statusColour));
            s_status->Show();
        }
    }

    if (s_dirty[0]) {
        if (!s_addr1 || s_addr1->CanShow()) { renderStrip(s_addr1, 0); s_dirty[0] = false; }
    }
    if (s_dirty[1]) {
        if (!s_addr2 || s_addr2->CanShow()) { renderStrip(s_addr2, 1); s_dirty[1] = false; }
    }
}

void strips_set(uint8_t index, const StripState &state) {
    if (index >= STRIP_COUNT) return;
    s_state[index] = state;
    s_dirty[index] = true;
}

const StripState &strips_get(uint8_t index) {
    static StripState empty;
    return index < STRIP_COUNT ? s_state[index] : empty;
}

void strips_setBrightness(uint8_t index, uint8_t brightness) {
    if (index >= STRIP_COUNT) return;
    s_state[index].brightness = brightness;
    s_state[index].on = brightness > 0;
    s_dirty[index] = true;
}

void status_set(StatusColour colour) {
    // net_tick() calls this every loop, so ignore no-op changes rather than
    // re-driving the pixel thousands of times a second.
    if (s_statusBlinkUntil) { s_statusRestore = colour; return; }
    if (colour == s_statusColour) return;
    s_statusColour = colour;
    s_statusRestore = colour;
    s_lastStatusRender = 0;
}

void status_blink(StatusColour colour, uint16_t durationMs) {
    s_statusRestore = s_statusColour;
    s_statusColour = colour;
    s_statusBlinkUntil = millis() + durationMs;
    s_lastStatusRender = 0;
}
