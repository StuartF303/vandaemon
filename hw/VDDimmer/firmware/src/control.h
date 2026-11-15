#pragma once
#include <Arduino.h>

// The single place a PWM channel level changes. Every source -- MQTT set,
// the web API, the buttons, the serial console -- calls these, so the fan-out
// (drive the pin, stage the level for NVS, publish the MQTT state) cannot
// drift between callers. Out-of-range channels are ignored.
void ctl_setLevel(uint8_t ch, uint8_t level);
void ctl_setAll(uint8_t level);
void ctl_allOff();
