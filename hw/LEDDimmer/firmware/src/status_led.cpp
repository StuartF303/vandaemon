/**
 * @file status_led.cpp
 * @brief WS2812 status LED control implementation
 */

#include "status_led.h"
#include <Adafruit_NeoPixel.h>

static Adafruit_NeoPixel statusLed(1, WS2812_PIN, NEO_GRB + NEO_KHZ800);

void status_init() {
    statusLed.begin();
    statusLed.setBrightness(50); // Moderate brightness (0-255)
    status_setColor(STATUS_OFF);
}

void status_setColor(uint32_t color) {
    statusLed.setPixelColor(0, color);
    statusLed.show();
}

void status_off() {
    status_setColor(STATUS_OFF);
}

void status_blink(uint32_t color, uint8_t times, uint16_t delayMs) {
    for (uint8_t i = 0; i < times; i++) {
        status_setColor(color);
        delay(delayMs);
        status_off();
        if (i < times - 1) {  // Don't delay after last blink
            delay(delayMs);
        }
    }
}

void status_breathe(uint32_t color, uint16_t durationMs) {
    const uint8_t steps = 50;
    const uint16_t stepDelay = durationMs / (steps * 2);

    // Extract RGB components
    uint8_t r = (color >> 16) & 0xFF;
    uint8_t g = (color >> 8) & 0xFF;
    uint8_t b = color & 0xFF;

    // Fade up
    for (uint8_t i = 0; i <= steps; i++) {
        uint8_t brightness = (i * 255) / steps;
        uint32_t fadeColor = ((r * brightness / 255) << 16) |
                             ((g * brightness / 255) << 8) |
                             (b * brightness / 255);
        status_setColor(fadeColor);
        delay(stepDelay);
    }

    // Fade down
    for (uint8_t i = steps; i > 0; i--) {
        uint8_t brightness = (i * 255) / steps;
        uint32_t fadeColor = ((r * brightness / 255) << 16) |
                             ((g * brightness / 255) << 8) |
                             (b * brightness / 255);
        status_setColor(fadeColor);
        delay(stepDelay);
    }

    status_off();
}
