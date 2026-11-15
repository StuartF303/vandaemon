#pragma once
#include <Arduino.h>

void net_begin();
void net_tick();

bool net_wifiUp();
bool net_mqttUp();

// Number of channels declared in the config message: 4, or 6 when the strips
// are exposed as channels 5 and 6.
uint8_t net_declaredChannels();

void net_publishChannelState(uint8_t channel);
void net_publishStripState(uint8_t index);
void net_publishAllStates();

void net_forgetWifiAndReboot();
