# VDDimmer Web Control, Phase 1: names, lights page, status, OTA confirm

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The board serves its own lights page on the van LAN with four named,
individually dimmable channels, stores the names, publishes them over MQTT, and
exposes a status object the OTA tooling uses to prove the running build id.

**Architecture:** A new `control.cpp` is the single fan-out for level changes
(PWM, NVS stage, MQTT state). A new `web.cpp` runs a synchronous `WebServer`
on port 80 once WiFi is up, serving a JSON API and one self-contained HTML page
that is embedded at build time from `src/web/index.html`. A new `status.cpp`
builds the status object used by `/api/status`, `/status.json` and
`Build-Flash.ps1`. **No change to the WiFi connect code in this phase**; the
state machine, portal fallback and live WiFi change are Phase 2, planned
separately after this phase is proven on the van board.

**Tech Stack:** Arduino-ESP32 3.x on pioarduino 54.03.21, `WebServer`,
`ESPmDNS`, `ArduinoJson` 7, `PubSubClient`; plain HTML/CSS/JS, no framework;
Python 3 for the build-time embed script, the desktop mock server and the
on-board API check; PowerShell for `Build-Flash.ps1`.

**Spec:** `docs/superpowers/specs/2026-11-15-vddimmer-web-control-design.md`
(sections 3, 4, 5, 7 and the Phase 1 list in 8). Section 6 is Phase 2.

## Global Constraints

- Build and flash **only** via `hw/VDDimmer/firmware/tools/Build-Flash.ps1`
  from PowerShell (`-NoFlash` for build+verify). Never a bare `pio run`, never
  from Git Bash. Expected success line: `Build verified.`
- The van board is sealed; flash it with `-Ota 192.168.1.115` only when the van
  has 12 V. It runs `ef18200f-dirty`, which has the OTA listener.
