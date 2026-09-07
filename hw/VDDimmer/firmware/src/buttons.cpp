#include "buttons.h"
#include "board_pins.h"

static constexpr uint32_t DEBOUNCE_MS  = 50;
static constexpr uint32_t LONG_PRESS_MS = 5000;

static bool     s_last1 = true, s_last2 = true;
static uint32_t s_change1 = 0, s_change2 = 0;
static uint32_t s_bothSince = 0;
static bool     s_bothFired = false;

void buttons_begin() {
    // R43/R44 are external 10k pull-ups to +3V3, so no internal pull is needed.
    pinMode(PIN_BTN1, INPUT);
    pinMode(PIN_BTN2, INPUT);
    s_last1 = digitalRead(PIN_BTN1);
    s_last2 = digitalRead(PIN_BTN2);
}

ButtonEvent buttons_poll() {
    uint32_t now = millis();
    bool b1 = digitalRead(PIN_BTN1);   // active low
    bool b2 = digitalRead(PIN_BTN2);

    if (!b1 && !b2) {
        if (!s_bothSince) s_bothSince = now;
        if (!s_bothFired && now - s_bothSince >= LONG_PRESS_MS) {
            s_bothFired = true;
            return BTN_BOTH_LONG;
        }
        s_last1 = b1;
        s_last2 = b2;
        return BTN_NONE;   // a two-button hold must not also fire a short press
    }

    if (s_bothSince && b1 && b2) {
        s_bothSince = 0;
        bool suppress = s_bothFired;
        s_bothFired = false;
        s_last1 = b1;
        s_last2 = b2;
        if (suppress) return BTN_NONE;
    }

    ButtonEvent ev = BTN_NONE;

    if (b1 != s_last1 && now - s_change1 > DEBOUNCE_MS) {
        s_change1 = now;
        if (!b1) ev = BTN1_SHORT;      // falling edge = press
        s_last1 = b1;
    }
    if (ev == BTN_NONE && b2 != s_last2 && now - s_change2 > DEBOUNCE_MS) {
        s_change2 = now;
        if (!b2) ev = BTN2_SHORT;
        s_last2 = b2;
    }

    return ev;
}
