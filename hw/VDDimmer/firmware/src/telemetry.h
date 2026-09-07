#pragma once
#include <Arduino.h>

void telemetry_begin();
void telemetry_tick();

float telemetry_vinVolts();
float telemetry_tempC();

// The over-temperature derate currently applied to the PWM channels.
// 1.0 = no limiting. Voltage is measured and reported but never acted on --
// the leisure battery's own BMS handles under-voltage.
float telemetry_derate();
bool  telemetry_overTemp();
