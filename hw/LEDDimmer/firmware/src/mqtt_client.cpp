/**
 * @file mqtt_client.cpp
 * @brief MQTT client implementation for VanDaemon integration
 */

#include "mqtt_client.h"
#include "config.h"
#include "pwm_control.h"
#include "status_led.h"
#include <WiFi.h>
#include <PubSubClient.h>
#include <Preferences.h>
#include <ArduinoJson.h>

static WiFiClient wifiClient;
static PubSubClient mqttClient(wifiClient);
static Preferences prefs;

static String deviceId;
static String deviceName;
static String mqttBroker;
static uint16_t mqttPort = MQTT_PORT;
static String mqttUser;
static String mqttPass;

static uint32_t lastReconnectAttempt = 0;
static uint32_t lastStatePublish = 0;
static uint32_t lastHeartbeat = 0;

// Topic strings (built dynamically with device ID)
static String topicStatus;
static String topicConfig;
static String topicHeartbeat;
static String topicChannelCmd[NUM_CHANNELS];
static String topicChannelState[NUM_CHANNELS];
static String topicAllCmd;

// Forward declarations
static void buildTopics();
static void mqttCallback(char* topic, byte* payload, unsigned int length);
static bool reconnect();

bool mqtt_init() {
    // Load configuration from NVS
    prefs.begin(PREFS_NAMESPACE, true);  // Read-only

    // Get device ID (use MAC address if not set)
    deviceId = prefs.getString(PREFS_DEVICE_ID, "");
    if (deviceId.isEmpty()) {
        uint8_t mac[6];
        WiFi.macAddress(mac);
        deviceId = "leddimmer-" + String(mac[3], HEX) + String(mac[4], HEX) + String(mac[5], HEX);

        // Save default device ID
        prefs.end();
        prefs.begin(PREFS_NAMESPACE, false);
        prefs.putString(PREFS_DEVICE_ID, deviceId);
        prefs.end();
        prefs.begin(PREFS_NAMESPACE, true);
    }

    deviceName = prefs.getString(PREFS_DEVICE_NAME, deviceId);
    mqttBroker = prefs.getString(PREFS_MQTT_BROKER, "");
    mqttUser = prefs.getString(PREFS_MQTT_USER, "");
    mqttPass = prefs.getString(PREFS_MQTT_PASS, "");

    prefs.end();

    #ifdef ENABLE_SERIAL
    Serial.println("MQTT Configuration:");
    Serial.printf("  Device ID: %s\n", deviceId.c_str());
    Serial.printf("  Device Name: %s\n", deviceName.c_str());
    Serial.printf("  Broker: %s:%d\n", mqttBroker.c_str(), mqttPort);
    #endif

    if (mqttBroker.isEmpty()) {
        #ifdef ENABLE_SERIAL
        Serial.println("MQTT broker not configured!");
        #endif
        return false;
    }

    // Build topic strings
    buildTopics();

    // Configure MQTT client
    mqttClient.setServer(mqttBroker.c_str(), mqttPort);
    mqttClient.setCallback(mqttCallback);
    mqttClient.setKeepAlive(MQTT_KEEPALIVE);
    mqttClient.setBufferSize(512);  // Increase buffer for JSON messages

    return true;
}

static void buildTopics() {
    String baseTopic = String(MQTT_BASE_TOPIC) + "/" + deviceId;

    topicStatus = baseTopic + "/status";
    topicConfig = baseTopic + "/config";
    topicHeartbeat = baseTopic + "/heartbeat";
    topicAllCmd = baseTopic + "/all/set";

    for (uint8_t i = 0; i < NUM_CHANNELS; i++) {
        topicChannelCmd[i] = baseTopic + "/channel/" + String(i) + "/set";
        topicChannelState[i] = baseTopic + "/channel/" + String(i) + "/state";
    }
}

static void mqttCallback(char* topic, byte* payload, unsigned int length) {
    // Convert payload to string
    char message[length + 1];
    memcpy(message, payload, length);
    message[length] = '\0';

    #ifdef ENABLE_SERIAL
    Serial.printf("MQTT message received [%s]: %s\n", topic, message);
    #endif

    String topicStr = String(topic);

    // Handle all channels command
    if (topicStr == topicAllCmd) {
        int value = String(message).toInt();
        value = constrain(value, 0, 255);
        pwm_setAllChannels(value);
        mqtt_publishAllStates();
        return;
    }

    // Handle individual channel commands
    for (uint8_t i = 0; i < NUM_CHANNELS; i++) {
        if (topicStr == topicChannelCmd[i]) {
            // Parse JSON or simple integer
            int value = 0;

            // Try to parse as JSON first
            JsonDocument doc;
            DeserializationError error = deserializeJson(doc, message);

            if (!error && doc.containsKey("brightness")) {
                value = doc["brightness"];
            } else {
                // Fall back to simple integer
                value = String(message).toInt();
            }

            value = constrain(value, 0, 255);

            #ifdef ENABLE_TRANSITIONS
            pwm_setChannelSmooth(i, value);
            #else
            pwm_setChannel(i, value);
            #endif

            // Publish new state
            mqtt_publishChannelState(i, value);

            // Save state
            pwm_saveState();
            return;
        }
    }
}

