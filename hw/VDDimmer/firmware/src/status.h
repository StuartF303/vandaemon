#pragma once
#include <Arduino.h>

// Serialises the board's status object -- identity, build id, uptime, WiFi,
// MQTT, telemetry, heap -- as JSON into buf. One shape, produced here only,
// served at /api/status and /status.json. Build-Flash.ps1 -Ota reads "build"
// from it to prove which image is running after an OTA push.
//
// Returns the number of bytes written, or 0 if the document did not fit.
size_t status_json(char *buf, size_t len);
