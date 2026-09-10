#include "strips.h"
#include "board_pins.h"
#include <Arduino.h>
#include <math.h>

// WS2812 output over the ESP32-S3 RMT, using the Arduino core's driver-NG API
// (rmtInit/rmtWrite in esp32-hal-rmt.h) rather than a library.
//
// This was NeoPixelBus. Its NeoEsp32Rmt*Method includes <driver/rmt.h>, the
// LEGACY RMT driver, while Arduino-ESP32 3.x drives RMT through driver-NG. IDF 5
// refuses both in one binary -- a constructor in the legacy driver logs
//   E rmt(legacy): CONFLICT! driver_ng is not allowed to be used with the
//   legacy driver
// and calls abort() before setup() runs. The board boot-looped on Rev A
// hardware, 2026-09-10. The library's I2S methods are compiled out on the S3,
// and its LcdX method panicked in strips_begin() with an MMU fault ("cache
// disabled but cached memory region accessed"), so the core's own RMT is both
// the simplest and the best-supported path.
//
// Still DMA-driven hardware, so spec section 6.4 holds: no bit-banging, no
// interrupt-disable windows, and the strips stay immune to WiFi activity.
// Three of the S3's four RMT TX channels are used, one is spare.

static constexpr uint32_t RMT_TICK_HZ = 10000000;   // 100 ns per tick
static constexpr uint8_t  BITS_PER_PIXEL = 24;

// Bit timings in 100 ns ticks. These suit WS2812B-V5, plain WS2812B and WS2815
// simultaneously, which matters because D7 on this board is a **V5/W**
// substitute (the V6 is not in JLC's catalogue). V5 caps T0H at 380 ns, so the
// commonly-quoted 400 ns would be out of spec on the very part fitted here.
//   T0H 300 ns (V5 allows 220-380)     T0L 900 ns (580-1600)
//   T1H 800 ns (580-1000)              T1L 400 ns (220-420)
static constexpr uint16_t T0H_TICKS = 3, T0L_TICKS = 9;
static constexpr uint16_t T1H_TICKS = 8, T1L_TICKS = 4;

// A 100-pixel strip is 2400 symbols, about 3 ms on the wire. Bounded rather
// than RMT_WAIT_FOR_EVER so a wedged peripheral cannot hang the main loop.
static constexpr uint32_t RMT_WRITE_TIMEOUT_MS = 100;

struct Rgb {
    uint8_t r = 0, g = 0, b = 0;
    Rgb() = default;
    Rgb(uint8_t red, uint8_t green, uint8_t blue) : r(red), g(green), b(blue) {}
};

struct RmtStrip {
    int      pin     = -1;
    uint16_t count   = 0;
    rmt_data_t *symbols = nullptr;
};

static RmtStrip s_status;
static RmtStrip s_addr[STRIP_COUNT];

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

// --- RMT plumbing ------------------------------------------------------------

static bool stripBegin(RmtStrip &strip, int pin, uint16_t count) {
    if (count == 0) return false;

    strip.symbols = (rmt_data_t *)calloc((size_t)count * BITS_PER_PIXEL, sizeof(rmt_data_t));
    if (strip.symbols == nullptr) {
        Serial.printf("[strip] symbol buffer alloc failed for pin %d (%u px)\n", pin, count);
        return false;
    }
    if (!rmtInit(pin, RMT_TX_MODE, RMT_MEM_NUM_BLOCKS_1, RMT_TICK_HZ)) {
        Serial.printf("[strip] rmtInit failed on pin %d\n", pin);
        free(strip.symbols);
        strip.symbols = nullptr;
        return false;
    }
    rmtSetEOT(pin, LOW);   // idle low, so the gap between frames is the >50 us reset

    strip.pin = pin;
    strip.count = count;
    return true;
}

