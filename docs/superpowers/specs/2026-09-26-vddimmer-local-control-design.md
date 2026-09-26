# VDDimmer local control: portal fallback, status page, Bluetooth, phone app

**Date:** 2026-09-26 · **Status:** design approved in conversation, awaiting spec review
**Branch:** `vddimmer-rev-b` (the firmware exists only on this branch; it is not on `main`)
**Hardware:** VANDIMMER-4CH+2A Rev A. Nothing here needs a board change.

## 1. Purpose

The lights must work when the Raspberry Pi is off, crashed or absent, and when
there is no network at all. Today the board is controllable only over MQTT,
whose broker runs on the Pi, so both cases leave the lights uncontrollable
except by BTN2. A wrong WiFi SSID also strands the board until NVS is erased
over USB.

### Success criteria

1. With the Pi and the van router both off, a paired phone can switch and dim
   every channel on every board in range.
2. A board with wrong WiFi credentials can be re-provisioned without USB,
   either from the app over Bluetooth or from the fallback setup portal.
3. Any browser on the van network can see a board's health at
   `http://<deviceId>.local/`.
4. A level change from any source (VanDaemon, app, another phone, button,
   serial) reaches every other connected client.
5. Nobody within Bluetooth range can pair with a board without both the PIN and
   access to its power during the pairing window.

### Decisions taken

| Decision | Choice |
|---|---|
| Fallback vector | Bluetooth LE direct to the board; MQTT cannot help when the Pi is the thing that is down |
| Platforms | iPhone and Android |
| App's relationship to VanDaemon | Dimmer remote only: lights, no tanks or electrical |
| App v1 features | Channel dimming and on/off; channel names; board setup over Bluetooth; status readout |
| Deferred | Addressable strip colour, scenes, all-off, store publishing |
| Pairing security | Pairing window after power-up **and** a passkey PIN; default PIN `123456` for development, randomised per board for production |
| Board web page | Read-only status; its only write is "Forget all phones", gated by the PIN |
| Bluetooth protocol | JSON command channel mirroring the MQTT contract |
| App stack | .NET MAUI; iOS builds via the user's Mac |

## 2. Firmware: networking, portal fallback, status page

### Non-blocking WiFi

`wifiConnect()`'s 20 s busy-wait in `net.cpp` is replaced by a state machine
polled from `loop()`: `CONNECTING -> UP -> LOST -> RETRY_WAIT -> CONNECTING`.
Retry delay backs off 5 s, 10 s, 20 s, then holds at 30 s. `loop()` never
blocks on WiFi, so buttons, serial, Bluetooth and the status LED keep working.

`WiFi.setSleep(false)` is removed. ESP-IDF requires WiFi modem sleep while
Bluetooth is enabled and errors or aborts otherwise. The cost is MQTT latency
of typically under 300 ms, acceptable for lights.

### Portal fallback

- With no stored SSID the portal starts at boot, as today.
- With a stored SSID, after **3 consecutive failed connection attempts** the
  portal starts in `WIFI_AP_STA` mode and the station keeps retrying behind it.
- The AP shares the station's channel, so station retries hop channels and
  knock a phone off the setup page. **While any client is associated with the
  AP (`WiFi.softAPgetStationNum() > 0`), station retries pause.**
- When the station connects and no client is on the AP, the AP shuts down and
  the board runs normally.
- The network scan becomes asynchronous (`WiFi.scanNetworks(true)`), so startup
  no longer blocks on it. The scan list and the no-antenna warning are kept.
- **Save requires the PIN** whenever an SSID is already stored. First-time
  setup, with no SSID stored, does not ask for one.
- Save still reboots. That is acceptable in the portal; only the Bluetooth
  path applies WiFi changes live (section 3).

### Status page

Served on the van network at `http://<deviceId>.local/`. ArduinoOTA already
brings up mDNS; add an `_http._tcp` service record.

