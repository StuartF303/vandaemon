#pragma once
#include <Arduino.h>

void net_begin();
void net_tick();

bool net_wifiUp();
bool net_mqttUp();

// Outcome of the most recent MQTT connect attempt, for the status object:
// "connected" | "refused" | "unreachable" | "timeout" | "lost" | "failed" |
// "idle" (not tried yet) | "unset" (no broker configured).
const char *net_mqttResult();

// Broker settings in g_settings changed: drop the session and reconnect.
void net_mqttReconfigure();

// Number of channels declared in the config message: 4, or 6 when the strips
// are exposed as channels 5 and 6.
uint8_t net_declaredChannels();

void net_publishChannelState(uint8_t channel);
void net_publishStripState(uint8_t index);
void net_publishAllStates();

// Re-send the retained config message (names changed). No-op when MQTT is down.
void net_republishConfig();

void net_forgetWifiAndReboot();
