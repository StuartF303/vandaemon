#pragma once
#include "settings.h"

// Loads settings from NVS, filling in MAC-derived defaults for deviceId and
// deviceName on first boot.
void store_begin();
void store_saveSettings();
void store_clearWifi();
void store_factoryReset();

// Channel/strip levels are written back lazily -- store_tick() flushes at most
// once every few seconds so a slider drag does not chew through NVS endurance.
void store_markLevelsDirty();
void store_stageLevels(const uint8_t *pwm, uint8_t pwmCount);
void store_tick();

void     store_loadLevels(uint8_t *pwm, uint8_t pwmCount);
void     store_saveLevelsNow(const uint8_t *pwm, uint8_t pwmCount);
uint32_t store_loadStripColour(uint8_t index, uint8_t &brightness, bool &on);
void     store_saveStripColour(uint8_t index, uint32_t rgb, uint8_t brightness, bool on);

// Last 6 hex characters of the WiFi MAC, e.g. "a1b2c3".
const char *store_macSuffix();
