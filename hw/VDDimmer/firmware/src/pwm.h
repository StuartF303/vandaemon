#pragma once
#include "board_pins.h"

// MUST be the first call in setup(). Drives every gate input LOW and latches
// the pad, closing the window in which a floating input to the permanently
// enabled 74HCT125 could turn a channel on. See the SAFETY note in board_pins.h.
void pwm_preinit();

void pwm_begin(uint16_t freqHz, bool gammaCorrect);

// value is the 0-255 wire value from MQTT, not a duty cycle.
void    pwm_set(uint8_t channel, uint8_t value);
uint8_t pwm_get(uint8_t channel);
const uint8_t *pwm_levels();

void pwm_setAll(uint8_t value);
void pwm_allOff();

// 1.0 = full output, 0.0 = off. Applied on top of every channel by the
// over-temperature protection in telemetry.cpp.
void  pwm_setDerate(float factor);
float pwm_derate();
