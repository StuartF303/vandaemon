# SD-installer (fallback path) — VanDaemon Pi-5 Appliance

> **This is the SECONDARY / fallback path.** The **primary** install is flashing the NVMe directly on a
> bench machine via a USB-NVMe adapter — see
> [`docs/deployment/pi-appliance-setup.md`](../../../docs/deployment/pi-appliance-setup.md). Use this SD
> path only when you have **no bench machine / no USB-NVMe adapter** and must image the NVMe from inside
> the Pi itself.

## What it does

1. You write the prebuilt appliance image **and** `install-to-nvme.sh` onto a microSD.
2. The Pi boots from the SD.
3. `install-to-nvme.sh` writes the appliance image onto the NVMe (`/dev/nvme0n1`), guarding against
   ever targeting an SD/MMC device.
4. You remove the SD and reboot — the Pi now boots from NVMe into the same appliance the primary path
   produces.

> microSD is used here **only as a transient installer**, never as the running medium (ADR-001 SD-rot).

## Procedure

1. Build the appliance image (`deploy/pi/build.sh`).
2. Flash a microSD with a minimal Raspberry Pi OS Lite, then copy onto its boot partition:
   - the appliance image as `vandaemon-appliance.img`
   - `install-to-nvme.sh`
3. Ensure the Pi-5 EEPROM `BOOT_ORDER` will try SD first, then NVMe (one-time firmware step — see the
   verification checklist).
4. Boot the Pi from the SD and run, as root:
   ```bash
   sudo ./install-to-nvme.sh /boot/firmware/vandaemon-appliance.img
   # non-interactive: ASSUME_YES=1 NVME_TARGET=/dev/nvme0n1 sudo -E ./install-to-nvme.sh <img>
   ```
5. Remove the SD and reboot. Verify per the operator guide.

## Safety

- Refuses any target that is not `/dev/nvme*`.
- Refuses if the NVMe device is absent (HAT/drive not seated).
- Requires typing `yes` unless `ASSUME_YES=1`.
- **On-hardware execution is Class C** — it is run and verified by a human, never by the build loop.
