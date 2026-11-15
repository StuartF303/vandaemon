# VDDimmer web control: named channels from any browser on the van LAN

**Date:** 2026-11-15 · **Status:** design agreed in conversation, awaiting builder check
**Branch:** cut from `main` at `744d32a` or later (PR #30 merged; the OTA path is in)
**Hardware:** VANDIMMER-4CH+2A Rev A. Nothing here needs a board change.
**Supersedes in part:** the firmware half of
`2026-09-26-vddimmer-local-control-design.md`. Sections 2 of that document
(non-blocking WiFi, portal fallback, status page, channel names) are carried
forward here with changes noted. Its Bluetooth service and phone app are **not**
in this work and are not cancelled; see section 9.

## 1. Purpose

Stuart wants the dimmer usable from his phone, directly, with the channels
named for the places they light: Front, Kitchen, Bedroom. Today that is only
possible through the VanDaemon dashboard, which needs the Pi and a broker to be
up, and names live only in VanDaemon's `controls.json`.

This work makes the board self-sufficient for lighting control: it serves its
own control page over the van WiFi, stores the channel names itself, and can
be re-provisioned from a phone when the WiFi changes instead of being opened
up and flashed over USB.

Scope ruling from Stuart, 2026-11-15: **device and firmware only.** No change
to the VanDaemon backend or frontend in this work. The larger project is
looked at later.

### Success criteria

1. Any browser on the van LAN opens `http://<deviceId>.local/` or
   `http://<ip>/` and can switch and dim each of the four PWM channels by its
   name, with a slider and an on/off switch per channel.
2. Channel names and the board name are set from that page, stored on the
   board, survive power cycles and NVS-independent of any phone, and appear in
   the board's MQTT `config` message so VanDaemon can adopt them later with no
   change now.
3. A level change from any source (page, VanDaemon over MQTT, BTN2, serial)
   is visible on every open page within a few seconds.
4. The page shows the running firmware build id, so an OTA update can be
   confirmed remotely. `Build-Flash.ps1 -Ota` uses it: its confirm stage
   proves the build id, closing the gap recorded in PR #30.
5. A board with wrong WiFi credentials recovers without USB: after repeated
   failures it raises its own setup network, and the WiFi can also be changed
   from the control page with automatic fallback if the new network does not
   work.
6. `loop()` never blocks on WiFi. Buttons, serial, strips and the status LED
   keep working while the board is trying to connect.

### Decisions taken

| Decision | Choice | Who |
|---|---|---|
| Phone transport | A web page served by the board over WiFi, not Bluetooth and not a native app. "WiFi is usually up; the web page is the fallback that works from any browser on the van's LAN." | Stuart |
| Lights page layout | One row per channel: name, on/off switch, slider (mockup A). No tiles, no master row, no strips on the page. | Stuart |
| Settings layout | Tabs: Names, Status, WiFi, Broker, Board. | Stuart |
| Authentication | **None.** No PIN on anything, "on the local LAN at least for now". Anyone on the van WiFi can dim, rename, and change WiFi or broker settings. | Stuart |
| Where names live | On the board, in NVS. Also sent in the MQTT config message. | agreed |
| Strips (J7/J8) on the page | Not in v1. Their MQTT control is unchanged. | agreed |
| Advanced settings on the page (PWM frequency, gamma, LED brightness, 5 V cap, strip lengths) | Not in v1. They stay in the first-boot portal form and over MQTT `cmd`. | agreed |
| Which settings tab opens first | Names while any channel is unnamed, otherwise Status. | Claude's call |
| Live update mechanism | The page polls the board every 2 s. No WebSocket, no server-sent events. | Claude's call, section 3 |
| Page delivery | One self-contained HTML document compiled into the firmware, no filesystem, no external assets. | Claude's call, section 3 |

## 2. What exists today, and what this builds on

Firmware is `hw/VDDimmer/firmware/`, about 1,900 lines, Arduino-ESP32 3.x on
the pioarduino platform. Relevant facts, verified against the source on
2026-11-15:

- `portal.cpp` already runs a synchronous `WebServer` on port 80 with a
  `DNSServer` captive portal, but **only** in AP mode on first boot (no stored
  SSID) or after `forget-wifi`. Nothing is served while the board is a station.
  Its HTML is a `PROGMEM` string built with `String` concatenation, with a
  full settings form including WiFi, MQTT, strips and behaviour.
- `settings.h` has `deviceId[32]`, `deviceName[40]`, WiFi and MQTT fields, strip
  settings and behaviour flags. There are **no channel names**. NVS namespace
  is `vandimmer`, keys are short (`devname`, `ssid`, `mqhost`, ...).
- `net.cpp` publishes `config` as a retained JSON message of
  `deviceId, deviceName, channels, version, variant` into a 256-byte buffer;
  the PubSubClient buffer is `MQTT_BUFFER_BYTES = 512`. With
  `exposeStripsAsChannels` on (default) `channels` is 6: the two strips appear
  as channels 4 and 5.
- `net.cpp` `wifiConnect()` **busy-waits up to 20 s** with `delay(50)`. On WiFi
  loss `net_tick()` calls `net_begin()` again after 5 s, which busy-waits
  again. PR #30 added a latched `startNetServices()` so a late association no
  longer strands the board, and recorded that this is a narrow fix pending the
  state machine designed in September.
- Level changes fan out from two places: `persistAndPublish()` in `main.cpp`
  (buttons, serial) and the `channel/N/set` handler in `net.cpp`. Each calls
  `pwm_set`, stages NVS and publishes MQTT state.
- `ArduinoOTA.begin()` starts mDNS with hostname `deviceId`. No HTTP service
  record is advertised.
- `BUILD_ID` is a compile-time define (`<short-sha>[-dirty] <UTC stamp>`)
  printed on the boot banner. It reaches nothing else.
- The van board is `vandimmer-6f9914` at 192.168.1.115, on `ef18200f-dirty`,
  in a sealed case. OTA is the normal update path (`vddimmer-ota` skill).

The backend's `MqttLedDimmerPlugin` deserialises `config` with
`System.Text.Json` into `LedDimmerDeviceInfo`, which ignores unknown
properties. Adding `names` to the message is safe for the backend as it stands.

## 3. The web interface

### Serving

One `WebServer` instance on port 80, owned by a new `web.cpp`, running whenever
the board has an IP on either interface. `portal.cpp` stops owning the server
and registers its routes on the shared instance instead. The captive-portal
`DNSServer` and the `onNotFound` catch-all remain portal-only behaviour, active
only while the AP is up.

mDNS: after `ArduinoOTA.begin()`, add `MDNS.addService("http", "tcp", 80)`.
The page is reachable at `http://<deviceId>.local/` where mDNS resolves
(Android, iOS, macOS; Windows is unreliable, see the `vddimmer-ota` skill) and
always at the IP.

### The page

A single HTML document with inline CSS and JavaScript, stored as a `PROGMEM`
string, served at `/` with `Cache-Control: no-cache`. Target size under 16 kB
uncompressed; no framework, no web fonts, no external requests. Dark theme,
large touch targets, `viewport` meta for phones. Hash routing: `#lights`
(default) and `#settings/<tab>`.

**Lights view** (mockup A, agreed): header with the board name and deviceId
and a gear icon. One row per PWM channel: name (or "Channel N" when unnamed),
an on/off switch, a slider 0–255 shown as a percentage. Footer: WiFi RSSI,
broker state as a coloured dot (green connected, cyan not connected), input
voltage, board temperature.

Switch off sets level 0 and remembers the previous non-zero level in the page;
switch on restores it, or 255 if there was none. This memory is per page, not
on the board (same rule as the September app design).

Slider behaviour: the UI moves immediately; while dragging, the page sends at
most one request every 100 ms and the final value on release. Polling is
paused while a slider is held so the board's echo does not fight the thumb.

**Settings view**: tab bar with Names, Status, WiFi, Broker, Board. Contents
as in the agreed mockup:

| Tab | Contents |
|---|---|
| Names | Board name; four channel name fields, 20 characters max, placeholder "Channel N"; Save |
| Status | deviceId, firmware version and build id, uptime, SSID and RSSI, broker host and connected/not, input voltage, board temperature with derate state, free heap. Read-only, refreshed with the poll |
| WiFi | Current SSID; SSID picker from a scan with a free-text fallback; password field; "Save and reconnect". Explanatory note about the fallback behaviour and that the page will stop responding if the board moves to a different network |
| Broker | Connected or not and the last failure reason; host, port, user, password (blank keeps the stored value); "Save and reconnect" |
| Board | Identify (status LED blinks ~5 s); Reboot (confirm in page); nothing else in v1 |

### HTTP API

All JSON. The page is the only intended client, but the API is plain enough
to script against. No authentication (Stuart's ruling).

| Route | Method | Body | Reply |
|---|---|---|---|
| `/` | GET | | the page |
| `/api/state` | GET | | `{"levels":[4],"names":[4],"deviceName","deviceId"}` |
| `/api/status` | GET | | the **status object** below |
| `/status.json` | GET | | alias of `/api/status`, kept for tooling |
| `/api/channel/<0-3>` | POST | `{"level":0-255}` | `{"ok":true,"level":N}` |
| `/api/names` | POST | `{"deviceName","names":[4]}` | `{"ok":true}` |
| `/api/wifi/scan` | GET | | `[{"ssid","rssi","open"}]` from the last async scan; triggers a new one |
| `/api/wifi` | POST | `{"ssid","pass"}` | `{"ok":true}` immediately; result via `/api/status` `wifi.lastResult` |
| `/api/mqtt` | POST | `{"host","port","user","pass"}` | `{"ok":true}` immediately; result via `/api/status` `mqtt` |
| `/api/identify` | POST | | `{"ok":true}` |
| `/api/reboot` | POST | | `{"ok":true}`, reboots 500 ms later |

Errors: `400 {"error":"..."}` for bad JSON, out-of-range channel or level,
over-long names. Nothing crashes or reboots the board on bad input. Request
bodies are limited to 512 bytes.

### Status object

Produced by one function, used by `/api/status` and `/status.json`. The
September design's shape, extended:

```json
{"t":"status","deviceId":"vandimmer-6f9914","deviceName":"Van lights",
 "fw":"1.1.0","build":"26216f0 2026-11-15T16:02:11Z","uptime":3600,
 "wifi":{"ssid":"JCDammVan","up":true,"rssi":-58,"ip":"192.168.1.115",
         "lastResult":"connected"},
 "mqtt":{"host":"192.168.1.50","port":1883,"up":false,"lastResult":"refused"},
 "vin":12.6,"tempC":34.5,"derate":1.0,"overTemp":false,
 "freeHeap":212000}
```

The 240-byte limit from the September design was a Bluetooth MTU constraint
and does not apply here. `FW_VERSION` is bumped to `1.1.0` in this work.

### Polling and consistency

The page fetches `/api/state` every 2 s and `/api/status` every 5 s. That is
how a change from VanDaemon, BTN2, the serial console or a second phone
appears on an open page. Two seconds is acceptable for lights and keeps the
synchronous `WebServer` adequate; it handles one request at a time and each
of these is a few hundred bytes.

Why not push: `WebServer` has no WebSocket or SSE support, the async server
library would be a new dependency with its own ESP32-S3 3.x compatibility
history, and the gain over 2 s polling on a lights page is not worth that
risk. Revisit if the page ever shows more than four sliders.

## 4. Firmware: control fan-out

A new `control.cpp` becomes the single entry point for level changes, as the
September design proposed:

```cpp
void ctl_setLevel(uint8_t ch, uint8_t level);  // pwm, NVS stage, MQTT state
void ctl_setAll(uint8_t level);
void ctl_allOff();
```

`main.cpp` (buttons, serial console), `net.cpp` (`channel/N/set`) and
`web.cpp` all call it. `persistAndPublish()` moves into it. The `s_allOff` and
last-levels bookkeeping that BTN1 uses stays with the button handler; it is
Rev A dead code anyway (J11.1 unrouted) but Rev B will want it.

## 5. Firmware: names

`settings.h` gains `char chName[PWM_CHANNELS][21]` (20 characters plus NUL),
default empty. NVS keys `chn0`..`chn3`. Saved through the existing
`store_saveSettings()`.

`publishConfig()` adds `"names":[...]` with one entry per **declared**
channel. For the four PWM channels it is the stored name, or `""` when unset;
for the two strip channels (when `exposeStripsAsChannels` is on) it is the
fixed strings `"Strip 1"` and `"Strip 2"`. The serialisation buffer grows from
256 to 384 bytes. Worst case: 40-char deviceName, four 20-char names, two
strip names, the existing fields: about 290 bytes, inside both the 384-byte
buffer and the 512-byte PubSubClient buffer. The builder adds a static assert
or a startup check that the serialised config fits.

Saving names republishes `config` immediately, so a VanDaemon that later
learns to read `names` picks them up on the retained message.

## 6. Firmware: WiFi state machine, portal fallback, live re-provisioning

Carried from the September design, section 2, with these changes:
`WiFi.setSleep(false)` **stays** (no Bluetooth, so no modem-sleep
requirement); the PIN on portal Save is **dropped**; "Save requires the PIN"
is gone everywhere.

### Non-blocking WiFi

`wifiConnect()`'s busy-wait is replaced by a state machine in `net.cpp`,
polled from `net_tick()`:

```
IDLE -> CONNECTING -> UP
CONNECTING --(20 s, not connected)--> RETRY_WAIT --(delay)--> CONNECTING
UP --(status != connected)--> LOST -> RETRY_WAIT
```

Retry delay backs off 5, 10, 20 s, then holds at 30 s. The failure counter
resets on a successful connection. `startNetServices()` and its latch from
PR #30 remain: services start the first time the state reaches UP, from
whichever path.

### Portal fallback

- No stored SSID: the portal starts at boot as today (AP only, captive DNS).
- Stored SSID and **3 consecutive failed attempts**: the board switches to
  `WIFI_AP_STA`, raises `VANDIMMER-<suffix>`, and keeps retrying the station
  behind it.
- While any client is associated with the AP (`WiFi.softAPgetStationNum() > 0`)
  station retries **pause**, so channel hopping does not knock the phone off
  the setup page.
- When the station connects and the AP has no clients, the AP shuts down.
- The network scan becomes asynchronous (`WiFi.scanNetworks(true)`); results
  are kept in the existing `ScanEntry` table and served by `/api/wifi/scan`.
  The no-antenna warning (empty scan) is kept.
- Portal Save still saves and reboots, as today. Only the control page path
  applies WiFi live.

### Live WiFi change from the control page

`POST /api/wifi` saves the **candidate** credentials in RAM, not NVS, and
enters `TRYING_NEW`: `WiFi.begin(candidate)`. If connected within 30 s, the
candidate is committed to NVS, `lastResult` = `connected`. Otherwise the
previous credentials are restored, the station reconnects to them, and
`lastResult` = `not-found` / `auth` / `timeout` (from the disconnect reason
where the driver gives one). The page shows the note that if it stops
responding the board has moved networks and the phone must follow it.

### Live broker change

`POST /api/mqtt` saves to NVS immediately (a wrong broker cannot strand the
board; the page still works), calls `s_mqtt.setServer()` with the new values,
disconnects, and lets the normal retry reconnect. `lastResult` is set from the
PubSubClient state: `connected`, `refused` (bad credentials), `unreachable`
(TCP failed), `timeout`.

## 7. Tooling: OTA confirm proves the build id

`Build-Flash.ps1 -Ota`'s confirm stage currently proves only that the board
rebooted and answers the OTA probe. Add a second check after the probe: GET
`http://<host>/status.json`, read `build`, compare with the build id the
verify stage pinned. On match the receipt says **DELIVERED (over OTA)** with
`proven: running build id matches`; on mismatch it **FAILS** with both ids;
if the endpoint does not answer (older firmware), it falls back to today's
weaker receipt and says why. `tools/ota_probe.py` is unchanged.

Update `.claude/skills/vddimmer-ota/SKILL.md` and `firmware/README.md`: the
"not proven: which build id is running" rows become "proven from firmware
1.1.0 onwards".

## 8. Sequencing and acceptance

Firmware builds and flashes **only** through `tools/Build-Flash.ps1`, per
CLAUDE.md and the `vddimmer-firmware` skill, from PowerShell. The van board
is updated over OTA (`-Ota 192.168.1.115`, `vddimmer-ota` skill); it needs
12 V, so acceptance on it waits for the van to be powered. A bench board on
USB is the faster loop if one is available.

Each phase is a PR to `main`, merged green, then flashed and its acceptance
list run on hardware with results in the PR.

**Phase 1: names and the lights page.** `control.cpp`, `chName` in settings
and NVS, `names` in `config`, `web.cpp` with the shared server, the page's
Lights view and Names, Status and Board tabs, the status object,
`/status.json`, mDNS HTTP record. WiFi and Broker tabs are present but show
"needs firmware 1.1 phase 2" until then. No change to the WiFi code.
- Open `http://<ip>/` from a phone; four rows; rename to Front, Kitchen,
  Bedroom; names survive a reboot.
- Dim each channel from the page; the strip on that channel follows; the
  MQTT `channel/N/state` message carries the new value (`mosquitto_sub`).
- Set a level from `mosquitto_pub` and from the serial console; the page
  shows it within 2 s.
- `config` carries `names` and still fits (`mosquitto_sub -t .../config`).
- `/status.json` reports the build id printed on the boot banner.
- Two phones open at once stay in step.
- Portal on first boot still works (no regression).

**Phase 2: WiFi state machine, portal fallback, live re-provisioning.**
- Router off at boot, then on: the board joins within one retry interval;
  serial `?` answers while it is retrying (proves `loop()` is unblocked).
- Wrong stored SSID: the setup network appears after 3 failures; correcting
  it from a phone recovers the board with no USB.
- A phone on the setup network is not dropped by station retries.
- From the control page, change WiFi to a wrong password: within 30 s the
  board is back on the old network and `lastResult` says `auth`.
- From the control page, change the broker to a wrong host: the page keeps
  working; `lastResult` says `unreachable`; change it back; `mqtt.up` true.
- WiFi, MQTT and the page run for an hour; `freeHeap` on the Status tab is
  flat.

**Phase 3: OTA confirm.** `Build-Flash.ps1 -Ota` against the van board on
phase-1 firmware ends with `proven: running build id matches`. Docs updated.

## 9. Out of scope

- Bluetooth, the native app, and the protocol library: sections 3–5 of the
  September design. Not cancelled; the control refactor, status object and
  state machine here are what they would build on.
- Any PIN or authentication. Stuart's ruling for now. The September design's
  per-board PIN remains the obvious addition if the van WiFi is ever shared.
- Scenes, an all-off row, night mode, strip colour on the page.
- Tunable white (`hw/VDDimmer/FUTURE-FEATURES.md`).
- Any VanDaemon backend or frontend change, including adopting `names`.
- Rev B hardware (BTN1, wall switch input).

## 10. Left to the builder

- Exact HTML/CSS/JS of the page within the layout agreed; the mockups are in
  `.superpowers/brainstorm/381-1794758216/content/` (gitignored, on this
  machine only) and described in section 3.
- How the `PROGMEM` page is kept readable in source (a raw string literal in
  its own `web_page.h` is the expected answer).
- Whether `WebServer` handles the two-phone case without request queueing
  problems; if it drops requests under the 2 s poll from two clients, the poll
  interval may be raised to 3 s without coming back to Stuart.
- Disconnect-reason mapping for `lastResult` on this core version.
- `FW_VERSION` bump and build size check against `app0` (6.25 MB; no risk).
