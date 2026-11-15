# VDDimmer: firmware updates without opening the case

**Date:** 2026-11-15 · **Status:** design approved in conversation
**Hardware:** VANDIMMER-4CH+2A Rev A, fitted in the van. No board change needed.
**Firmware baseline:** `main` @ `26162ef`

## 1. Purpose

The board lives in a sealed case in the van. Every firmware update so far has
meant opening it and connecting USB-C, which is not a maintenance strategy. The
hardware already supports the alternative: ArduinoOTA is compiled in and, when
it starts, works. The problem is that it does not reliably start, and the
tooling has no supported path to use it.

### Success criteria

1. With the board powered and on the van WiFi, a firmware update completes from
   a laptop on the same network, with no physical access to the board.
2. The image delivered over OTA is byte-identical to the image that passed the
   script's verify stage — the same guarantee the USB path gives today.
3. A board that associates with WiFi *late* still ends up with OTA listening.
4. A failed OTA leaves the board running its previous firmware.

### What this is not

Not a replacement for the 2026-09-26 local-control design. That work replaces
`wifiConnect()`'s busy-wait with a proper state machine, adds a portal fallback
and a status page, and will supersede part of this. This is the narrow slice
that makes remote updates trustworthy now, shaped so it does not fight that
design later.

## 2. The defect this fixes

Reproduced on the van board, 2026-11-15, as a contrast between two boots of
identical firmware:

| Boot | UDP 3232 | Meaning |
|---|---|---|
| First | no reply | `ArduinoOTA.begin()` never ran |
| After power cycle | `OK` | `net_begin()` completed |

`net_begin()` (`src/net.cpp`) calls `wifiConnect()`, which busy-waits up to
`WIFI_TIMEOUT_MS` (20 s). If the AP has not associated by then, `s_wifiUp` is
false and `net_begin()` **returns early**, before:

```cpp
ArduinoOTA.setHostname(g_settings.deviceId);
ArduinoOTA.begin();                              // never runs
s_mqtt.setServer(g_settings.mqttHost, ...);      // never runs
```

ESP-IDF's driver keeps retrying on its own and usually associates a few seconds
later. But `net_tick()`'s recovery block is guarded by
`WiFi.status() != WL_CONNECTED`, which is now false — so it never re-enters
`net_begin()`. The board falls through to `ArduinoOTA.handle()` on a listener
that was never begun, and `mqttConnect()` against a server that was never set.

The result is a board that is powered, associated, pingable and **completely
unreachable**: no OTA, no MQTT, no mDNS, indefinitely. Every remote recovery
route on Rev A is gone at once — BTN1 is the unrouted J11.1 pad, `forget-wifi`
needs the broker it cannot reach, `net_tick()` has no portal fallback, and OTA
was never started. Only USB remains, which is the thing we are trying to avoid.

Observed symptom, for a future search: **status LED cyan, board answers ping,
nothing on MQTT, nothing on UDP 3232.**

## 3. Decisions taken

| Decision | Choice | Why |
|---|---|---|
| Update path | Laptop pushes over van WiFi | Closest to working today; needs no new subsystem |
| Fix depth | Re-entrant service init only | ~10 lines, kills the exact failure; the Sept design's state machine supersedes it cleanly |
| Verification | espota's on-device MD5, plus an OTA re-probe after reboot | No new firmware surface, and needs no broker — there is none in the van |
| OTA password | **None**, deliberately | See below |
| Board addressing | Explicit IP or hostname | mDNS did not resolve from Windows this session |
| Tooling shape | `-Ota <host>` inside `Build-Flash.ps1` | Keeps the delivery gates in one place |
| Bootstrap | One final USB flash | Shipping the fix via the mechanism it repairs is circular |

### On the absent password

`ArduinoOTA.setPassword()` is never called, so anyone on the van WiFi can
reflash the board. This is accepted for now, treating the WiFi as the trust
boundary, because the failure mode of a password is worse than the risk it
removes: a forgotten or mismatched password means no OTA, which forces opening
the case — the exact outcome this design exists to prevent. Revisit when the
Sept design's per-board PIN lands, which gives a secret that is already
provisioned, recoverable through the portal, and shared with Bluetooth pairing.

## 4. Firmware change

One concern, in `src/net.cpp`: *the post-connect services must start whenever
WiFi is up, not only when `net_begin()` happens to see it come up.*

Extract the body after the early return into one function and guard it with a
latch:

```cpp
static bool s_servicesUp = false;

static void startNetServices() {
    if (s_servicesUp) return;
    ArduinoOTA.setHostname(g_settings.deviceId);
    ArduinoOTA.begin();
    s_mqtt.setServer(g_settings.mqttHost, g_settings.mqttPort);
    s_mqtt.setBufferSize(MQTT_BUFFER_BYTES);
    s_mqtt.setCallback(onMessage);
    s_mqtt.setKeepAlive(30);
    s_servicesUp = true;
    mqttConnect();
}
```

