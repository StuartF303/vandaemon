/**
 * @file status_led.h
 * @brief WS2812 status LED control
 */

#ifndef STATUS_LED_H
#define STATUS_LED_H

#include <Arduino.h>
#include "config.h"

/**
 * @brief Initialize status LED
 */
void status_init();

/**
 * @brief Set status LED color
 * @param color RGB color (0xRRGGBB format)
 */
void status_setColor(uint32_t color);

/**
 * @brief Turn off status LED
 */
void status_off();

/**
 * @brief Blink status LED
 * @param color Blink color
 * @param times Number of blinks
 * @param delayMs Delay between blinks (ms)
 */
void status_blink(uint32_t color, uint8_t times, uint16_t delayMs = 200);

/**
 * @brief Status LED breathing effect
 * @param color Base color
 * @param durationMs Total duration (ms)
 */
void status_breathe(uint32_t color, uint16_t durationMs = 2000);

#endif // STATUS_LED_H
