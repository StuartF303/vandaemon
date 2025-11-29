/**
 * @file main.cpp
 * @brief VanDaemon LED Dimmer - Main application
 *
 * ESP32-based 8-channel PWM LED controller with MQTT integration
 * Designed for VanDaemon camper van control system
 */

#include <Arduino.h>
#include "config.h"
#include "status_led.h"
#include "button_handler.h"
#include "pwm_control.h"
#include "wifi_manager.h"
#include "mqtt_client.h"

// Application state
enum AppState {
    INIT,
    WIFI_CONNECTING,
    MQTT_CONNECTING,
    RUNNING,
    ERROR
};

static AppState appState = INIT;

/**
 * @brief Arduino setup function
 */
void setup() {
    #ifdef ENABLE_SERIAL
    Serial.begin(115200);
    delay(100);
    Serial.println("\n\n======================================");
    Serial.println("VanDaemon LED Dimmer");
    Serial.printf("Version: %s\n", FIRMWARE_VERSION);
    Serial.printf("Build: %s\n", FIRMWARE_BUILD);
    Serial.printf("Board: %s (%d channels)\n", BOARD_VARIANT, NUM_CHANNELS);
    Serial.println("======================================\n");
    #endif

    // Initialize hardware
    status_init();
    button_init();
    pwm_init();

    // Show startup sequence
    status_blink(STATUS_READY, 3, 100);

    // Initialize WiFi
    appState = WIFI_CONNECTING;
    if (!wifi_init()) {
        #ifdef ENABLE_SERIAL
        Serial.println("WiFi initialization failed!");
        #endif
        appState = ERROR;
        status_setColor(STATUS_ERROR);
        // Continue anyway - might connect later
    }

    // Initialize MQTT
    if (appState != ERROR) {
        appState = MQTT_CONNECTING;
        if (mqtt_init()) {
            mqtt_connect();  // Initial connection attempt
        } else {
            #ifdef ENABLE_SERIAL
            Serial.println("MQTT not configured, running in standalone mode");
            #endif
        }
    }

    appState = RUNNING;
    status_setColor(STATUS_READY);

    #ifdef ENABLE_SERIAL
    Serial.println("\n=== System Ready ===\n");
    Serial.println("Button 1: Fade demo");
    Serial.println("Button 2: Toggle all channels");
    Serial.println("Button 1 + 2 (hold): Reset WiFi\n");
    #endif
}

/**
 * @brief Arduino main loop
 */
void loop() {
    // Update hardware inputs
    button_update();

    // WiFi maintenance
    wifi_maintain();

    // MQTT client loop
    mqtt_loop();

    // Handle button 1 - Fade demo
    if (button_getButton1Pressed()) {
        #ifdef ENABLE_SERIAL
        Serial.println("Running fade demo...");
        #endif

        status_setColor(STATUS_ACTIVE);

        // Sequential fade demo
        for (uint8_t ch = 0; ch < NUM_CHANNELS; ch++) {
            // Fade up
            for (int val = 0; val <= 255; val += 5) {
                pwm_setChannel(ch, val);
                delay(5);
            }
            // Fade down
            for (int val = 255; val >= 0; val -= 5) {
                pwm_setChannel(ch, val);
                delay(5);
            }
        }

        // Publish updated states
        if (mqtt_isConnected()) {
            mqtt_publishAllStates();
        }

        status_setColor(STATUS_READY);

        #ifdef ENABLE_SERIAL
        Serial.println("Fade demo complete");
        #endif
    }

    // Handle button 2 - Toggle all channels
    static bool allOn = false;
    if (button_getButton2Pressed()) {
        allOn = !allOn;
        uint8_t value = allOn ? 128 : 0;  // 50% brightness or off

        pwm_setAllChannels(value);
        pwm_saveState();

        if (mqtt_isConnected()) {
            mqtt_publishAllStates();
        }

        status_setColor(allOn ? STATUS_ACTIVE : STATUS_READY);

        #ifdef ENABLE_SERIAL
        Serial.printf("All channels %s\n", allOn ? "ON" : "OFF");
        #endif
    }

    // Handle both buttons held - Reset WiFi credentials
    if (button_isButton1Down() && button_isButton2Down()) {
        static uint32_t bothButtonsStart = 0;

        if (bothButtonsStart == 0) {
            bothButtonsStart = millis();
            #ifdef ENABLE_SERIAL
            Serial.println("Hold both buttons to reset WiFi...");
            #endif
            status_blink(STATUS_ERROR, 1, 100);
        }

        // Hold for 3 seconds to reset
        if (millis() - bothButtonsStart >= 3000) {
            #ifdef ENABLE_SERIAL
            Serial.println("Resetting WiFi credentials!");
            #endif

            status_blink(STATUS_ERROR, 5, 100);
            wifi_resetCredentials();
            // Device will restart after this
        }
    } else {
        // Reset timer if buttons released
        static uint32_t bothButtonsStart = 0;
        bothButtonsStart = 0;
    }

    // Small delay to prevent tight loop
    delay(10);
}
