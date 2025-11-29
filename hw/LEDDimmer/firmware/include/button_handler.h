/**
 * @file button_handler.h
 * @brief Button input handling with debounce
 */

#ifndef BUTTON_HANDLER_H
#define BUTTON_HANDLER_H

#include <Arduino.h>

/**
 * @brief Initialize button inputs
 */
void button_init();

/**
 * @brief Update button states (call periodically in loop)
 */
void button_update();

/**
 * @brief Check if button 1 was pressed
 * @return true if button was pressed (clears flag)
 */
bool button_getButton1Pressed();

/**
 * @brief Check if button 2 was pressed
 * @return true if button was pressed (clears flag)
 */
bool button_getButton2Pressed();

/**
 * @brief Get current button 1 state
 * @return true if currently pressed
 */
bool button_isButton1Down();

/**
 * @brief Get current button 2 state
 * @return true if currently pressed
 */
bool button_isButton2Down();

#endif // BUTTON_HANDLER_H
