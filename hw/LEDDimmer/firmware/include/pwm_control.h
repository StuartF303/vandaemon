/**
 * @file pwm_control.h
 * @brief PWM channel control for LED dimming
 */

#ifndef PWM_CONTROL_H
#define PWM_CONTROL_H

#include <Arduino.h>
#include "config.h"

/**
 * @brief Initialize all PWM channels
 */
void pwm_init();

/**
 * @brief Set a single channel brightness
 * @param channel Channel number (0-indexed, 0-7)
 * @param value Brightness value (0-255)
 */
void pwm_setChannel(uint8_t channel, uint8_t value);

/**
 * @brief Set all channels to the same brightness
 * @param value Brightness value (0-255)
 */
void pwm_setAllChannels(uint8_t value);

/**
 * @brief Get current channel brightness
 * @param channel Channel number (0-indexed, 0-7)
 * @return Current brightness value (0-255)
 */
uint8_t pwm_getChannel(uint8_t channel);

/**
 * @brief Get pointer to all channel values
 * @return Pointer to channel values array
 */
const uint8_t* pwm_getAllChannels();

/**
 * @brief Set channel with smooth transition (if ENABLE_TRANSITIONS defined)
 * @param channel Channel number (0-indexed, 0-7)
 * @param targetValue Target brightness value (0-255)
 */
void pwm_setChannelSmooth(uint8_t channel, uint8_t targetValue);

/**
 * @brief Save current channel values to NVS
 */
void pwm_saveState();

/**
 * @brief Load channel values from NVS
 */
void pwm_loadState();

#endif // PWM_CONTROL_H
