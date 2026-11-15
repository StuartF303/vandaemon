# VANDIMMER-4CH+2A firmware

ESP32-S3 firmware for the VanDaemon 4-channel PWM dimmer with two addressable
LED outputs. Targets **Rev A**, git tag `ordered-revA` (`37c12ab`).

This is new code. The 8-channel sketch at `hw/LEDDimmer-8ch/led_dimmer.ino` was
a reference only — its pin map does not match this board, it has no MQTT, and
its README describes a PlatformIO project that never existed.

## Four things about this board that shape the firmware

**Channels are low-side N-FETs behind a buffer.** GPIO HIGH = channel ON. U4 is
a 74HCT125 on +5 V driving the gates through 220 R.

**U4's `/OE` pins are strapped to GND**, so the buffer is always enabled, and
`GATE_IN1..4` have no pulldown — R5–R8 sit on the gate side and cannot fight a
push-pull output. While the ESP32 is in reset those four pins float, so **a
brief flash at cold power-on is expected and is not a fault**. `pwm_preinit()`
is the first thing `setup()` calls and latches the pads with `gpio_hold_en()`,
which closes the window on every subsequent soft reset.

**The addressable outputs are single-wire.** R11/R12 (47 R) are DNP, so the
clock never reaches J7.3/J8.3 — these are WS2812-style data-only outputs.
Worth knowing before anyone plans an APA102 retrofit: R11 and R12 are both fed
from the *same* buffer output, so even fully fitted that is one clock shared by
both strips, not two independent ones.

**There is no USB power path.** USB-C is data and ESD only. The board must have
12 V on J1/J2 to run, flash, or even enumerate.

## Building

PlatformIO, Arduino framework, Arduino-ESP32 **3.x** (the `ledcAttach` 3-argument
API and the S3 RMT driver both need it). `platformio.ini` pins the pioarduino
platform fork because Espressif's own PlatformIO platform stalled at core 2.0.x;
`board_pins.h` has a `#error` guard so a wrong platform fails loudly.

**Build and flash with `tools/Build-Flash.ps1`, not a bare `pio run`.**

```powershell
cd hw/VDDimmer/firmware

.\tools\Build-Flash.ps1                  # build, verify, flash, confirm on hardware
.\tools\Build-Flash.ps1 -NoFlash         # build and verify only
.\tools\Build-Flash.ps1 -Clean -Port COM4
```

This is not a convenience wrapper. `os.spawnve` is broken OS-wide on the
development machine (see the traps below) and SCons routes every build action
through it, so `pio run` could **fail while reporting success** -- producing
`bootloader.bin` and `partitions.bin` but no `firmware.bin`, after which an
upload flashes whatever stale image was already there and says nothing. The
script deletes `firmware.bin` before building so its absence proves failure,
pins the build identity so the upload cannot quietly rebuild something else,
and confirms over serial that the board runs the binary just produced.

Use **PowerShell**, never Git Bash -- `idf_tools.py` refuses MSYS.

Underneath it runs, roughly:

```
pio run -e 4ch2a                 # build
pio run -e 4ch2a -t upload       # flash over USB  (12 V MUST be applied)
pio device monitor -e 4ch2a      # 115200 baud

pio run -e 4ch2a-ota -t upload --upload-port vandimmer-a1b2c3.local
```

To clear stored settings without a reflash -- the only way back into the portal
on Rev A, see *First boot* -- erase just the NVS partition:

```powershell
python -m esptool --chip esp32s3 --port COM4 erase-region 0x9000 0x5000
```

If native USB auto-download ever misbehaves, `EN` and `IO0` are both broken out
on J13 (pins 5 and 6): hold J13.6 to GND, pulse J13.5 to GND, release.

Verified build, 2026-09-05: RAM 15.6% (51,040 of 327,680), flash 16.9%
(1,106,022 of 6,553,600), zero warnings from these sources.

### Environment traps on Windows

Three unrelated things broke the toolchain install before it worked. None is
in the firmware, and each fails in a way that does not name the real cause.

1. **Do not run `pio` from Git Bash / MSYS.** `idf_tools.py` hard-refuses that
   environment (`ERROR: MSys/Mingw is not supported`), and the platform runs it
   with stdout and stderr discarded — so all you see is `idf_tools.py
   installation failed` and later `'xtensa-esp32s3-elf-g++' is not recognized`.
   Build from PowerShell or cmd.
