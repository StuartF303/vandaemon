/**
 * @file wifi_manager.cpp
 * @brief WiFi connection management implementation
 */

#include "wifi_manager.h"
#include "config.h"
#include "status_led.h"
#include <WiFi.h>
#include <WiFiManager.h>

static WiFiManager wifiManager;
static bool wifiConnected = false;
static uint32_t lastReconnectAttempt = 0;
static const uint32_t RECONNECT_INTERVAL = 30000;  // 30 seconds

bool wifi_init() {
    #ifdef ENABLE_SERIAL
    Serial.println("Initializing WiFi...");
    #endif

    // Set WiFi mode
    WiFi.mode(WIFI_STA);

    // Set hostname
    WiFi.setHostname(WIFI_AP_NAME);

    // Show WiFi setup status
    status_setColor(STATUS_WIFI_SETUP);

    // Configure WiFiManager
    wifiManager.setConfigPortalTimeout(WIFI_TIMEOUT);
    wifiManager.setAPCallback([](WiFiManager *myWiFiManager) {
        #ifdef ENABLE_SERIAL
        Serial.println("Entered config mode");
        Serial.print("AP SSID: ");
        Serial.println(myWiFiManager->getConfigPortalSSID());
        Serial.print("AP IP: ");
        Serial.println(WiFi.softAPIP());
        #endif

        // Blink purple while in config mode
        status_blink(STATUS_WIFI_SETUP, 3, 200);
    });

    // Set save config callback
    wifiManager.setSaveConfigCallback([]() {
        #ifdef ENABLE_SERIAL
        Serial.println("WiFi credentials saved");
        #endif
    });

    // Try to connect
    wifiConnected = wifiManager.autoConnect(WIFI_AP_NAME, WIFI_AP_PASS);

    if (wifiConnected) {
        #ifdef ENABLE_SERIAL
        Serial.println("WiFi connected!");
        Serial.print("IP address: ");
        Serial.println(WiFi.localIP());
        Serial.print("RSSI: ");
        Serial.print(WiFi.RSSI());
        Serial.println(" dBm");
        #endif

        status_setColor(STATUS_READY);
        return true;
    } else {
        #ifdef ENABLE_SERIAL
        Serial.println("Failed to connect to WiFi");
        #endif

        status_setColor(STATUS_ERROR);
        return false;
    }
}

bool wifi_isConnected() {
    wifiConnected = (WiFi.status() == WL_CONNECTED);
    return wifiConnected;
}

int wifi_getRSSI() {
    if (wifi_isConnected()) {
        return WiFi.RSSI();
    }
    return 0;
}

String wifi_getIPAddress() {
    if (wifi_isConnected()) {
        return WiFi.localIP().toString();
    }
    return "0.0.0.0";
}

void wifi_resetCredentials() {
    #ifdef ENABLE_SERIAL
    Serial.println("Resetting WiFi credentials...");
    #endif

    wifiManager.resetSettings();

    delay(1000);
    ESP.restart();
}

void wifi_maintain() {
    // Check connection status
    if (!wifi_isConnected()) {
        // Try to reconnect if interval has passed
        if (millis() - lastReconnectAttempt >= RECONNECT_INTERVAL) {
            #ifdef ENABLE_SERIAL
            Serial.println("WiFi disconnected, attempting reconnect...");
            #endif

            status_setColor(STATUS_WIFI_SETUP);

            WiFi.reconnect();
            lastReconnectAttempt = millis();

            // Wait a bit for connection
            for (int i = 0; i < 20; i++) {
                if (wifi_isConnected()) {
                    #ifdef ENABLE_SERIAL
                    Serial.println("WiFi reconnected!");
                    #endif

                    status_setColor(STATUS_READY);
                    return;
                }
                delay(500);
            }

            #ifdef ENABLE_SERIAL
            Serial.println("WiFi reconnect failed");
            #endif

            status_setColor(STATUS_ERROR);
        }
    }
}