| Route | Method | Purpose |
|---|---|---|
| `/` | GET | Human status page; refreshes itself every 5 s |
| `/status.json` | GET | The **status object** (below), identical to the Bluetooth `status` reply |
| `/forget-phones` | POST, field `pin` | Clears every Bluetooth bond; 403 on a wrong PIN |

The page shows identity, firmware version and build ID, uptime, WiFi SSID and
RSSI, MQTT connected or not, input voltage, board temperature, derate and
over-temperature state, the 4 channel levels with their names, and the number
of bonded phones. It never shows a password or the PIN.

One `WebServer` instance serves the portal routes while the AP is active and
the status routes while the station is up. `portal.cpp` keeps the setup form;
a new `webstatus.cpp` owns the status page.

### Status object

One shape, produced by one function, used by `/status.json` and by Bluetooth:

```json
{"t":"status","deviceId":"vandimmer-a1b2c3","deviceName":"Galley dimmer",
 "fw":"1.1.0","build":"a9e0823b 2026-09-12T14:54Z","uptime":3600,
 "wifi":{"ssid":"VanNet","up":true,"rssi":-61},"mqtt":true,
 "vin":12.8,"tempC":34.5,"derate":1.0,"overTemp":false,"bonds":2}
```

It must stay under 240 bytes so it fits one Bluetooth notification at a
247-byte MTU. The field names are deliberately short for that reason.

### New settings (NVS)

| Key | Type | Default | Notes |
|---|---|---|---|
| `pin` | 6 digits | `123456` | Bluetooth passkey, portal Save, `/forget-phones` |
| `chName[0..3]` | string, 20 chars max | `""` | Empty means the UI shows "Channel N" |
| `pairWindowSec` | uint16 | `120` | `0` = pairing allowed any time, with the PIN |

Channel names are also added to the MQTT `config` message as `"names":[...]`.
Today's `MqttLedDimmerPlugin` ignores unknown fields, so this breaks nothing,
and VanDaemon can adopt the names later. **Check the retained `config` message
still fits `MQTT_BUFFER_BYTES` (512).**

## 3. Firmware: Bluetooth service and protocol

### Stack and advertising

NimBLE on Arduino-ESP32 3.x. The plan pins the exact library, NimBLE-Arduino
2.x or the core's bundled BLE, whichever builds cleanly against the pinned
pioarduino platform. Expected cost is 50-70 KB of heap against 51 KB used of
320 KB.

The board advertises one custom 128-bit service UUID, with `deviceName` as the
GAP name. It accepts at most **3 simultaneous connections** and keeps
advertising while below that.

### Security

- LE Secure Connections, bonding, MITM protection. The board declares IO
  capability *DisplayOnly* and uses the stored `pin` as a static passkey, so
  the phone's OS prompts for it.
- New pairings are accepted only while the pairing window is open: the first
  `pairWindowSec` seconds after power-up, or always when that setting is 0. A
  pairing attempt outside the window is refused.
- Both characteristics require an encrypted, authenticated link. Nothing is
  readable before pairing.
- At most **8 bonds**. When the table is full, pairing is refused with a reason;
  no existing phone is evicted.
- Forget all phones: Bluetooth `forget-phones`, the status page POST, and a new
  serial console command `forget-phones`.
- Production: the PIN is derived per board from eFuse data (like `deviceId`,
  so an NVS erase does not change it) and printed on the board label or
  enclosure. The derivation is out of scope here, but no board leaves the bench
  on `123456` with `pairWindowSec = 0`.

### GATT

One service, two characteristics:

| Characteristic | Properties | Direction |
|---|---|---|
| `rx` | Write, Write Without Response; encrypted and authenticated | phone -> board |
| `tx` | Notify; encrypted and authenticated | board -> phone |

Every message is one JSON object in one ATT packet. The board requests a
247-byte MTU and every message is sized to fit; there is no fragmentation
layer. Requests may carry an `id`, echoed in the reply.

### Requests