- No authentication on any route (Stuart's ruling, 2026-11-15).
- No change to `wifiConnect()`, `net_tick()`'s WiFi branches, or `portal.cpp`'s
  AP behaviour in this phase.
- Channel names: at most 20 bytes, UTF-8, stored per PWM channel; empty means
  "Channel N" in the UI and `""` on the wire.
- MQTT `config` must still fit `MQTT_BUFFER_BYTES` (512); its serialisation
  buffer becomes 384 bytes.
- `FW_VERSION` becomes `"1.1.0"`.
- Request bodies over 512 bytes are rejected with 413.
- Commit messages end with `Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>`.
- Every firmware source change ends with `.\tools\Build-Flash.ps1 -NoFlash` passing.

## Review Focus

Inputs the spec implies but no acceptance item exercises; each has a test in
the owning task.

1. A channel name containing `<`, `"` or `&` must render literally on the page,
   not as markup. (Task 5: page escapes text via `textContent`, never
   `innerHTML` with user data.)
2. A name longer than 20 bytes, or a 4-byte emoji that would be split at byte
   20, must be rejected with 400, not truncated into invalid UTF-8. (Task 4
   `web_check.py` and Task 5 mock test.)
3. `POST /api/channel/2` with `{"level":"abc"}`, `{"level":300}`, or an
   unparsable body must return 400 and leave the level unchanged. (Task 4.)
4. `POST /api/names` with three names, or with `names` missing, must return 400
   and change nothing. (Task 4.)
5. Two browsers polling every 2 s plus a slider drag must not make
   `WebServer` drop requests or starve `net_tick()`; `freeHeap` must be flat
   over ten minutes. (Hardware acceptance, Task 7; the mock cannot show this.)

---

### Task 1: `control.cpp`, the single fan-out for level changes

**Files:**
- Create: `hw/VDDimmer/firmware/src/control.h`, `hw/VDDimmer/firmware/src/control.cpp`
- Modify: `hw/VDDimmer/firmware/src/main.cpp` (remove `persistAndPublish`, call `ctl_*`), `hw/VDDimmer/firmware/src/net.cpp:188-206` (`handleChannelSet` PWM branch)

**Interfaces:**
- Produces:
  ```cpp
  void ctl_setLevel(uint8_t ch, uint8_t level);  // ch < PWM_CHANNELS; pwm_set, store_stageLevels+markDirty, net_publishChannelState(ch)
  void ctl_setAll(uint8_t level);                // all four, one publish per channel
  void ctl_allOff();                             // ctl_setAll(0)
  ```
  Out-of-range `ch` is ignored. `s_allOff` and `s_lastOnLevels` stay in
  `main.cpp`; `main.cpp` keeps setting `s_allOff` where it did.

- [ ] **Step 1: Create `control.h`/`control.cpp` with the three functions above.** `ctl_setLevel` does exactly what the PWM branch of `handleChannelSet` plus `persistAndPublish` did, for one channel.
- [ ] **Step 2: Replace `persistAndPublish()` in `main.cpp`.** Button and console handlers call `ctl_setAll`, `ctl_allOff`, `ctl_setLevel`; the BTN1 restore loop calls `ctl_setLevel(i, s_lastOnLevels[i])`. Delete `persistAndPublish`.
- [ ] **Step 3: In `net.cpp` `handleChannelSet`, the `channel < PWM_CHANNELS` branch becomes `ctl_setLevel(channel, value); return;`** (the publish is inside). Strip branch unchanged.
- [ ] **Step 4: Build.** Run `.\tools\Build-Flash.ps1 -NoFlash` from `hw/VDDimmer/firmware` in PowerShell. Expected: `Build verified.`
- [ ] **Step 5: Commit** `refactor(vddimmer): route every level change through control.cpp`.

### Task 2: Channel names in settings, NVS and the MQTT config message

**Files:**
- Modify: `src/settings.h` (add `chName`, bump `FW_VERSION`), `src/store.cpp:45-102` (load/save), `src/net.cpp:70-83` (`publishConfig`), `src/net.h`

**Interfaces:**
- Produces:
  ```cpp
  // settings.h
  static constexpr uint8_t CH_NAME_MAX = 20;            // bytes, excluding NUL
  char chName[PWM_CHANNELS][CH_NAME_MAX + 1] = {{0}};   // in Settings
  // net.h
  void net_republishConfig();   // no-op when MQTT is down
  ```
- NVS keys `chn0`..`chn3`, string, default `""`.

- [ ] **Step 1: Add `CH_NAME_MAX` and `chName` to `Settings`; set `FW_VERSION` to `"1.1.0"`.**
- [ ] **Step 2: Load and save `chn0..3` in `store_begin()` and `store_saveSettings()`** using the existing `getStr`/`putString` pattern with a `snprintf`-built key.
- [ ] **Step 3: `publishConfig()` adds `names`:** a JSON array with one entry per `net_declaredChannels()`; PWM entries are `g_settings.chName[i]`, strip entries are `"Strip 1"`, `"Strip 2"`. Grow `payload` to 384 and add `static_assert(384 <= MQTT_BUFFER_BYTES)`. Log `[mqtt] config %u bytes` once per publish so the hardware test can read the size.
- [ ] **Step 4: Add `net_republishConfig()`** that calls `publishConfig()` when `s_mqtt.connected()`.
- [ ] **Step 5: Build.** `.\tools\Build-Flash.ps1 -NoFlash`. Expected: `Build verified.`
- [ ] **Step 6: Commit** `feat(vddimmer): store channel names on the board and publish them in config`.

### Task 3: The status object

**Files:**
- Create: `src/status.h`, `src/status.cpp`
- Modify: `src/net.h`, `src/net.cpp` (two accessors)

**Interfaces:**
- Consumes: `telemetry_vinVolts/tempC/derate/overTemp`, `net_wifiUp`, `net_mqttUp`, `g_settings`, `BUILD_ID`, `FW_VERSION`.
- Produces:
  ```cpp
  // status.h
  size_t status_json(char *buf, size_t len);   // returns bytes written; 0 if it did not fit
  // net.h
  const char *net_mqttResult();   // "connected" | "refused" | "unreachable" | "timeout" | "unset" | "idle"
  ```
  `net_mqttResult()` maps the last `PubSubClient::state()`: 0 `connected`,
  5 or 4 `refused`, -2 `unreachable`, -4 `timeout`; `unset` when
  `mqttHost` is empty; `idle` before the first attempt.
- Output shape (spec section 3, Phase 1 fields; `wifi.lastResult` is Phase 2):
  ```json
  {"t":"status","deviceId":"","deviceName":"","fw":"1.1.0","build":"<BUILD_ID>",
   "uptime":0,"wifi":{"ssid":"","up":true,"rssi":-58,"ip":"192.168.1.115"},
   "mqtt":{"host":"","port":1883,"up":false,"lastResult":"refused"},
   "vin":12.6,"tempC":34.5,"derate":1.0,"overTemp":false,"freeHeap":212000}
  ```
  `vin` rounded to 2 dp, `tempC` to 1 dp, as `publishTelemetry()` does.

- [ ] **Step 1: Implement `net_mqttResult()`** in `net.cpp`, recording `s_mqtt.state()` after each `mqttConnect()` attempt.
- [ ] **Step 2: Implement `status_json()`** with `JsonDocument` and `serializeJson` into the caller's buffer; a 512-byte buffer is the expected caller size.
- [ ] **Step 3: Build.** Expected: `Build verified.`
- [ ] **Step 4: Commit** `feat(vddimmer): one status object for the web API and the OTA tooling`.

### Task 4: `web.cpp`, the JSON API, mDNS record, and an on-board API check

**Files:**
- Create: `src/web.h`, `src/web.cpp`, `tools/web_check.py`
- Modify: `src/net.cpp` (`startNetServices()` calls `web_begin()`, adds `MDNS.addService("http","tcp",80)`; connected path of `net_tick()` calls `web_tick()`), `src/main.cpp` (include)

**Interfaces:**
- Consumes: `ctl_setLevel`, `status_json`, `net_republishConfig`, `store_saveSettings`, `status_blink(STATUS_BUTTON, 5000)`, `store_saveLevelsNow`.
- Produces:
  ```cpp
  void web_begin();   // idempotent; constructs WebServer(80), registers routes, begin()
  void web_tick();    // handleClient(); also fires the deferred reboot
  ```
- Routes, all `application/json`:

  | Route | Method | Behaviour |
  |---|---|---|
  | `/api/state` | GET | `{"deviceId","deviceName","levels":[4],"names":[4]}` from `pwm_levels()` and `g_settings` |
  | `/api/status` | GET | `status_json` |
  | `/status.json` | GET | same handler |
  | `/api/channel/<n>` | POST | body `{"level":N}`; `n` 0-3 and `N` 0-255 integers else 400; `ctl_setLevel`; reply `{"ok":true,"level":N}` |
  | `/api/names` | POST | body `{"deviceName":"","names":["","","",""]}`; `names` must be an array of exactly 4 strings each ≤ 20 bytes, `deviceName` ≤ 39 bytes, else 400; save, `store_saveSettings()`, `net_republishConfig()`; `{"ok":true}` |
  | `/api/mqtt` | POST | body `{"host","port","user","pass"}`; blank `pass` keeps the stored one; `port` 1-65535; save; `net_mqttReconfigure()` (new: `setServer` + `disconnect`, retry loop does the rest); `{"ok":true}` |
  | `/api/identify` | POST | `status_blink(STATUS_BUTTON, 5000)`; `{"ok":true}` |
  | `/api/reboot` | POST | `store_saveLevelsNow`; `{"ok":true}`; `ESP.restart()` 500 ms later from `web_tick` |
  | `/api/wifi`, `/api/wifi/scan` | any | `501 {"error":"phase 2"}` |
  | `/` | GET | Task 5; until then `200 text/plain "VANDIMMER web: page not built"` |
  | anything else | | `404 {"error":"not found"}` |

  Bodies over 512 bytes: `413 {"error":"too large"}`. Bad JSON:
  `400 {"error":"bad json"}`. The route for `/api/channel/<n>` uses
  `WebServer`'s `UriBraces` (`/api/channel/{}`) and `pathArg(0)`.

- `tools/web_check.py --host <ip>`: Python 3, stdlib only (`urllib`). Runs
  the checks below against a live board and exits 0 only if all pass; prints
  one line per check. Runs under the venv Python like `ota_probe.py`.

- [ ] **Step 1: Write `tools/web_check.py`** with these checks, each a function returning pass/fail text:
  - `GET /api/state` → 200, `levels` and `names` are length-4 arrays
  - `GET /status.json` → 200, has `build` and `fw == "1.1.0"`
  - `POST /api/channel/1 {"level":77}` → 200; `GET /api/state` shows `levels[1] == 77`; then restore the prior value
  - `POST /api/channel/1 {"level":300}` → 400, level unchanged
  - `POST /api/channel/1 {"level":"abc"}` → 400
  - `POST /api/channel/7 {"level":1}` → 400
  - `POST /api/channel/1` with body `not json` → 400
  - `POST /api/names` with `{"deviceName":"T","names":["a","b","c"]}` → 400, names unchanged
  - `POST /api/names` with a 21-byte name → 400
  - `POST /api/names` with a name of five `€` (15 bytes) → 200 and read back identical; then restore prior names
  - `POST /api/channel/1` with a 600-byte body → 413
  - `GET /nothing` → 404
  - `POST /api/wifi {}` → 501
- [ ] **Step 2: Run it against the mock from Task 5 later; for now, `python tools/web_check.py --help` prints usage.**
- [ ] **Step 3: Implement `web.cpp`** per the table. Reading the body: `server.arg("plain")`, check `server.hasArg("plain")` and length ≤ 512 first. UTF-8 names: byte length check only (a well-formed JSON string from the browser is valid UTF-8; reject if `strlen > CH_NAME_MAX`).
- [ ] **Step 4: Hook into `net.cpp`:** `web_begin()` and `MDNS.addService` at the end of `startNetServices()`; `web_tick()` after `ArduinoOTA.handle()` in `net_tick()`. Add `net_mqttReconfigure()` to `net.h`/`net.cpp`.
- [ ] **Step 5: Build.** Expected: `Build verified.` Note RAM/flash lines from the build output in the commit message.
- [ ] **Step 6: Commit** `feat(vddimmer): JSON API on port 80 with names, levels, status and reboot`.

### Task 5: The page, embedded at build, with a desktop mock for testing

**Files:**
- Create: `src/web/index.html`, `tools/embed_web.py`, `tools/web_mock.py`
- Modify: `platformio.ini` (`extra_scripts` adds `pre:tools/embed_web.py`), `src/web.cpp` (`/` serves the page), `.gitignore` under firmware (`src/web_page.gen.h`)

**Interfaces:**
- `tools/embed_web.py` (SCons pre-script): reads `src/web/index.html`, writes `src/web_page.gen.h` containing
  `static const char WEB_PAGE[] PROGMEM = R"VDWEB(...)VDWEB";` and
  `static const size_t WEB_PAGE_LEN = N;`. Fails the build with a clear message if the HTML contains `)VDWEB"` or exceeds 24,000 bytes.
- `web.cpp` serves `/` with `server.send_P(200, "text/html; charset=utf-8", WEB_PAGE, WEB_PAGE_LEN)` and `Cache-Control: no-cache`.
- `tools/web_mock.py [--port 8765]`: stdlib `http.server`; serves `src/web/index.html` at `/` and implements every route in Task 4's table with in-memory state, same validation and status codes, `status.json` with `build` = `"mock 2026-11-15T00:00:00Z"`. Prints the URL. Exists so the page and `web_check.py` can be exercised on the PC before a board is available.

- [ ] **Step 1: Write `tools/web_mock.py`** implementing the API table; run it; `python tools/web_check.py --host localhost:8765` must pass every check. Fix the mock until it does (this is also the executable specification of the API).
- [ ] **Step 2: Write `src/web/index.html`** to the agreed design:
  - Hash routes `#lights` (default) and `#settings/<names|status|wifi|broker|board>`.
  - Lights view: header with `deviceName` and `deviceId`, gear link; one row per channel: name (or `Channel N`), switch, `<input type=range min=0 max=255>`, percentage label `Math.round(level/2.55)`; footer with RSSI, broker dot (green `mqtt.up`, cyan otherwise), `vin` V, `tempC` °C.
  - Switch off → remember level, POST 0; switch on → POST remembered or 255.
  - Slider: on `input` update label and POST at most every 100 ms; on `change` POST final; polling of `/api/state` paused while a pointer is down on any slider.
  - Poll `/api/state` every 2 s and `/api/status` every 5 s; on fetch failure show a "board not reachable" banner and keep retrying.
  - Settings opens on Names if any `names[i] === ""`, else Status. Names tab: five inputs, `maxlength=20`, Save → POST `/api/names`, toast on `ok`, inline message on 400. Status tab: table from `/api/status`. WiFi tab: message "Available in firmware 1.1 phase 2". Broker tab: form → POST `/api/mqtt`; shows `mqtt.up` and `lastResult`. Board tab: Identify, Reboot (JS `confirm()`-free: two-tap "Reboot" → "Confirm reboot"), firmware `fw` and `build` lines.
  - All user-supplied text set via `textContent` or `value`, never `innerHTML`.
  - Dark theme, 16 px base, 44 px minimum touch height, `viewport` meta. No external resources.
- [ ] **Step 3: Test in a browser against the mock:** rename channels, verify names show on Lights and survive mock reload; set a name to `<b>&"x` and confirm it renders literally; drag sliders; switch off and on restores; open the page in two tabs and confirm the second follows within 2 s; Broker tab round-trips; Board tab reboot shows two-tap. Fix until all hold.
- [ ] **Step 4: Write `tools/embed_web.py`**, add it to `extra_scripts` after `build_id.py`, gitignore `src/web_page.gen.h`, serve `/` from `web.cpp`.
- [ ] **Step 5: Build.** Expected: `Build verified.` Confirm `src/web_page.gen.h` was generated and is untracked by git.
- [ ] **Step 6: Commit** `feat(vddimmer): the lights page, embedded from src/web/index.html, with a desktop mock`.

### Task 6: OTA confirm proves the build id; docs

**Files:**
- Modify: `tools/Build-Flash.ps1:384-416` (OTA confirm), `README.md` (firmware), `.claude/skills/vddimmer-ota/SKILL.md`, `.claude/skills/vddimmer-firmware/SKILL.md`, `hw/VDDimmer/NEW-MACHINE-SETUP.md` (one line), repo `CLAUDE.md` (one line)

- [ ] **Step 1: In the OTA confirm block, after the probe succeeds,** `Invoke-RestMethod "http://$Ota/status.json" -TimeoutSec 5` with up to 5 retries 2 s apart. If it answers: compare `.build` to `$buildId`; match → receipt line `proven    running build id matches (/status.json)`; mismatch → `Fail` with both ids. If it never answers: keep today's receipt and print `unproven  running build id -- /status.json not served (firmware before 1.1.0)`.
- [ ] **Step 2: Firmware `README.md`:** new section "Web control" after "MQTT": the URL, the lights page and settings tabs, the API table from Task 4, `web_check.py` and `web_mock.py`, and that there is no PIN. Update the `config` row of the MQTT table with `names`. Update the OTA paragraph: confirm now proves the build id from 1.1.0.
- [ ] **Step 3: `vddimmer-ota/SKILL.md`:** the "Not proven" table row becomes "Proven from firmware 1.1.0 via `/status.json`; older firmware falls back to the weaker receipt". Add `web_check.py` to the diagnosis ladder. `vddimmer-firmware/SKILL.md`: one line that `src/web_page.gen.h` is generated and must not be edited.
- [ ] **Step 4: `NEW-MACHINE-SETUP.md` STEP 5:** add "open `http://<ip>/` from a phone on the van WiFi; you should see the four channels". `CLAUDE.md` hw/VDDimmer bullet: `- **Web control:** the board serves its own page on port 80; API in firmware/README.md`.
- [ ] **Step 5: Build** (`-NoFlash`) to prove the PowerShell edit parses and nothing regressed. Expected: `Build verified.`
- [ ] **Step 6: Commit** `feat(vddimmer): OTA confirm reads the running build id; document web control`.

### Task 7: Deliver, then hardware acceptance

- [ ] **Step 1: Final `-NoFlash` build on a clean tree; `git status` clean; push branch; open PR to `main`** with the Phase 1 acceptance list below in the body, unchecked, and the note that items are ticked only after running on hardware.
- [ ] **Step 2: Tell Stuart it is ready to test**, with: the mock command for a desktop look now (`python tools/web_mock.py`), and the OTA command for when the van is powered (`.\tools\Build-Flash.ps1 -Ota 192.168.1.115`).
- [ ] **Step 3: Hardware acceptance (needs 12 V on the van board):**
  - `Build-Flash.ps1 -Ota 192.168.1.115` ends `proven running build id matches` (on the first push: confirm runs after the reboot into 1.1.0)
  - `python tools\web_check.py --host 192.168.1.115` passes every check
  - Phone opens `http://192.168.1.115/`; four rows; rename to Front, Kitchen, Bedroom; reboot via Board tab; names persist
  - Dim Front from the page; the lamp follows; `mosquitto_sub -t 'vandaemon/leddimmer/vandimmer-6f9914/#' -v` shows `channel/0/state` (if a broker is configured and reachable)
  - `mosquitto_pub ... channel/1/set -m 128` and serial `ch 2 200`: page follows within 2 s
  - `config` message carries `names`; serial log shows its byte count under 384
  - Two phones stay in step; `freeHeap` on the Status tab flat over 10 minutes with both polling
  - Power-cycle the board: page returns; names intact; first-boot portal unaffected (verify on a bench board, or skip and note)
