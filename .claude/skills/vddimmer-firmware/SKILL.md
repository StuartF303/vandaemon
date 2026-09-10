---
name: vddimmer-firmware
description: |
  Builds, flashes and verifies VANDIMMER-4CH+2A ESP32-S3 firmware in hw/VDDimmer/firmware/.
  Use when: building, compiling, uploading, flashing or deploying VDDimmer firmware; debugging
  a board that will not boot or is running the wrong image; or ANY PlatformIO build on this
  machine, where os.spawnve is broken OS-wide and a raw `pio run` can fail while reporting success.
allowed-tools: Read, Edit, Write, Glob, Grep, Bash, PowerShell
---

# VDDimmer Firmware — Build & Deploy

## The rule

**Always build and flash with `tools/Build-Flash.ps1`. Never run a bare `pio run -t upload`.**

```powershell
cd C:\Projects\vandaemon\hw\VDDimmer\firmware
.\tools\Build-Flash.ps1                 # build, verify, flash, confirm on hardware
.\tools\Build-Flash.ps1 -NoFlash        # build + verify only
.\tools\Build-Flash.ps1 -Clean -Port COM4
.\tools\Build-Flash.ps1 -FixEnvironment # also offers to clear the stale user IDF_PATH
```

This is not a convenience wrapper. On this machine a raw `pio run` **fails while reporting
success**, and a following upload then flashes whatever stale image is lying around, with
nothing in the output saying so. Every gate in the script exists because that happened.

Use the **PowerShell tool**, not Bash — Git Bash is MSYS and `idf_tools.py` hard-refuses it,
failing later with a misleading `'xtensa-esp32s3-elf-g++' is not recognized`.

## Why — the machine defect

`os.spawnve()` is broken OS-wide on this box (Windows 11 build 26200). Any call passing an
environment dies with `0xC0000005`:

| call | result |
|---|---|
| `os.spawnv` (no env) | works |
| `os.spawnve` with `{}`, `{'A':'B'}` or `os.environ` | ACCESS_VIOLATION |
| same under CPython 3.6.8 **and** 3.11.5 | identical |
| same with a real console attached | identical |

Not the venv, not `pythonw`, not console-related, no injected DLLs. Padding the environment
changes the *failure mode* rather than curing it, which is heap corruption — hence the
intermittency. SCons routes **every** build action through `spawnve`, so builds died partway
leaving `bootloader.bin` and `partitions.bin` but no `firmware.bin`, while consoleless
`cmd.exe` spawns each allocated a window (~190 of them once took the PC down).

Fixed permanently by `tools/scons_spawn_fix.py`, wired in via `extra_scripts = pre:` in
`platformio.ini`. **Do not remove it** unless the preflight self-test reports spawnve healthy.

## What the script guarantees

1. **Preflight** — interpreter, `IDF_PATH` cleared, re-tests `spawnve`, and **aborts if the
   workaround is not registered**. Auto-cleans a 0-byte `.sconsign*.dblite`.
2. **Build** — deletes `firmware.bin` first, so its absence afterwards proves failure whatever
   the exit code claims. Pins `VANDIMMER_BUILD_ID` so build and upload yield the same binary.
3. **Verify** — artifacts present, freshly written, plausibly sized, sha256 recorded.
4. **Flash** — only if verify passed; re-hashes afterwards to prove the upload did not rebuild.
5. **Confirm** — reads the boot banner over serial and requires `[build] <id>` to match.

A red **FAIL** is real. Do not work around it — diagnose it.

## Traps that cost hours

- **Never pass argv as a list to `subprocess` in the spawn fix.** `list2cmdline()` re-quotes
  SCons' already-escaped `cmd /C` argument with backslash escapes, which cmd.exe does not
  understand — it exits 1 printing *nothing*, surfacing as a bare `Error 1` with no compiler
  diagnostics. Build the command line by hand. `VANDIMMER_SPAWN_DEBUG=<file>` logs every spawn.
- **The board has no USB power path.** Apply 12 V to J1/J2 before flashing; it will not
  enumerate from USB-C alone.
- **Stale user-level `IDF_PATH=d:\esp32\esp-idf-v3.3.1`** points at a drive that no longer
  exists and makes `idf_tools.py` abort with an unrelated-looking error.
- **`esptool` 5.0 requires `click` < 8.2.** Pin it if the venv is ever rebuilt.
- **Read the MAC from the eFuse, not `WiFi.macAddress()`**, anywhere that runs before
  `net_begin()` — it returns zeros until the WiFi driver starts, which once made every board
  call itself `vandimmer-000000`.

## Status LED (D7, one WS2812B)

| colour | meaning |
|---|---|
| dim white | booting |
| **purple** | config portal — no WiFi credentials; join its AP, browse `192.168.4.1` |
| blue | connecting to WiFi |
| cyan | waiting for MQTT |
| green | ready |
| yellow | button pressed |
| orange | over-temperature |
| red | error |

## Keeping this current

This skill is meant to grow. When a build or deploy problem is diagnosed **to root cause and
verified fixed**, update it in the same session:

- Add a durable trap to *Traps that cost hours*, with the symptom it presents as — the symptom
  is what a future session will search for, not the cause.
- If a gate is added to `Build-Flash.ps1`, record what failure it catches under *What the
  script guarantees*. Gates without a stated reason get deleted by someone later.
- If a claim here is disproven, **replace it and say so**. An earlier note blamed KiCad's
  `pythonw.exe` for the console storm; it was wrong and sent a session down a dead end.
- Keep it to what is verified on this hardware. No speculative advice.

Related memories: `pio-spawnve-broken-windows`, `vandimmer-firmware-delivery-gate`,
`esp32-s3-build-environment`.
