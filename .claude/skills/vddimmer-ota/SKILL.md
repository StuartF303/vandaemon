---
name: vddimmer-ota
description: |
  Updates VANDIMMER-4CH+2A firmware over WiFi, with no USB and no opening the case.
  Use when: flashing, updating or reflashing a VDDimmer board that is installed in the van;
  diagnosing a board that answers ping but is silent on MQTT; or deciding whether OTA is
  usable at all before planning physical access.
allowed-tools: Read, Edit, Write, Glob, Grep, Bash, PowerShell
---

# VDDimmer OTA — updating the sealed board

The board lives in a closed case in the van. OTA is the normal update path; USB is the
fallback that costs a disassembly. Build and flash guarantees are shared with
`vddimmer-firmware` — read that skill for the build side, the `spawnve` defect and the
toolchain. This one covers the WiFi transport only.

## The rule

```powershell
cd C:\Projects\vandaemon\hw\VDDimmer\firmware
.\tools\Build-Flash.ps1 -Ota 192.168.1.115     # build, verify, push over WiFi, re-probe
.\tools\Build-Flash.ps1 -NoFlash               # build + verify, touch nothing
.\tools\Build-Flash.ps1 -Port COM6             # USB fallback
```

`-Ota` and `-Port` are different transports; pass only one. PowerShell, never Git Bash.

## Before assuming OTA is available

Two preconditions, and the second is not obvious:

1. The board is powered (12 V on J1/J2 — **there is no USB power path**) and on the van WiFi.
2. Its firmware is **`b9e6812` or later**. Earlier images can be associated and pingable
   with no OTA listener at all, permanently. See the stranding defect below.

`Build-Flash.ps1 -Ota` probes for the listener in preflight, before spending ~2.5 minutes
building, and fails with the diagnosis rather than the symptom.

## Traps that cost hours

- **ArduinoOTA listens on UDP 3232, not TCP.** A TCP scan reports `closed` on a perfectly
  healthy board. This sent one session to the wrong conclusion entirely — it "proved" OTA
  was dead on a board that was fine. The only valid test is the espota handshake:
  `python tools\ota_probe.py --host <ip>`. `OK` or `AUTH <nonce>` means listening; silence
  means it is not.
- **Never flash with `pio run -e 4ch2a-ota -t upload`.** `4ch2a-ota` is a separate
  PlatformIO environment with its own build directory, so it *rebuilds*: the script
  verifies and hashes `.pio/build/4ch2a/firmware.bin`, then that command would upload a
  different binary from `.pio/build/4ch2a-ota/`. That is the verified-one-thing-flashed-
  another failure every gate in `Build-Flash.ps1` exists to prevent. Stage 4 calls
  `espota.py` directly on the artifact VERIFY hashed. The env survives in
  `platformio.ini` for manual use and is not the supported path.
- **`ota_probe.py` is a write, not a read — never sweep a subnet with it.** On receiving
  the invitation the board calls `Update.begin()`, which erases its *inactive* OTA
  partition, then fails to connect back to the host port the tool deliberately does not
  serve, and aborts. Harmless to the running image, but it is not a ping.
- **mDNS names are unreliable from Windows.** `vandimmer-6f9914.local` would not resolve
  via `Resolve-DnsName` even with the board healthy and OTA live. Use the IP. The board
  calls `WiFi.setHostname(deviceId)`, so the router's DHCP table is the practical place
  to find it — **and the right place to pin a static lease**, since `-Ota` takes an
  address and has no discovery.
- **OTA has no password, deliberately.** `ArduinoOTA.setPassword()` is never called, so
  anyone on the van WiFi can reflash the board. Accepted: a forgotten or mismatched
  password means no OTA, which forces opening the case — the exact outcome OTA exists to
  avoid. Revisit when the 2026-09-26 design's per-board PIN lands. Do not add one
  casually; it changes the recovery story.

## The stranding defect (fixed in b9e6812 — recognise it in older images)

**Symptom: status LED cyan, board answers ping, silent on MQTT, silent on UDP 3232.**

