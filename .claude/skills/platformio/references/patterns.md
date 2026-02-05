# PlatformIO Patterns Reference

## Contents
- Build Configuration
- Library Management
- Debug Patterns
- Anti-Patterns

## Build Configuration

### platformio.ini Structure

```ini
[platformio]
default_envs = 8ch

[env]
platform = espressif32
board = esp32dev
framework = arduino
monitor_speed = 115200
lib_deps =
    knolleary/PubSubClient@^2.8
    tzapu/WiFiManager@^0.16.0
    fastled/FastLED@^3.6.0

[env:8ch]
build_flags = 
    -D NUM_CHANNELS=8
    -D DEVICE_VARIANT="8CH"

[env:4ch]
build_flags = 
    -D NUM_CHANNELS=4
    -D DEVICE_VARIANT="4CH"

[env:8ch-ota]
extends = env:8ch
upload_protocol = espota
upload_port = 192.168.1.100
```

### Conditional Compilation

```cpp
// config.h
#ifndef NUM_CHANNELS
#define NUM_CHANNELS 8
#endif

#if NUM_CHANNELS == 8
const int PWM_PINS[] = {25, 26, 27, 14, 4, 5, 18, 19};
#elif NUM_CHANNELS == 4
const int PWM_PINS[] = {25, 26, 27, 14};
#endif
```

## Library Management

### Adding Dependencies

```ini
# In platformio.ini [env] section
lib_deps =
    # By name and version
    knolleary/PubSubClient@^2.8
    
    # By GitHub URL
    https://github.com/tzapu/WiFiManager.git
    
    # By specific commit
    https://github.com/tzapu/WiFiManager.git#v2.0.17
```

### Update Libraries

```bash
# Update all libraries
pio pkg update

# Update specific library
pio pkg update -l "PubSubClient"
```

## Debug Patterns

### Serial Debug Macros

```cpp
// Define in config.h
#define DEBUG_ENABLED 1

#if DEBUG_ENABLED
#define DEBUG_PRINT(x) Serial.print(x)
#define DEBUG_PRINTLN(x) Serial.println(x)
#define DEBUG_PRINTF(fmt, ...) Serial.printf(fmt, ##__VA_ARGS__)
#else
#define DEBUG_PRINT(x)
#define DEBUG_PRINTLN(x)
#define DEBUG_PRINTF(fmt, ...)
#endif

// Usage
DEBUG_PRINTF("Channel %d set to %d\n", channel, brightness);
```

### Build Flag Debug Control

```ini
[env:8ch-debug]
extends = env:8ch
build_flags = 
    ${env:8ch.build_flags}
    -D DEBUG_ENABLED=1
    -D CORE_DEBUG_LEVEL=4
```

## Anti-Patterns

### WARNING: Blocking in loop()

**The Problem:**

```cpp
// BAD - Blocks WiFi and MQTT handling
void loop() {
    delay(1000);  // Blocks everything for 1 second
    readSensors();
}
```

**Why This Breaks:**
1. WiFiManager captive portal becomes unresponsive
2. MQTT keepalive fails, connection drops
3. WS2812 status LED updates freeze

**The Fix:**

```cpp
// GOOD - Non-blocking with millis()
unsigned long lastSensorRead = 0;
const unsigned long SENSOR_INTERVAL = 1000;

void loop() {
    if (millis() - lastSensorRead >= SENSOR_INTERVAL) {
        lastSensorRead = millis();
        readSensors();
    }
    // Other non-blocking code runs every iteration
}
```

### WARNING: String Concatenation in Loops

**The Problem:**

```cpp
// BAD - Memory fragmentation on ESP32
void publishAllStates() {
    for (int i = 0; i < NUM_CHANNELS; i++) {
        String topic = baseTopic + "/channel/" + String(i) + "/state";
        mqttClient.publish(topic.c_str(), String(brightness[i]).c_str());
    }
}
```

**Why This Breaks:**
1. Each `+` creates temporary String objects
2. ESP32 heap fragments over time
3. Eventually crashes with out-of-memory

**The Fix:**

```cpp
// GOOD - Pre-allocated char buffers
void publishAllStates() {
    char topic[64];
    char payload[8];
    
    for (int i = 0; i < NUM_CHANNELS; i++) {
        snprintf(topic, sizeof(topic), "%s/channel/%d/state", baseTopic, i);
        snprintf(payload, sizeof(payload), "%d", brightness[i]);
        mqttClient.publish(topic, payload);
    }
}
```

### WARNING: Missing MQTT Reconnection Logic

**The Problem:**

```cpp
// BAD - No reconnection handling
void loop() {
    mqttClient.loop();  // Silently fails if disconnected
}
```

**The Fix:**

```cpp
// GOOD - Reconnection with backoff
unsigned long lastReconnectAttempt = 0;

void loop() {
    if (!mqttClient.connected()) {
        unsigned long now = millis();
        if (now - lastReconnectAttempt > 5000) {
            lastReconnectAttempt = now;
            if (reconnectMqtt()) {
                lastReconnectAttempt = 0;
            }
        }
    } else {
        mqttClient.loop();
    }
}