| `op` | Fields | Reply |
|---|---|---|
| `get` | | `{"t":"state","deviceId","deviceName","levels":[4],"names":[4]}` |
| `set` | `ch` 0-3, `level` 0-255 | `level` notifications |
| `all` | `level` 0-255 | `level` notifications |
| `name` | `ch`, `name` | `{"t":"ok"}` |
| `devname` | `name` | `{"t":"ok"}` |
| `status` | | the status object |
| `scan` | | one `{"t":"ap","ssid","rssi","ch","open"}` per network, then `{"t":"scan-done","n"}` |
| `wifi` | `ssid`, `pass` | `{"t":"wifi","result":"connected"\|"failed","reason"}` |
| `mqtt` | `host`, `port`, `user`, `pass`, `base` | `{"t":"mqtt","result":"connected"\|"failed","reason"}` |
| `identify` | | `{"t":"ok"}`; the status LED blinks for about 5 s |
| `forget-phones` | | `{"t":"ok"}`, then the link drops |
| `reboot` | | `{"t":"ok"}`, then the board reboots |

`wifi` and `mqtt` are applied **live**. The new values are saved, the state
machine restarts, and the result is reported once it connects or gives up
(30 s timeout). Because the Bluetooth link survives, the app can show
"connected" or "wrong password" instead of losing the board to a reboot.
`reason` values: `not-found`, `auth`, `timeout`, `refused`.

Errors: `{"t":"err","id":...,"msg":"..."}`. Unknown `op`, out-of-range values
and malformed JSON all produce one; none crash or reboot the board.

**Passwords are write-only.** No reply, notification, page or log line ever
contains a WiFi password, MQTT password or the PIN.

### Unsolicited notifications

`{"t":"level","ch":2,"level":128}` goes to every connected phone whenever a
channel level changes, whatever the source. This is what keeps phones,
VanDaemon and the board consistent.

### The control refactor

The "level changed" fan-out is currently split between `main.cpp`
(`persistAndPublish`) and `net.cpp`. A new `control.cpp` becomes the single
entry point:

```cpp
void ctl_setLevel(uint8_t ch, uint8_t level);  // PWM, NVS stage, MQTT state, BLE notify
void ctl_setAll(uint8_t level);
void ctl_allOff();
```

MQTT, Bluetooth, buttons and the serial console all call it. `ble.cpp` owns
only transport and security and hands parsed JSON to shared command handlers.
Those handlers are also where the MQTT `cmd` topic lands, so the two
transports cannot drift apart.

### Firmware module map after this work

```
control.*    single point of state change and fan-out        (new)
ble.*        NimBLE service, security, bonds, pairing window  (new)
commands.*   JSON op handlers shared by BLE and MQTT cmd      (new)
webstatus.*  status page, /status.json, /forget-phones        (new)
net.*        WiFi state machine, MQTT, OTA, mDNS              (reworked)
portal.*     setup form, AP_STA fallback, PIN on save         (reworked)
main.cpp     setup/loop, serial console                       (slimmed)
```

## 4. The app

### Layout

- `src/Mobile/VanDimmer.Protocol`: a `net10.0` class library with message
  records, JSON serialisation and an `IDimmerLink` transport interface. No
  MAUI dependency. **Included in `VanDaemon.sln`**, tested by
  `tests/VanDimmer.Protocol.Tests` (xUnit + FluentAssertions).
- `src/Mobile/VanDimmer.App`: the .NET MAUI app with its own
  `VanDimmer.App.sln`. **Not in `VanDaemon.sln`**, because MAUI workloads and
  the Mac requirement would break the Linux Docker and CI builds. Plugin.BLE
  for transport, CommunityToolkit.Mvvm for view models, XAML views, dark theme
  close to VanDaemon's.

### Screens

1. **Lights (home).** One card per known board with its 4 channels by name,
   each with an on/off toggle and a slider. Each card shows connecting,
   connected or out of range. Unknown dimmers in range are listed below under
   "Add a dimmer".