2. **A stale `IDF_PATH` poisons the installer.** Any leftover ESP-IDF pointer —
   this machine had a user-level `IDF_PATH=d:\esp32\esp-idf-v3.3.1` on a drive
   that no longer exists — makes `idf_tools.py` abort with "It is not possible
   to determine the IDF version". Clear it for the build, or delete it.
3. **`esptool` 5.0.0 needs `click` < 8.2.** Click 8.2 changed
   `ParamType.get_metavar()` to take a `ctx` argument, so the bootloader
   `elf2image` step dies with a `TypeError` and the build reports only
   `*** [bootloader.bin] Error 1`.

Also note the toolchain is *not* a normal PlatformIO package: the registry zip
is a 1.6 kB descriptor, and `idf_tools.py` fetches the real GCC. A
`toolchain-xtensa-esp-elf` directory containing only `package.json` and
`tools.json` means that fetch failed, whatever the log claimed.

4. **`os.spawnve` is broken on this Windows build (11, 26200).** Any call
   passing an environment dies with `0xC0000005`, measured identically under
   CPython 3.6.8 and 3.11.5, with and without a real console attached. It is
   not the venv, not `pythonw`, and not console-related; padding the
   environment changes the *failure mode* rather than curing it, which is the
   signature of heap corruption. SCons routes every build action through it, so
   builds died partway with no `firmware.bin`, and every `cmd.exe` that did
   launch allocated its own console window -- a full rebuild opened ~190 and
   took the machine down.

   Fixed permanently by `tools/scons_spawn_fix.py`, wired in through
   `extra_scripts` in `platformio.ini`. It patches
   `SCons.Platform.win32.spawnve` only, leaving SCons' own argument escaping
   untouched, and adds `CREATE_NO_WINDOW`. **Do not remove it** unless the
   preflight in `Build-Flash.ps1` reports `spawnve` healthy.

   The trap inside that fix: never hand argv to `subprocess` as a list.
   `list2cmdline()` re-quotes SCons' already-escaped `cmd /C` argument with
   backslash escapes, which cmd.exe cannot parse -- it exits 1 printing
   *nothing*, surfacing as a bare `Error 1` with no compiler diagnostics.
   `VANDIMMER_SPAWN_DEBUG=<file>` logs every spawn if this needs re-diagnosing.

An earlier note here blamed KiCad's `pythonw.exe` for the console storm. That
was wrong, and it cost a session chasing the venv instead of the OS.

## First boot

With no stored WiFi credentials the board opens an unsecured AP named
`VANDIMMER-<mac suffix>` with a captive-portal setup form covering identity,
WiFi, MQTT, strip length and supply, and behaviour. Save reboots the board.

Before switching to AP mode it **scans for networks** and lists them on the
form with channel and signal strength; tapping one fills the SSID box. That
list is the quickest diagnosis of a board that will not associate, because
configuration happens from a phone or tablet where the serial log is not
visible. Two things it makes obvious:

- **Nothing listed at all** means no antenna. U1 is an `ESP32-S3-WROOM-1U`,
  which has **no PCB antenna** -- only a U.FL connector. Without one fitted the
  receiver is deaf: on the first Rev A board the scan found a single AP at
  -94 dBm, the noise floor, and the requested SSID did not appear.
- **A network you expected is missing** usually means it is 5 GHz-only. The
  ESP32-S3 is 2.4 GHz only, so such an AP is invisible and the failure reads as
  `NO_AP_FOUND` rather than an authentication error.

### Getting back into the portal

**On Rev A there is no button route.** `BTN_BOTH_LONG` needs BTN1, and J11.1 is
one of the unrouted pads, so the "hold both buttons for five seconds" recovery
does not exist on this hardware. The MQTT `forget-wifi` command only works when
MQTT is already reachable, which it is not when the WiFi details are wrong.

`net_tick()` also has **no portal fallback**: when WiFi never comes up,
`s_wifiUp` is already false so the retry block never arms, and the board sits
red indefinitely. The repeating `NO_AP_FOUND` lines are the ESP-IDF driver's
own scanning, not the application retrying.

