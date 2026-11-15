#include "status.h"
#include "settings.h"
#include "net.h"
#include "telemetry.h"
#include <WiFi.h>
#include <ArduinoJson.h>
#include <math.h>

size_t status_json(char *buf, size_t len) {
    JsonDocument doc;
    doc["t"]          = "status";
    doc["deviceId"]   = g_settings.deviceId;
    doc["deviceName"] = g_settings.deviceName;
    doc["fw"]         = FW_VERSION;
    doc["build"]      = BUILD_ID;
    doc["uptime"]     = (uint32_t)(millis() / 1000);

    JsonObject wifi = doc["wifi"].to<JsonObject>();
    wifi["ssid"] = g_settings.wifiSsid;
    wifi["up"]   = net_wifiUp();
    wifi["rssi"] = net_wifiUp() ? WiFi.RSSI() : 0;
    wifi["ip"]   = net_wifiUp() ? WiFi.localIP().toString() : String("");

    JsonObject mqtt = doc["mqtt"].to<JsonObject>();
    mqtt["host"]       = g_settings.mqttHost;
    mqtt["port"]       = g_settings.mqttPort;
    mqtt["up"]         = net_mqttUp();
    mqtt["lastResult"] = net_mqttResult();

    doc["vin"]      = roundf(telemetry_vinVolts() * 100.0f) / 100.0f;
    doc["tempC"]    = roundf(telemetry_tempC() * 10.0f) / 10.0f;
    doc["derate"]   = telemetry_derate();
    doc["overTemp"] = telemetry_overTemp();
    doc["freeHeap"] = (uint32_t)ESP.getFreeHeap();

    if (measureJson(doc) + 1 > len) return 0;
    return serializeJson(doc, buf, len);
}