2. **Add a dimmer.** Tap a board; the OS pairing dialog asks for the PIN. If
   the board refuses because its window has closed, the app says: "Switch the
   dimmer off and on at its breaker, then try again within 2 minutes."
3. **Board settings.** Rename the board and its channels; status block; WiFi
   setup (scan list, password, live result); MQTT setup; Identify; Forget all
   phones.

### Behaviour

- Connect to known boards on foreground; disconnect on background. No
  background control.
- Sliders send Write Without Response at most every 50 ms while dragging, then
  the final value with response. The UI moves immediately, then follows the
  board's `level` notifications.
- An on/off toggle remembers the last non-zero level per channel in the app,
  switching between 0 and that level, or 255 if there was none.
- Known boards (`deviceId` plus the platform peripheral ID) are kept in MAUI
  Preferences. No account, no cloud.

### Platform requirements

- iOS never exposes a MAC address; boards are keyed by `deviceId` from `get`.
- iOS starts pairing automatically on first access to an encrypted
  characteristic; Android does the same, with `createBond` as a fallback.
- Android 12+: `BLUETOOTH_SCAN` (with `neverForLocation`) and
  `BLUETOOTH_CONNECT`. Android 11 and older: location permission for scanning.
- iOS `Info.plist`: `NSBluetoothAlwaysUsageDescription`.

### Distribution

Development only: Android by sideloaded APK, iOS through the Mac with Xcode
provisioning. TestFlight and store publishing are a later decision.

## 5. Testing and sequencing

Firmware builds and flashes **only** through `tools/Build-Flash.ps1`, per
CLAUDE.md. Each phase ends with its acceptance checks run on the Rev A board,
with results recorded in the phase's commit message.

**Phase 0: get the bench board onto the network.** The Rev A board has never
joined WiFi. Fit the U.FL antenna, provision it through the portal and confirm
VanDaemon lists its channels. Every later phase depends on it.

**Phase 1: control refactor and non-blocking WiFi.** No new features.
- VanDaemon sliders, BTN2 and the serial console still drive the channels, and
  each change is published once to MQTT.
- With the router off, serial commands respond during WiFi retries (proves
  `loop()` no longer blocks).

**Phase 2: portal fallback, PIN, status page.**
- Wrong SSID: the portal appears after 3 failures; correcting it recovers the
  board with no USB.
- Router off at boot, then on: the board joins and the AP disappears.
- A phone on the AP is not dropped by retries.
- Save with a wrong PIN is refused when an SSID is stored.
- `/status.json` matches the status object and is under 240 bytes.
- `/forget-phones` with a wrong PIN returns 403.
- The MQTT `config` message carries `names` and still fits the buffer.

**Phase 3: Bluetooth service.** Verified with nRF Connect on a phone, with no
app.
- Pairing inside the window with `123456` succeeds; after the window it is
  refused; a bonded phone reconnects after the window.
- Reading or writing before pairing fails.
- Every request in section 3 gives the documented reply.
- A level set over MQTT notifies a connected phone.
- `wifi` with a wrong password returns `failed` / `auth` and the link survives.
- 3 phones connect; a 4th is refused; bond table full refuses the 9th.
- WiFi, MQTT and Bluetooth run together for an hour with no reboot, and heap
  stays flat on the status page.

**Phase 4: protocol library.** xUnit round-trip tests for every request and
reply in section 3, against golden JSON fixtures taken from the Phase 3
captures, so the library is tested against what the firmware actually sends.

**Phase 5: app on Android**, sideloaded from Windows, for the fastest loop.
Then **Phase 6: app on iOS** through the Mac. For each platform: pair, control
all channels on two boards, rename, re-provision WiFi, and confirm VanDaemon
sees the app's changes.

## 6. Out of scope

- Addressable strip colour and effects in the app, scenes, all-off.
- Any VanDaemon backend or UI change (adopting `names` is a later step).
- A wall-switch input: a Rev B hardware consideration, the fallback that needs
  no phone at all.
- Production PIN derivation and labelling.
- App store publishing.