So a wrong SSID strands the board until NVS is erased over USB (see *Building*).
A nuisance on the bench; a dead board in the van, where there is no USB.
**Rev B needs the BTN1 pad routed, a portal fallback after N failed attempts,
or both.**

## Identity

Three distinct fields, easily confused:

| Field | Unique? | Purpose |
|---|---|---|
| `deviceId` | **yes** | MQTT topic level, and the prefix of every VanDaemon control ID (`{deviceId}-CH0`) |
| `deviceName` | ideally | Human label; the backend renders controls as "*{deviceName}* — Channel 3" |
| `variant` | no | Type name, `4CH2A`, identical on every board of this design |

`deviceId` defaults to `vandimmer-<last 6 hex of the WiFi MAC>` and is derived
from the MAC rather than stored, so erasing NVS will not change it and orphan
the controls VanDaemon has already persisted to `controls.json`. It must stay a
single MQTT topic level (no `/`), and its last hyphen-separated segment must not
begin with `CH` — the plugin's `SetStateAsync` splits control IDs on `-` and
reads the tail as the channel.

## MQTT

Base topic `vandaemon/leddimmer`, so every topic below is prefixed
`vandaemon/leddimmer/{deviceId}/`.

| Topic | Dir | Payload |
|---|---|---|
| `status` | pub, retained | `online` / `offline` (last will) |
| `config` | pub, retained | `{"deviceId","deviceName","channels","version","variant"}` |
| `channel/{N}/state` | pub, retained | **bare integer 0-255** |
| `channel/{N}/set` | sub, QoS 1 | integer 0-255, or `{"brightness":N}` |
| `heartbeat` | pub, 60 s | `{"uptime","freeHeap","rssi"}` |
| `telemetry` | pub, retained | `{"vin","tempC","derate","overTemp"}` — extension |
| `addr/{1\|2}/state` | pub, retained | `{"on","brightness","rgb":[r,g,b],"length","supply5v"}` — extension |
| `addr/{1\|2}/set` | sub, QoS 1 | same shape; any subset of the fields |
| `cmd` | sub, QoS 1 | `reboot` / `identify` / `republish` / `forget-wifi` / `status-bright <0-255>` — extension |

Channel numbers are **0-based**: `channel/0` is physical channel 1.

Everything marked *extension* is ignored by today's backend and exists so a
future colour-strip module has something already shaped for it.

### Four constraints the backend imposes

These come from reading `MqttLedDimmerPlugin.cs`, not the old README. Each one
silently produces a board that connects and does nothing if you get it wrong.

1. **`config` is mandatory.** Seeing only `status: online` makes the plugin
   record the device with `Channels = 0`, and `MqttLedDimmerService` loops
   `channel < Channels` — zero controls registered, nothing in the UI.
2. **`config` and `status` must be retained**, or a backend that starts after
   the board never hears the announcement.
3. **`channel/{N}/state` must be a bare integer.** The handler is
   `int.TryParse`; JSON is silently dropped.
4. **Channel numbers are 0-based on the wire.**

One further hazard the firmware works around: `HandleConfigMessage` *replaces*
the plugin's device record, discarding its cached channel states, so retained
`/state` messages that arrive before `/config` are lost. Every state is
republished immediately after config and again with each heartbeat, so the
backend converges no matter what order the broker delivers retained messages in.

### The two addressable outputs

The plugin has no concept of a colour strip — it only knows `channel/N`. By
default `exposeStripsAsChannels` is **on**, so the config message declares
**6** channels and CH4/CH5 map to per-strip master brightness. The strips then
appear as ordinary dimmers in VanDaemon with no backend change.

**Turn this off once a dedicated colour-strip module exists**, or the same
hardware ends up with two sets of controls — and delete the two orphaned
"Channel 5"/"Channel 6" entries from `controls.json` once when you do. The
colour and effect capability is on the `addr/` topics either way.

## Behaviour

**Dimming.** 1.2 kHz (spec asks 1–2 kHz), 12-bit LEDC, with a CIE 1931 gamma
curve mapping the 0–255 wire value onto duty. Perceptually even steps and a far
finer low end than a linear ramp — at the cost of "50 %" in the UI no longer
meaning 50 % duty. Both the frequency and the gamma curve are settings.

**Buttons.** BTN1 short: toggle all channels off / back to their previous
levels. BTN2 short: cycle 25 / 50 / 75 / 100 % across all channels. Both held
5 s: clear WiFi credentials and reboot into the portal.

