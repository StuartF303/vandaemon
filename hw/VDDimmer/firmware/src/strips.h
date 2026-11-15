#pragma once
#include "settings.h"

struct StripState {
    bool    on         = false;
    uint8_t brightness = 0;      // master, 0-255
    uint8_t r = 255, g = 255, b = 255;
};

// Status pixel colours (D7 on the board, one WS2812B).
enum StatusColour {
    STATUS_BOOT,
    STATUS_PORTAL,
    STATUS_WIFI_WAIT,
    STATUS_MQTT_WAIT,
    STATUS_READY,
    STATUS_BUTTON,
    STATUS_OVERTEMP,
    STATUS_ERROR
};

void strips_begin();
void strips_tick();

void              strips_set(uint8_t index, const StripState &state);
const StripState &strips_get(uint8_t index);

// CH4/CH5 when exposeStripsAsChannels is on: master brightness only, the
// colour is whatever the strip was last told.
void strips_setBrightness(uint8_t index, uint8_t brightness);

void status_set(StatusColour colour);
void status_blink(StatusColour colour, uint16_t durationMs);
