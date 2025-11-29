/**
 * @file mqtt_client.h
 * @brief MQTT client for VanDaemon LED Dimmer control
 */

#ifndef MQTT_CLIENT_H
#define MQTT_CLIENT_H

#include <Arduino.h>

/**
 * @brief Initialize MQTT client with broker settings from NVS
 * @return true if configuration valid
 */
bool mqtt_init();

/**
 * @brief Connect to MQTT broker
 * @return true if connected successfully
 */
bool mqtt_connect();

/**
 * @brief Check MQTT connection status
 * @return true if connected
 */
bool mqtt_isConnected();

/**
 * @brief MQTT client loop - handles incoming messages and keeps connection alive
 * Call this frequently in main loop
 */
void mqtt_loop();

/**
 * @brief Publish channel state to MQTT
 * @param channel Channel number (0-indexed, 0-7)
 * @param value Brightness value (0-255)
 */
void mqtt_publishChannelState(uint8_t channel, uint8_t value);

/**
 * @brief Publish all channel states
 */
void mqtt_publishAllStates();

/**
 * @brief Publish device status (online/offline via LWT)
 * @param online true for online, false for offline
 */
void mqtt_publishStatus(bool online);

/**
 * @brief Publish device configuration/capabilities
 */
void mqtt_publishConfig();

/**
 * @brief Publish heartbeat message
 */
void mqtt_publishHeartbeat();

/**
 * @brief Set MQTT broker configuration
 * @param broker Broker hostname or IP
 * @param port Broker port (default 1883)
 * @param username MQTT username (optional)
 * @param password MQTT password (optional)
 */
void mqtt_setConfig(const char* broker, uint16_t port,
                    const char* username = nullptr,
                    const char* password = nullptr);

/**
 * @brief Get device ID
 * @return Device ID string
 */
String mqtt_getDeviceId();

/**
 * @brief Set device name
 * @param name Friendly device name
 */
void mqtt_setDeviceName(const char* name);

/**
 * @brief Get device name
 * @return Device name string
 */
String mqtt_getDeviceName();

#endif // MQTT_CLIENT_H