static void encodeByte(rmt_data_t *dest, uint8_t value) {
    for (uint8_t bit = 0; bit < 8; bit++) {
        const bool one = value & (0x80 >> bit);   // MSB first
        dest[bit].level0    = 1;
        dest[bit].duration0 = one ? T1H_TICKS : T0H_TICKS;
        dest[bit].level1    = 0;
        dest[bit].duration1 = one ? T1L_TICKS : T0L_TICKS;
    }
}

static void stripFill(RmtStrip &strip, const Rgb &colour) {
    if (strip.symbols == nullptr) return;
    for (uint16_t px = 0; px < strip.count; px++) {
        rmt_data_t *p = strip.symbols + (size_t)px * BITS_PER_PIXEL;
        encodeByte(p,      colour.g);   // WS2812 wire order is GRB
        encodeByte(p + 8,  colour.r);
        encodeByte(p + 16, colour.b);
    }
}

static void stripShow(RmtStrip &strip) {
    if (strip.symbols == nullptr || strip.pin < 0) return;
    rmtWrite(strip.pin, strip.symbols,
             (size_t)strip.count * BITS_PER_PIXEL, RMT_WRITE_TIMEOUT_MS);
}

// --- colour ------------------------------------------------------------------

static Rgb statusRgb(StatusColour c) {
    switch (c) {
        case STATUS_BOOT:      return Rgb(12, 12, 12);
        case STATUS_PORTAL:    return Rgb(16, 0, 16);
        case STATUS_WIFI_WAIT: return Rgb(0, 0, 20);
        case STATUS_MQTT_WAIT: return Rgb(0, 14, 14);
        case STATUS_READY:     return Rgb(0, 16, 0);
        case STATUS_BUTTON:    return Rgb(16, 16, 0);
        case STATUS_OVERTEMP:  return Rgb(24, 6, 0);
        case STATUS_ERROR:     return Rgb(24, 0, 0);
    }
    return Rgb(0, 0, 0);
}

// Scales the requested colour by master brightness, then by a power cap if the
// output is running off the 5 V rail.
static void computeStripColour(uint8_t index, Rgb &out, uint16_t length) {
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

    out = Rgb((uint8_t)lroundf(r), (uint8_t)lroundf(g), (uint8_t)lroundf(b));
}

static void renderStrip(uint8_t index) {
    RmtStrip &strip = s_addr[index];
    if (strip.symbols == nullptr) return;
    Rgb colour;
    computeStripColour(index, colour, strip.count);
    stripFill(strip, colour);
    stripShow(strip);
}

// --- public ------------------------------------------------------------------

void strips_begin() {
    if (stripBegin(s_status, PIN_STATUS_DIN, 1)) {
        stripFill(s_status, statusRgb(STATUS_BOOT));
        stripShow(s_status);
    }

    // ADDR_CLK (IO48) is deliberately left unconfigured -- R11/R12 are DNP, so
    // the outputs are single-wire. See board_pins.h.

    const int pins[STRIP_COUNT] = { PIN_ADDR1_DIN, PIN_ADDR2_DIN };
    for (uint8_t i = 0; i < STRIP_COUNT; i++) {
        if (g_settings.strip[i].length == 0) continue;
        if (stripBegin(s_addr[i], pins[i], g_settings.strip[i].length)) {
            stripFill(s_addr[i], Rgb(0, 0, 0));
            stripShow(s_addr[i]);
        }
    }
    Serial.printf("[strip] status=1px addr1=%upx addr2=%upx\n",
                  s_addr[0].count, s_addr[1].count);
}

void strips_tick() {
    if (s_statusBlinkUntil && millis() > s_statusBlinkUntil) {
        s_statusBlinkUntil = 0;
        s_statusColour = s_statusRestore;
        s_lastStatusRender = 0;
    }
    if (millis() - s_lastStatusRender > 250) {
        s_lastStatusRender = millis();
        stripFill(s_status, statusRgb(s_statusColour));
        stripShow(s_status);
    }

    for (uint8_t i = 0; i < STRIP_COUNT; i++) {
        if (!s_dirty[i]) continue;
        renderStrip(i);
        s_dirty[i] = false;
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
