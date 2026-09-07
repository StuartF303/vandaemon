#pragma once
#include <Arduino.h>

static constexpr char FW_VERSION[] = "1.0.0";

// Type name, identical on every board of this design. NOT a device identity --
// the backend only logs it. Uniqueness lives in Settings::deviceId.
static constexpr char FW_VARIANT[] = "4CH2A";

static constexpr uint8_t STRIP_COUNT = 2;
static constexpr uint16_t STRIP_MAX_PIXELS = 300;

struct StripSettings {
    uint16_t length   = 30;
    bool     supply5v = true;   // matches the J9/J10 shunt position; see README
};

struct Settings {
    char deviceId[32]   = {0};  // MQTT topic level + control-ID prefix; unique
    char deviceName[40] = {0};  // human label the backend renders in control names

    char wifiSsid[33] = {0};
    char wifiPass[65] = {0};

    char     mqttHost[64] = {0};
    uint16_t mqttPort     = 1883;
    char     mqttUser[33] = {0};
    char     mqttPass[65] = {0};
    char     baseTopic[64] = "vandaemon/leddimmer";

    StripSettings strip[STRIP_COUNT];

    // When true the config message declares 6 channels and CH4/CH5 map to the
    // per-strip master brightness, so the strips appear as ordinary dimmers in
    // VanDaemon with no backend change. Turn this OFF once a dedicated
    // colour-strip module exists, or the same hardware gets two sets of
    // controls -- and delete the two orphaned "Channel 5"/"Channel 6" entries
    // from controls.json once.
    bool exposeStripsAsChannels = true;

    bool     restoreOnBoot = true;   // reload last levels from NVS at power-up
    uint16_t pwmFreqHz     = 1200;   // spec asks for 1-2 kHz
    bool     gammaCorrect  = true;   // CIE 1931 curve over the 0-255 wire value
};

extern Settings g_settings;
