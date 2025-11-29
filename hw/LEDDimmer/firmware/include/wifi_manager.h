/**
 * @file wifi_manager.h
 * @brief WiFi connection management with captive portal for initial setup
 */

#ifndef WIFI_MANAGER_H
#define WIFI_MANAGER_H

#include <Arduino.h>

/**
 * @brief Initialize WiFi with WiFiManager
 * Starts captive portal if no credentials stored
 * @return true if connected successfully
 */
bool wifi_init();

/**
 * @brief Check WiFi connection status
 * @return true if connected
 */
bool wifi_isConnected();

/**
 * @brief Get WiFi RSSI signal strength
 * @return RSSI value in dBm
 */
int wifi_getRSSI();

/**
 * @brief Get local IP address as string
 * @return IP address string
 */
String wifi_getIPAddress();

/**
 * @brief Reset WiFi credentials and restart in AP mode
 */
void wifi_resetCredentials();

/**
 * @brief WiFi keep-alive and reconnection handler
 * Call this periodically in main loop
 */
void wifi_maintain();

#endif // WIFI_MANAGER_H
