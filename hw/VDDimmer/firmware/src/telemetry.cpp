#include "telemetry.h"
#include "board_pins.h"
#include "pwm.h"
#include <math.h>

// Board thermal limits. These protect the board itself -- the 5 A total cap and
// the four 20N06 channels -- not the battery.
static constexpr float TEMP_DERATE_START_C = 70.0f;   // begin backing off
static constexpr float TEMP_DERATE_FULL_C  = 85.0f;   // channels held off
static constexpr float TEMP_RECOVER_C      = 65.0f;   // hysteresis

static constexpr uint8_t  ADC_SAMPLES = 16;
static constexpr uint32_t SAMPLE_INTERVAL_MS = 1000;

static float    s_vin       = 0.0f;
static float    s_tempC     = 25.0f;
static float    s_derate    = 1.0f;
static bool     s_overTemp  = false;
static uint32_t s_lastSample = 0;

static uint32_t averageMillivolts(uint8_t pin) {
    uint32_t sum = 0;
    for (uint8_t i = 0; i < ADC_SAMPLES; i++) sum += analogReadMilliVolts(pin);
    return sum / ADC_SAMPLES;
}

void telemetry_begin() {
    analogSetPinAttenuation(PIN_VIN_SENSE, ADC_11db);
    analogSetPinAttenuation(PIN_NTC_SENSE, ADC_11db);
    telemetry_tick();
}

static float readVin() {
    return (float)averageMillivolts(PIN_VIN_SENSE) * VIN_DIVIDER_RATIO / 1000.0f;
}

static float readTempC() {
    float mv = (float)averageMillivolts(PIN_NTC_SENSE);

    // RT1 sits between NTC_SENSE and GND with R29 pulling up to +3V3, so the
    // measured voltage falls as the board warms.
    if (mv <= 1.0f || mv >= NTC_RAIL_MV - 1.0f) {
        return s_tempC;   // open or shorted sensor: hold the last good reading
    }

    float rNtc = NTC_SERIES_OHMS * mv / (NTC_RAIL_MV - mv);
    float invT = 1.0f / NTC_NOMINAL_K + logf(rNtc / NTC_NOMINAL_OHMS) / NTC_BETA;
    return 1.0f / invT - 273.15f;
}

void telemetry_tick() {
    if (s_lastSample && millis() - s_lastSample < SAMPLE_INTERVAL_MS) return;
    s_lastSample = millis();

    s_vin = readVin();
    s_tempC = readTempC();

    float derate = 1.0f;
    if (s_tempC >= TEMP_DERATE_FULL_C) {
        derate = 0.0f;
    } else if (s_tempC > TEMP_DERATE_START_C) {
        derate = 1.0f - (s_tempC - TEMP_DERATE_START_C)
                            / (TEMP_DERATE_FULL_C - TEMP_DERATE_START_C);
    }

    if (s_overTemp && s_tempC < TEMP_RECOVER_C) s_overTemp = false;
    if (derate < 1.0f) s_overTemp = true;

    s_derate = derate;
    pwm_setDerate(derate);
}

float telemetry_vinVolts() { return s_vin; }
float telemetry_tempC()    { return s_tempC; }
float telemetry_derate()   { return s_derate; }
bool  telemetry_overTemp() { return s_overTemp; }
