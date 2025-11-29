/**
 * @file button_handler.cpp
 * @brief Button input handling with debounce implementation
 */

#include "button_handler.h"
#include "config.h"

static bool btn1Pressed = false;
static bool btn2Pressed = false;
static bool lastBtn1State = true;  // HIGH = not pressed (pull-up)
static bool lastBtn2State = true;
static uint32_t lastDebounceTime = 0;

void button_init() {
    pinMode(BTN1_PIN, INPUT_PULLUP);
    pinMode(BTN2_PIN, INPUT_PULLUP);

    // Read initial state
    lastBtn1State = digitalRead(BTN1_PIN);
    lastBtn2State = digitalRead(BTN2_PIN);

    #ifdef ENABLE_SERIAL
    Serial.println("Buttons initialized");
    #endif
}

void button_update() {
    // Check if enough time has passed since last debounce
    if (millis() - lastDebounceTime < DEBOUNCE_MS) {
        return;
    }

    // Read current button states
    bool currentBtn1 = digitalRead(BTN1_PIN);
    bool currentBtn2 = digitalRead(BTN2_PIN);

    // Detect falling edge (button press) for Button 1
    if (lastBtn1State == HIGH && currentBtn1 == LOW) {
        btn1Pressed = true;
        lastDebounceTime = millis();
        #ifdef ENABLE_SERIAL
        Serial.println("Button 1 pressed");
        #endif
    }

    // Detect falling edge (button press) for Button 2
    if (lastBtn2State == HIGH && currentBtn2 == LOW) {
        btn2Pressed = true;
        lastDebounceTime = millis();
        #ifdef ENABLE_SERIAL
        Serial.println("Button 2 pressed");
        #endif
    }

    // Update last states
    lastBtn1State = currentBtn1;
    lastBtn2State = currentBtn2;
}

bool button_getButton1Pressed() {
    if (btn1Pressed) {
        btn1Pressed = false;  // Clear flag
        return true;
    }
    return false;
}

bool button_getButton2Pressed() {
    if (btn2Pressed) {
        btn2Pressed = false;  // Clear flag
        return true;
    }
    return false;
}

bool button_isButton1Down() {
    return digitalRead(BTN1_PIN) == LOW;
}

bool button_isButton2Down() {
    return digitalRead(BTN2_PIN) == LOW;
}