`net_begin()` calls it when `s_wifiUp`. `net_tick()` calls it on the connected
path, before `ArduinoOTA.handle()`. The latch makes the second call free in the
common case.

**The latch never resets, including on WiFi loss.** That is deliberate, not an
oversight: `ArduinoOTA::begin()` is itself guarded by `_initialized` and logs
*"already initialized"* and returns, so re-calling it after a reconnect would
not rebind the UDP socket anyway. A latch that reset would imply a recovery it
cannot deliver. MQTT needs no re-init either — `net_tick()` already retries
`mqttConnect()` every `MQTT_RETRY_MS`.

### Why not just drop the early return

Considered and rejected. `ArduinoOTA.begin()` binds a UDP socket and, where
`CONFIG_MDNS_MAX_INTERFACES` is defined, calls `MDNS.begin()` — neither is
meaningful with no network, and the bind failure path only logs. Starting
services on the connected transition is the honest expression of the
requirement.

## 5. Tooling change

`tools/Build-Flash.ps1` gains `-Ota <host>`. Stages 1–3 are unchanged and
shared with the USB path; only the transport-specific stages differ:

| Stage | USB | `-Ota` |
|---|---|---|
| 1 Preflight | interpreter, `IDF_PATH`, `spawnve`, workaround registered | same, plus: host reachable, OTA listener answers |
| 2 Build | unchanged | unchanged |
| 3 Verify | unchanged | unchanged |
| 4 Flash | `pio run -t upload` (esptool) | `espota.py` against the **verified** `firmware.bin` |
| 5 Confirm | serial boot banner, build id must match | board drops off, comes back, OTA re-probe answers `OK` |

### The artifact trap

**Do not implement stage 4 as `pio run -e 4ch2a-ota -t upload`.** `4ch2a-ota`
is a separate PlatformIO environment with its own build directory, so that
command rebuilds the image. The script would verify
`.pio/build/4ch2a/firmware.bin`, record its sha256, and then upload a
*different* binary from `.pio/build/4ch2a-ota/` — reintroducing the
verified-one-thing-flashed-another failure the script exists to prevent.

Stage 4 therefore invokes `espota.py` directly on the artifact stage 3 hashed:

```
<venv python> <framework>/tools/espota.py -i <host> -p 3232 -f .pio/build/4ch2a/firmware.bin
```

The `4ch2a-ota` env stays in `platformio.ini` for manual use and is no longer
the supported path.

### Preflight additions, and why

A missing OTA listener is the single most likely reason an OTA push fails, and
espota's own failure for that case is slow and unclear. So preflight probes UDP
3232 **before** building, and if nothing answers it stops with the diagnosis
rather than the symptom: the board is up but its services never started, and
this build cannot reach it — use USB.

That probe is the espota invitation itself, which has a side effect worth
recording: the board erases its inactive OTA partition and aborts when no
transfer follows. Harmless, since it runs from the active partition, but it is
why the probe must not be used to sweep a subnet looking for boards.

### Addressing

`-Ota` takes an IP or a resolvable name; there is no discovery.
`vandimmer-6f9914.local` failed to resolve from Windows this session, and the
only reliable probe has the side effect above. The board does call
`WiFi.setHostname(g_settings.deviceId)`, so the router's DHCP table is the
practical way to find it — documented in the README rather than automated.

## 6. Error handling

| Failure | Behaviour |
|---|---|
| Host unreachable | Preflight stops before building |
| No OTA listener | Preflight stops, reports the stranding diagnosis, points at USB |
| Transfer interrupted | espota reports failure; MD5 never validates, boot partition unchanged, board keeps running its old firmware |
| MD5 mismatch | Same — `Update` aborts rather than switching partitions |
| Board never returns | Stage 5 fails after a bounded wait; board is almost certainly still on its old image, but say so rather than claim delivery |

The reboot window is the one unavoidable gap: lamp outputs float while the chip
resets, so channels may flash. Documented, same as the USB path.

## 7. Testing

No test harness exists for this firmware — the repo's test projects are .NET
only — so verification is by exercise on the one board, in this order:

1. `-NoFlash` build still passes unchanged (regression on the shared stages).
2. USB flash of the fixed firmware. Confirm stage must report the new build id.
3. OTA push of a trivially changed build to the same board. Stage 5 must prove
   it came back.
4. **Re-create the defect**: power-cycle until a boot loses the association
   race, then confirm OTA now answers anyway. This is the one that proves the
   fix rather than the plumbing. If the race proves hard to lose on purpose,
   temporarily lowering `WIFI_TIMEOUT_MS` forces it.
5. Confirm a failed OTA is survivable: interrupt a transfer, confirm the board
   returns on its old firmware.

## 8. Out of scope

Deferred to the 2026-09-26 local-control design: the non-blocking WiFi state
machine, the `AP_STA` portal fallback, the HTTP status page, Bluetooth and the
phone app. Also out of scope: URL-pull updates, a version manifest, signed
images, OTA to multiple boards in one command, and the `info` console command
that would have let us read back the configured broker without a reboot.
