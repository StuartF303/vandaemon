/**
 * @file config.h
 * @brief Configuration and pin definitions for VanDaemon LED Dimmer
 *
 * Hardware: ESP32-WROOM-32
 * Channels: 4 or 8 (configured via platformio.ini)
 */

#ifndef CONFIG_H
#define CONFIG_H

#include <Arduino.h>

// ============================================
// BOARD CONFIGURATION
// ============================================
#ifndef NUM_CHANNELS
    #define NUM_CHANNELS 8  // Default to 8-channel if not defined
#endif

#ifndef BOARD_VARIANT
    #define BOARD_VARIANT "8CH"
#endif

// Validate configuration
#if NUM_CHANNELS != 4 && NUM_CHANNELS != 8
    #error "NUM_CHANNELS must be 4 or 8"
#endif

// ============================================
// HARDWARE PIN DEFINITIONS
// ============================================
#define WS2812_PIN      16  // Status LED (WS2812B addressable RGB)
#define BTN1_PIN        32  // Button 1 input (active LOW with pull-up)
#define BTN2_PIN        33  // Button 2 input (active LOW with pull-up)

// PWM Channel GPIO mappings
// Channels 1-4 are common to both board variants
// Channels 5-8 only populated on 8-channel board
const uint8_t PWM_PINS[8] = {
    25,     // CH1
    26,     // CH2
    27,     // CH3
    14,     // CH4
    4,      // CH5 (8-ch only)
    5,      // CH6 (8-ch only)
    18,     // CH7 (8-ch only)
    19      // CH8 (8-ch only)
};

// ============================================
// PWM CONFIGURATION
// ============================================
#define PWM_FREQ        5000    // 5kHz - good for most LEDs (increase to 20kHz to eliminate flicker)
#define PWM_RESOLUTION  8       // 8-bit (0-255 brightness levels)

// ============================================
// WIFI CONFIGURATION
// ============================================
#define WIFI_AP_NAME    "VanDaemon-LEDDimmer"   // Access point name for initial setup
#define WIFI_AP_PASS    "vandaemon123"          // Access point password
#define WIFI_TIMEOUT    180                     // WiFi connection timeout (seconds)

// ============================================
// MQTT CONFIGURATION
// ============================================
#define MQTT_PORT           1883
#define MQTT_KEEPALIVE      60                  // Keepalive interval (seconds)
#define MQTT_QOS            1                   // QoS level (0, 1, or 2)
#define MQTT_RETAIN         true                // Retain state messages
#define MQTT_RECONNECT_MS   5000                // Reconnect interval (milliseconds)

// MQTT Topic structure: vandaemon/leddimmer/{deviceId}/...
#define MQTT_BASE_TOPIC     "vandaemon/leddimmer"

// ============================================
// STATUS LED COLOURS (GRB format for WS2812)
// ============================================
#define STATUS_OFF          0x000000    // Off
#define STATUS_READY        0x001000    // Dim green - ready state
#define STATUS_ACTIVE       0x000010    // Dim blue - active/working
#define STATUS_ERROR        0x100000    // Dim red - error state
#define STATUS_WIFI_SETUP   0x100010    // Purple - WiFi setup mode
#define STATUS_MQTT_CONN    0x001010    // Cyan - MQTT connecting
#define STATUS_BTN          0x101000    // Yellow - button pressed

// ============================================
// TIMING CONFIGURATION
// ============================================
#define DEBOUNCE_MS         50      // Button debounce time
#define STATE_PUBLISH_MS    5000    // Publish state every 5 seconds
#define HEARTBEAT_MS        30000   // Heartbeat publish interval

// ============================================
// PERSISTENCE CONFIGURATION
// ============================================
#define PREFS_NAMESPACE     "leddimmer"     // NVS namespace
#define PREFS_DEVICE_ID     "deviceId"      // Device ID key
#define PREFS_DEVICE_NAME   "deviceName"    // Device name key
#define PREFS_MQTT_BROKER   "mqttBroker"    // MQTT broker address key
#define PREFS_MQTT_USER     "mqttUser"      // MQTT username key
#define PREFS_MQTT_PASS     "mqttPass"      // MQTT password key
#define PREFS_CHANNEL_BASE  "ch"            // Channel value prefix (ch0, ch1, etc.)

// ============================================
// OPTIONAL FEATURES
// ============================================
// #define ENABLE_OTA          // Uncomment to enable OTA updates
#define ENABLE_SERIAL       // Uncomment to enable serial debug output
#define ENABLE_TRANSITIONS  // Uncomment to enable smooth fading transitions

#ifdef ENABLE_TRANSITIONS
    #define TRANSITION_STEP_MS  20      // Transition step interval (ms)
    #define TRANSITION_STEPS    50      // Number of steps for smooth fade
#endif

// ============================================
// VERSION INFORMATION
// ============================================
#define FIRMWARE_VERSION    "1.0.0"
#define FIRMWARE_BUILD      __DATE__ " " __TIME__

#endif // CONFIG_H
