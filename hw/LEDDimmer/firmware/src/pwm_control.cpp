/**
 * @file pwm_control.cpp
 * @brief PWM channel control implementation
 */

#include "pwm_control.h"
#include <Preferences.h>

static uint8_t channelValues[NUM_CHANNELS] = {0};
static Preferences prefs;

void pwm_init() {
    #ifdef ENABLE_SERIAL
    Serial.println("Initializing PWM channels...");
    #endif

    // Initialize all PWM channels
    for (uint8_t i = 0; i < NUM_CHANNELS; i++) {
        ledcAttach(PWM_PINS[i], PWM_FREQ, PWM_RESOLUTION);
        ledcWrite(PWM_PINS[i], 0);  // Start with all channels off

        #ifdef ENABLE_SERIAL
        Serial.printf("  CH%d -> GPIO%d\n", i + 1, PWM_PINS[i]);
        #endif
    }

    // Load saved state from NVS
    pwm_loadState();

    #ifdef ENABLE_SERIAL
    Serial.printf("PWM initialized: %d channels @ %d Hz\n", NUM_CHANNELS, PWM_FREQ);
    #endif
}

void pwm_setChannel(uint8_t channel, uint8_t value) {
    if (channel >= NUM_CHANNELS) {
        #ifdef ENABLE_SERIAL
        Serial.printf("Invalid channel: %d\n", channel);
        #endif
        return;
    }

    channelValues[channel] = value;
    ledcWrite(PWM_PINS[channel], value);

    #ifdef ENABLE_SERIAL
    Serial.printf("CH%d set to %d\n", channel + 1, value);
    #endif
}

void pwm_setAllChannels(uint8_t value) {
    for (uint8_t i = 0; i < NUM_CHANNELS; i++) {
        pwm_setChannel(i, value);
    }
}

uint8_t pwm_getChannel(uint8_t channel) {
    if (channel >= NUM_CHANNELS) {
        return 0;
    }
    return channelValues[channel];
}

const uint8_t* pwm_getAllChannels() {
    return channelValues;
}

void pwm_setChannelSmooth(uint8_t channel, uint8_t targetValue) {
    if (channel >= NUM_CHANNELS) {
        return;
    }

    #ifdef ENABLE_TRANSITIONS
    uint8_t currentValue = channelValues[channel];

    if (currentValue == targetValue) {
        return;  // Already at target
    }

    // Calculate step size
    int16_t diff = targetValue - currentValue;
    int16_t step = (diff > 0) ? 1 : -1;

    // Smooth transition
    while (currentValue != targetValue) {
        currentValue += step;
        ledcWrite(PWM_PINS[channel], currentValue);
        channelValues[channel] = currentValue;
        delay(TRANSITION_STEP_MS);
    }

    #ifdef ENABLE_SERIAL
    Serial.printf("CH%d smoothly transitioned to %d\n", channel + 1, targetValue);
    #endif

    #else
    // No transitions, just set directly
    pwm_setChannel(channel, targetValue);
    #endif
}

void pwm_saveState() {
    prefs.begin(PREFS_NAMESPACE, false);

    for (uint8_t i = 0; i < NUM_CHANNELS; i++) {
        char key[8];
        snprintf(key, sizeof(key), "%s%d", PREFS_CHANNEL_BASE, i);
        prefs.putUChar(key, channelValues[i]);
    }

    prefs.end();

    #ifdef ENABLE_SERIAL
    Serial.println("Channel states saved to NVS");
    #endif
}

void pwm_loadState() {
    prefs.begin(PREFS_NAMESPACE, true);  // Read-only

    bool stateLoaded = false;
    for (uint8_t i = 0; i < NUM_CHANNELS; i++) {
        char key[8];
        snprintf(key, sizeof(key), "%s%d", PREFS_CHANNEL_BASE, i);

        if (prefs.isKey(key)) {
            uint8_t value = prefs.getUChar(key, 0);
            pwm_setChannel(i, value);
            stateLoaded = true;
        }
    }

    prefs.end();

    #ifdef ENABLE_SERIAL
    if (stateLoaded) {
        Serial.println("Channel states loaded from NVS");
    } else {
        Serial.println("No saved channel states found");
    }
    #endif
}
