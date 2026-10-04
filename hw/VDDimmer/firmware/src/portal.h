#pragma once
#include <Arduino.h>

// Brings up an open AP named VANDIMMER-<mac suffix> with a captive-portal form
// for WiFi, MQTT and strip settings. Blocks nothing -- call portal_tick() from
// loop(). Entered automatically when no WiFi credentials are stored, or on a
// five-second two-button hold.
void portal_begin();
void portal_tick();
bool portal_active();