`wifiConnect()` busy-waits `WIFI_TIMEOUT_MS` (20 s). If the AP has not associated by then,
`net_begin()` returns early — before `ArduinoOTA.begin()` and `s_mqtt.setServer()`.
ESP-IDF's driver keeps retrying on its own and usually associates seconds later, but
`net_tick()`'s recovery block is guarded on `WiFi.status() != WL_CONNECTED`, which is now
false, so it never re-enters `net_begin()`. The board runs `ArduinoOTA.handle()` on a
listener that was never begun and `mqttConnect()` against a server never set.

Every remote route on Rev A is gone at once: BTN1 is the unrouted J11.1 pad, `forget-wifi`
needs the broker it cannot reach, there is no portal fallback, and OTA never started. USB
is the only way back.

Fixed by a latched `startNetServices()`, called from `net_tick()`'s connected path as well
as `net_begin()`. The latch never resets, including on WiFi loss: `ArduinoOTA::begin()` is
itself guarded by `_initialized` and returns early, so re-calling it after a reconnect
would not rebind the socket — a resetting latch would imply a recovery it cannot deliver.

**Consequence for planning:** a board on pre-`b9e6812` firmware is a coin flip on every
power cycle. Bootstrapping the fix needs one USB flash; after that, power cycles are safe.

## What the OTA confirm stage does and does not prove

Weaker than the serial path, and it says so in its own output.

| Proven | How |
|---|---|
| The image arrived intact | espota validates MD5 **on the device** before switching boot partitions |
| A corrupt transfer cannot boot | `Update` aborts rather than switching; board keeps its previous firmware |
| The board rebooted and is reachable | OTA re-probe answers after the reboot — this is what makes the *next* update possible |

| Which build id is running | **From firmware 1.1.0:** the confirm stage reads `build` from the board's `/status.json` and fails on a mismatch. On older firmware the endpoint is absent and the receipt says `unproven  the running build id`. |

Before 1.1.0 that gap was validated once by hand, on the van board 2026-11-15: an OTA
push was followed by `verify_boot.py` over USB, which matched the delivered build id
exactly. The first OTA *to* 1.1.0 is still the weaker receipt (the old image has no
status endpoint when confirm runs); every one after it is proven.

## Diagnosing a board that will not take an OTA

Work down this ladder; each step is cheap and rules out a class.

| Check | Command | Reading |
|---|---|---|
| On the network? | `ping <ip>` | No reply → unpowered, or a blade fuse. The board has no USB power path. |
| Right device? | `Get-NetNeighbor` → MAC | `deviceId` is `vandimmer-<last 6 hex of WiFi MAC>` |
| OTA listening? | `python tools\ota_probe.py --host <ip>` | Silence + cyan LED + ping OK → the stranding defect; use USB |
| Web API healthy? | `python tools\web_check.py --host <ip>` | 15/15 on 1.1.0+; a 404 on `/api/state` means pre-1.1.0 firmware |
| What is it running? | `http://<ip>/status.json` in any browser | `build` is the BUILD_ID; `mqtt.lastResult` says why the broker is not connected |
| On the bus? | subscribe `vandaemon/leddimmer/#` | Needs the board's **configured** broker, which may not be the only one on the LAN |

Status LED (D7): dim white boot, **purple** portal, blue WiFi, **cyan** MQTT-waiting,
green ready, yellow button, orange over-temperature, red fault.

A cyan board is healthy at the WiFi layer. Do not chase WiFi when the LED says cyan —
chase the broker, or the stranding defect.

## Known board

| Field | Value |
|---|---|
| IP | `192.168.1.115` (van WiFi `JCDammVan`) — pin a DHCP reservation |
| WiFi MAC | `1C:DB:D4:6F:99:14` (not in common Espressif OUI lists; do not filter by OUI) |
| deviceId | `vandimmer-6f9914` |
| Configured broker | **unknown** — the startup line that prints it was missed twice; no way to read it back remotely |

Note the only MQTT broker seen on that LAN is the **Victron Cerbo GX** at `192.168.1.2`
(topics `N/c0619ab50946/...`), which carries no `vandaemon/leddimmer/*` topics. The Pi that
runs the VanDaemon stack was not on the network. A silent board there is expected, not a
fault.

## Keeping this current

When an OTA problem is diagnosed **to root cause and verified fixed**, add it to *Traps*
with the symptom it presents as — the symptom is what a future session searches for. If a
claim here is disproven, replace it and say so. Keep it to what is verified on this
hardware.
