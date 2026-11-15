/*
 * LED Dimmer Controller
 * Supports 4 or 8 channel boards
 * 
 * Configuration: Set NUM_CHANNELS to 4 or 8
 */

#include <Arduino.h>
#include <Adafruit_NeoPixel.h>

// ============================================
// BOARD CONFIGURATION - CHANGE THIS
// ============================================
#define NUM_CHANNELS    8       // Set to 4 or 8

// ============================================
// PIN DEFINITIONS
// ============================================
#define WS2812_PIN      16
#define BTN1_PIN        32
#define BTN2_PIN        33

// PWM Channel GPIO mappings
// Channels 1-4 are common to both board variants
// Channels 5-8 only populated on 8-channel board
const uint8_t PWM_PINS[] = {
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
#define PWM_FREQ        5000    // 5kHz - good for most LEDs
#define PWM_RESOLUTION  8       // 8-bit (0-255)

// ============================================
// STATUS LED COLOURS
// ============================================
#define STATUS_READY    0x001000    // Dim green
#define STATUS_ACTIVE   0x000010    // Dim blue
#define STATUS_ERROR    0x100000    // Dim red
#define STATUS_BTN      0x101000    // Yellow on button press

// ============================================
// GLOBAL VARIABLES
// ============================================
Adafruit_NeoPixel statusLed(1, WS2812_PIN, NEO_GRB + NEO_KHZ800);

uint8_t channelValues[NUM_CHANNELS] = {0};
bool btn1Pressed = false;
bool btn2Pressed = false;
uint32_t lastDebounce = 0;
const uint32_t DEBOUNCE_MS = 50;

// ============================================
// FUNCTION PROTOTYPES
// ============================================
void initPWM();
void setChannel(uint8_t channel, uint8_t value);
void setAllChannels(uint8_t value);
void updateButtons();
void setStatus(uint32_t colour);
void fadeDemo();

// ============================================
// SETUP
// ============================================
void setup() {
    Serial.begin(115200);
    Serial.println();
    Serial.print("LED Dimmer - ");
    Serial.print(NUM_CHANNELS);
    Serial.println(" Channel");
    
    // Validate configuration
    #if NUM_CHANNELS != 4 && NUM_CHANNELS != 8
        #error "NUM_CHANNELS must be 4 or 8"
    #endif
    
    // Init PWM channels
    initPWM();
    
    // Init button inputs
    pinMode(BTN1_PIN, INPUT_PULLUP);
    pinMode(BTN2_PIN, INPUT_PULLUP);
    
    // Init status LED
    statusLed.begin();
    setStatus(STATUS_READY);
    
    Serial.println("Ready");
}

// ============================================
// MAIN LOOP
// ============================================
void loop() {
    updateButtons();
    
    // Example: Button 1 triggers fade demo
    if (btn1Pressed) {
        btn1Pressed = false;
        setStatus(STATUS_ACTIVE);
        fadeDemo();
        setStatus(STATUS_READY);
    }
    
    // Example: Button 2 toggles all channels
    static bool allOn = false;
    if (btn2Pressed) {
        btn2Pressed = false;
        allOn = !allOn;
        setAllChannels(allOn ? 128 : 0);
        setStatus(allOn ? STATUS_ACTIVE : STATUS_READY);
    }
    
    // Add your control logic here
    // e.g., serial commands, WiFi, MQTT, etc.
    
    delay(10);
}

// ============================================
// PWM INITIALISATION
// ============================================
void initPWM() {
    for (uint8_t i = 0; i < NUM_CHANNELS; i++) {
        // ESP32 Arduino 3.x API
        ledcAttach(PWM_PINS[i], PWM_FREQ, PWM_RESOLUTION);
        ledcWrite(PWM_PINS[i], 0);  // Start with all off
        
        Serial.print("CH");
        Serial.print(i + 1);
        Serial.print(" -> GPIO");
        Serial.println(PWM_PINS[i]);
    }
}

// ============================================
// SET SINGLE CHANNEL (0-indexed)
// ============================================
void setChannel(uint8_t channel, uint8_t value) {
    if (channel >= NUM_CHANNELS) return;
    
    channelValues[channel] = value;
    ledcWrite(PWM_PINS[channel], value);
}

// ============================================
// SET ALL CHANNELS TO SAME VALUE
// ============================================
void setAllChannels(uint8_t value) {
    for (uint8_t i = 0; i < NUM_CHANNELS; i++) {
        setChannel(i, value);
    }
}

// ============================================
// BUTTON HANDLING WITH DEBOUNCE
// ============================================
void updateButtons() {
    if (millis() - lastDebounce < DEBOUNCE_MS) return;
    
    static bool lastBtn1 = true;
    static bool lastBtn2 = true;
    
    bool currentBtn1 = digitalRead(BTN1_PIN);
    bool currentBtn2 = digitalRead(BTN2_PIN);
    
    // Detect falling edge (button press)
    if (lastBtn1 && !currentBtn1) {
        btn1Pressed = true;
        lastDebounce = millis();
        Serial.println("BTN1 pressed");
    }
    
    if (lastBtn2 && !currentBtn2) {
        btn2Pressed = true;
        lastDebounce = millis();
        Serial.println("BTN2 pressed");
    }
    
    lastBtn1 = currentBtn1;
    lastBtn2 = currentBtn2;
}

// ============================================
// STATUS LED CONTROL
// ============================================
void setStatus(uint32_t colour) {
    statusLed.setPixelColor(0, colour);
    statusLed.show();
}

// ============================================
// DEMO: SEQUENTIAL FADE
// ============================================
void fadeDemo() {
    Serial.println("Running fade demo...");
    
    // Fade each channel up and down sequentially
    for (uint8_t ch = 0; ch < NUM_CHANNELS; ch++) {
        // Fade up
        for (int val = 0; val <= 255; val += 5) {
            setChannel(ch, val);
            delay(10);
        }
        // Fade down
        for (int val = 255; val >= 0; val -= 5) {
            setChannel(ch, val);
            delay(10);
        }
    }
    
    Serial.println("Demo complete");
}

// ============================================
// OPTIONAL: SERIAL COMMAND INTERFACE
// ============================================
/*
 * Uncomment and add to loop() for serial control:
 * 
 * Format: "CHx:yyy" where x = channel (1-8), yyy = value (0-255)
 * Example: "CH1:128" sets channel 1 to 50%
 * 
 * Or: "ALL:yyy" to set all channels
 */

/*
void processSerial() {
    if (Serial.available()) {
        String cmd = Serial.readStringUntil('\n');
        cmd.trim();
        
        if (cmd.startsWith("ALL:")) {
            int val = cmd.substring(4).toInt();
            val = constrain(val, 0, 255);
            setAllChannels(val);
            Serial.print("All channels -> ");
            Serial.println(val);
        }
        else if (cmd.startsWith("CH")) {
            int colonIdx = cmd.indexOf(':');
            if (colonIdx > 2) {
                int ch = cmd.substring(2, colonIdx).toInt() - 1;  // Convert to 0-indexed
                int val = cmd.substring(colonIdx + 1).toInt();
                val = constrain(val, 0, 255);
                
                if (ch >= 0 && ch < NUM_CHANNELS) {
                    setChannel(ch, val);
                    Serial.print("CH");
                    Serial.print(ch + 1);
                    Serial.print(" -> ");
                    Serial.println(val);
                }
            }
        }
    }
}
*/
