#pragma once
#include <Arduino.h>

// The board's own web interface on port 80: the lights page at / and the JSON
// API under /api/. Started once WiFi is up (from startNetServices()) and
// serviced from net_tick(). No authentication, by decision -- anyone on the
// van LAN can dim, rename, and change the broker. See firmware/README.md.
void web_begin();   // idempotent
void web_tick();
