# VDDimmer: set up a new machine to build and flash the van's board

Paste the block below into a fresh Claude Code session on the new machine, opened at
the root of a clone of `https://github.com/StuartF303/vandaemon.git` on `main`. Clone it
to `C:\Projects\vandaemon`: several older notes in this folder hardcode that path, and it
keeps the toolchain paths short. The prompt
walks Claude through installing the firmware toolchain, proving it with a hardware-free
build, then flashing the Rev A board fitted in the van over USB-C.

Nothing here needs KiCad, Konnect or the .NET SDK. Those are for PCB design and for
running VanDaemon itself; see `HANDOFF.md` ("Working from another machine") if you want
them too.

---

## PROMPT

```
Set up this machine to build and flash VANDIMMER-4CH+2A firmware, then flash the board
in the van. Work through the steps in order and stop only where a step says to.

CONTEXT
  Repo: this clone of StuartF303/vandaemon, branch main. Firmware is in
  hw/VDDimmer/firmware/ (PlatformIO, Arduino-ESP32 3.x via the pioarduino fork,
  pinned in platformio.ini). The board is Rev A, git tag `ordered-revA`, an
  ESP32-S3-WROOM-1U. It is fitted in the van and powered from the van's 12 V.
  Read hw/VDDimmer/firmware/README.md first, then invoke the `vddimmer-firmware`
  skill. It ships with the repo under .claude/skills/ and is binding.

THE ONE RULE
  Build and flash only with hw/VDDimmer/firmware/tools/Build-Flash.ps1, from
  PowerShell. Never a bare `pio run -t upload`, and never from Git Bash: the ESP-IDF
  tool installer refuses MSYS and fails later with a misleading
  "'xtensa-esp32s3-elf-g++' is not recognized". On the original dev machine a raw
  build could fail while reporting success and then flash a stale image; the
  script's gates exist because that happened.

STEP 1  Check what is installed
  - Git for Windows (git --version).
  - PowerShell may run local scripts: Get-ExecutionPolicy -Scope CurrentUser must not
    be Restricted (the Windows default), or Build-Flash.ps1 fails before its preflight
    with "running scripts is disabled". Fix:
      Set-ExecutionPolicy -Scope CurrentUser RemoteSigned
    Git-cloned files carry no mark-of-the-web, so RemoteSigned is enough.
  - Python 3.11 or 3.12 from python.org, visible to the `py` launcher (py -0).
    If missing:  winget install Python.Python.3.12
    Do not use a Python under 3.10 (esptool 5 needs 3.10+), the Microsoft Store
    Python, or one bundled with another application.
  - No user-level IDF_PATH:  [Environment]::GetEnvironmentVariable('IDF_PATH','User')
    must be empty. A stale one makes the toolchain install abort with an unrelated
    error. If set, run Build-Flash.ps1 -FixEnvironment later; it asks before removing.
  - No PlatformIO IDE, Arduino IDE or global `pio` is needed. Ignore any that exist.
  Report what you found before continuing.

STEP 2  Create the PlatformIO virtual environment
  Build-Flash.ps1 looks for the interpreter at exactly
  $env:USERPROFILE\.platformio-venv\Scripts\python.exe and prints these same
  commands if it is missing:
    py -3.12 -m venv $env:USERPROFILE\.platformio-venv      # or -3.11, whichever py -0 showed
    & "$env:USERPROFILE\.platformio-venv\Scripts\python.exe" -m pip install --upgrade pip
    & "$env:USERPROFILE\.platformio-venv\Scripts\python.exe" -m pip install platformio "click<8.2"
  The click pin is not optional: esptool 5.0 breaks on click 8.2 and the build dies
  at bootloader.bin with a TypeError. Confirm with
    & "$env:USERPROFILE\.platformio-venv\Scripts\python.exe" -m platformio --version

STEP 3  Prove the toolchain with a hardware-free build
    cd hw\VDDimmer\firmware
    .\tools\Build-Flash.ps1 -NoFlash
  The first run downloads the platform, toolchain and libraries (several hundred MB,
  a few minutes). Expected end state: "Build verified. -NoFlash set, stopping here."
  with firmware.bin, bootloader.bin, partitions.bin and a build id listed.
  The preflight also tests os.spawnve. On the original machine it was broken at the
  OS level and tools/scons_spawn_fix.py works around it. If this machine reports it
  healthy, leave the workaround wired in anyway; it is harmless.
  If anything is red, stop and diagnose. Do not re-run hoping. The traps section of
  firmware/README.md lists every failure seen so far with the symptom it presents as.

STEP 4  Flash the board in the van
  Before touching anything, confirm with me that the board is powered: it has NO USB
  power path, so it must have 12 V on J1/J2 from the van or it will not enumerate at
  all. Then connect USB-C from this machine to the board. Windows 10/11 binds the
  built-in usbser driver; no driver install.
    .\tools\Build-Flash.ps1              # add -Port COMx if more than one port shows
  Expected end state: DELIVERED, with the build id, sha256 and port. The confirm
  stage resets the board and reads its boot banner. While the chip is held in
  download mode and at each reset the gate inputs float, so the four lamp outputs
  may come on or flicker for the duration of the upload; README.md documents the
  power-on flash as expected on Rev A. Warn whoever is in the van.
  If the confirm stage times out, re-run with -ConfirmTimeout 30 before suspecting
  the flash. If "flashed image is byte-identical" fails, the build id pin is not
  reaching tools/build_id.py; stop and report.

STEP 5  Verify the board came back
  Status LED D7: green means WiFi and MQTT are up, cyan means WiFi up but no broker,
  blue means connecting, purple means it has no WiFi credentials and has opened its
  setup AP. If the mosquitto clients are installed here and a broker is reachable:
    mosquitto_sub -h <broker> -t 'vandaemon/leddimmer/#' -v -W 10
  should show a retained `status online`, a `config` message and six
  `channel/N/state` values. Report the LED colour and what MQTT showed.

DO NOT
  - Change the board's WiFi settings without reading "Getting back into the portal"
    in firmware/README.md. On Rev A a wrong SSID strands the board: no button
    recovery, no portal fallback. The only way back is erasing NVS over USB:
      & "$env:USERPROFILE\.platformio-venv\Scripts\python.exe" "$env:USERPROFILE\.platformio\packages\tool-esptoolpy\esptool.py" --chip esp32s3 --port COMx erase-region 0x9000 0x5000
    esptool is not pip-installed in the venv; the pioarduino platform fetches it as
    the tool-esptoolpy package, so `python -m esptool` fails with "No module named
    esptool".
    That keeps the deviceId (it is derived from the MAC), so VanDaemon's saved
    controls survive, but WiFi, MQTT and strip settings must be re-entered through
    the portal at 192.168.4.1 on the VANDIMMER-xxxx AP.
  - Use the 4ch2a-ota environment unless the board is already on the network. It
    exists in platformio.ini but has not been exercised through Build-Flash.ps1, and
    the script's confirm stage is serial-only. USB is the proven route.
  - Remove tools/scons_spawn_fix.py or the extra_scripts lines in platformio.ini.

WHEN DONE
  Tell me: Python version used, PlatformIO version, whether spawnve was healthy
  here, the build id that was delivered, and the LED colour afterwards. If this
  machine needed anything not covered above, add it to firmware/README.md's traps
  section and to the vddimmer-firmware skill, with the symptom it presented as, and
  commit that on a branch.
```