static bool reconnect() {
    #ifdef ENABLE_SERIAL
    Serial.print("Attempting MQTT connection...");
    #endif

    status_setColor(STATUS_MQTT_CONN);

    // Create Last Will Testament (LWT) message
    String willTopic = topicStatus;
    String willMessage = "offline";

    // Attempt to connect
    bool connected = false;
    if (mqttUser.isEmpty()) {
        connected = mqttClient.connect(deviceId.c_str(),
                                       willTopic.c_str(), MQTT_QOS, MQTT_RETAIN,
                                       willMessage.c_str());
    } else {
        connected = mqttClient.connect(deviceId.c_str(),
                                       mqttUser.c_str(), mqttPass.c_str(),
                                       willTopic.c_str(), MQTT_QOS, MQTT_RETAIN,
                                       willMessage.c_str());
    }

    if (connected) {
        #ifdef ENABLE_SERIAL
        Serial.println("connected");
        #endif

        status_setColor(STATUS_READY);

        // Publish online status
        mqtt_publishStatus(true);

        // Publish device configuration
        mqtt_publishConfig();

        // Subscribe to command topics
        for (uint8_t i = 0; i < NUM_CHANNELS; i++) {
            mqttClient.subscribe(topicChannelCmd[i].c_str(), MQTT_QOS);
            #ifdef ENABLE_SERIAL
            Serial.printf("Subscribed to: %s\n", topicChannelCmd[i].c_str());
            #endif
        }

        // Subscribe to all channels command
        mqttClient.subscribe(topicAllCmd.c_str(), MQTT_QOS);
        #ifdef ENABLE_SERIAL
        Serial.printf("Subscribed to: %s\n", topicAllCmd.c_str());
        #endif

        // Publish current states
        mqtt_publishAllStates();

        return true;
    } else {
        #ifdef ENABLE_SERIAL
        Serial.printf("failed, rc=%d\n", mqttClient.state());
        #endif

        status_setColor(STATUS_ERROR);
        return false;
    }
}

bool mqtt_connect() {
    return reconnect();
}

bool mqtt_isConnected() {
    return mqttClient.connected();
}

void mqtt_loop() {
    if (!mqttClient.connected()) {
        // Attempt reconnect every 5 seconds
        if (millis() - lastReconnectAttempt >= MQTT_RECONNECT_MS) {
            lastReconnectAttempt = millis();
            reconnect();
        }
    } else {
        mqttClient.loop();

        // Periodic state publish
        if (millis() - lastStatePublish >= STATE_PUBLISH_MS) {
            lastStatePublish = millis();
            mqtt_publishAllStates();
        }

        // Periodic heartbeat
        if (millis() - lastHeartbeat >= HEARTBEAT_MS) {
            lastHeartbeat = millis();
            mqtt_publishHeartbeat();
        }
    }
}

void mqtt_publishChannelState(uint8_t channel, uint8_t value) {
    if (!mqttClient.connected() || channel >= NUM_CHANNELS) {
        return;
    }

    String payload = String(value);
    mqttClient.publish(topicChannelState[channel].c_str(), payload.c_str(), MQTT_RETAIN);

    #ifdef ENABLE_SERIAL
    Serial.printf("Published: %s = %s\n", topicChannelState[channel].c_str(), payload.c_str());
    #endif
}

void mqtt_publishAllStates() {
    for (uint8_t i = 0; i < NUM_CHANNELS; i++) {
        mqtt_publishChannelState(i, pwm_getChannel(i));
    }
}

void mqtt_publishStatus(bool online) {
    if (!mqttClient.connected()) {
        return;
    }

    String payload = online ? "online" : "offline";
    mqttClient.publish(topicStatus.c_str(), payload.c_str(), MQTT_RETAIN);

    #ifdef ENABLE_SERIAL
    Serial.printf("Published status: %s\n", payload.c_str());
    #endif
}

void mqtt_publishConfig() {
    if (!mqttClient.connected()) {
        return;
    }

    JsonDocument doc;
    doc["deviceId"] = deviceId;
    doc["deviceName"] = deviceName;
    doc["channels"] = NUM_CHANNELS;
    doc["version"] = FIRMWARE_VERSION;
    doc["variant"] = BOARD_VARIANT;

    String payload;
    serializeJson(doc, payload);

    mqttClient.publish(topicConfig.c_str(), payload.c_str(), MQTT_RETAIN);

    #ifdef ENABLE_SERIAL
    Serial.printf("Published config: %s\n", payload.c_str());
    #endif
}

void mqtt_publishHeartbeat() {
    if (!mqttClient.connected()) {
        return;
    }

    JsonDocument doc;
    doc["uptime"] = millis() / 1000;
    doc["freeHeap"] = ESP.getFreeHeap();
    doc["rssi"] = WiFi.RSSI();

    String payload;
    serializeJson(doc, payload);

    mqttClient.publish(topicHeartbeat.c_str(), payload.c_str(), false);
}

void mqtt_setConfig(const char* broker, uint16_t port, const char* username, const char* password) {
    prefs.begin(PREFS_NAMESPACE, false);

    prefs.putString(PREFS_MQTT_BROKER, broker);
    mqttBroker = broker;
    mqttPort = port;

    if (username != nullptr) {
        prefs.putString(PREFS_MQTT_USER, username);
        mqttUser = username;
    }

    if (password != nullptr) {
        prefs.putString(PREFS_MQTT_PASS, password);
        mqttPass = password;
    }

    prefs.end();

    #ifdef ENABLE_SERIAL
    Serial.println("MQTT configuration saved");
    #endif
}

String mqtt_getDeviceId() {
    return deviceId;
}

void mqtt_setDeviceName(const char* name) {
    deviceName = String(name);

    prefs.begin(PREFS_NAMESPACE, false);
    prefs.putString(PREFS_DEVICE_NAME, deviceName);
    prefs.end();

    #ifdef ENABLE_SERIAL
    Serial.printf("Device name set to: %s\n", deviceName.c_str());
    #endif

    // Republish config with new name
    if (mqttClient.connected()) {
        mqtt_publishConfig();
    }
}

String mqtt_getDeviceName() {
    return deviceName;
}