> **Rev A: BTN1 does not exist.** J11.1 is unrouted, so anything needing BTN1 --
> the off/restore toggle and the both-held credential reset -- is dead on this
> hardware. Only BTN2 works. See *Getting back into the portal*.

**Boot state.** Channel levels and strip colours are restored from NVS at
power-up (`restoreOnBoot`, default on). Writes are debounced to one flush every
five seconds so a slider drag does not chew through NVS endurance.

**Protection.** Board temperature from RT1 derates the PWM channels linearly
from 70 °C and holds them off at 85 °C, recovering below 65 °C — this protects
the board and the 5 A cap. Input voltage is measured and published but **never
acted on**; the leisure battery's own BMS handles under-voltage.

**5 V strip power cap.** When a strip is marked as fed from 5 V, the firmware
estimates its draw and scales brightness to stay under ~1.8 A, so a long strip
dims rather than tripping the 2 A polyfuse behind a wall panel. Spec §6.5 puts
the honest limit at ~33 LEDs at full white, ~100 mixed-colour. Set the checkbox
to match the actual J9/J10 shunt position — the firmware cannot detect it.

**Status LED** (D7): dim white boot, magenta portal, blue WiFi, cyan MQTT,
green ready, yellow button, orange over-temperature, red fault.

Brightness is the `statusBrightness` setting, a 0-255 master scale over those
colours, default 64 (25 %) because a WS2812B viewed directly on a bench is
glaring. It is a setting rather than a constant because the right level is a
judgement you make by looking at it, and on Rev A revisiting a constant would
need a reflash *and* an NVS erase. Adjust it live, persisted immediately:

```
mosquitto_pub -t vandaemon/leddimmer/<deviceId>/cmd -m "status-bright 32"
```

A non-zero channel never scales to zero, so a low setting cannot turn the red
of a fault into black.

## Bring-up order

1. 12 V on J1/J2 before anything else — nothing works without it.
2. Confirm the serial banner and the `[adc]` line: `vin` should read within
   ~0.2 V of the bench supply, board temperature near ambient. If `vin` reads
   about 11× wrong, R27/R28 are the wrong way round.
3. Confirm the status LED lights. If not, D7 or U5 is suspect.
4. **Fit the U.FL antenna.** U1 is a WROOM-1U with no PCB antenna; without one
   the board associates with nothing.
5. Portal to WiFi and broker. The scan list on the form should show your network
   at a sane level (roughly -40 to -70 dBm indoors); one or two entries near
   -90 dBm means the antenna is not seated. Watch for `[mqtt] connected`.
6. `mosquitto_sub -t 'vandaemon/leddimmer/#' -v` and confirm retained `config`
   and four (or six) `channel/N/state` messages.
7. One channel at a time into a resistive load before any LED strip, checking
   the right physical channel responds to `channel/0..3/set`. Do this
   deliberately: **U5's buffer channels 3 and 4 were swapped during layout**, so
   a channel driving the wrong output is a known hazard, not paranoia.
8. Strips last, with the J9/J10 shunt position checked twice against the
   silkscreen. On Rev A the **12 V position of J9/J10 is dead** (J9.1/J10.1 are
   unrouted), so the strips must be 5 V parts -- a WS2815 or any 12 V
   addressable strip will not be powered at all.

## Layout

```
platformio.ini      envs 4ch2a (USB) and 4ch2a-ota
src/main.cpp        setup/loop, button actions
src/board_pins.h    netlist-derived pin map + the hardware notes above
src/settings.h      the settings struct
src/store.*         NVS via Preferences, MAC-derived identity, debounced writes
src/pwm.*           LEDC, gamma table, gate-safe init, thermal derate
src/strips.*        NeoPixelBus over RMT: status pixel + 2 outputs
src/telemetry.*     VIN and NTC scaling, over-temperature policy
src/buttons.*       debounce, short press, two-button hold
src/portal.*        captive-portal setup AP
src/net.*           WiFi, OTA, MQTT, the contract above
```

`board_pins.h` carries the netlist table and the `kicad-cli` command that
produced it. Re-derive it rather than trusting it — the GPIO map changed twice
during layout, and the spec's §7 pin table is the original plan and is wrong.
