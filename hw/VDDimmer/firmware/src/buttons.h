#pragma once
#include <Arduino.h>

enum ButtonEvent {
    BTN_NONE,
    BTN1_SHORT,      // toggle all PWM channels off / back to last levels
    BTN2_SHORT,      // cycle 25 / 50 / 75 / 100 % across all channels
    BTN_BOTH_LONG    // 5 s: clear WiFi credentials and reboot into the portal
};

void        buttons_begin();
ButtonEvent buttons_poll();
