# Quickstart: Build & Deploy the VanDaemon Pi-5 Appliance

> Build runs on **Linux / WSL2 / Docker** — pi-gen does NOT run natively on Windows.
> On-hardware steps are **Class C** (human-verified); this quickstart shows the flow, not a pass claim.

## 0. One-time CI setup (publishes the app images)

Push to `main` (or tag) triggers `.github/workflows/publish-images.yml`, which buildx-publishes
`ghcr.io/<owner>/vandaemon-api` and `-web` as `linux/amd64,linux/arm64`. Make the packages public
(or provide `GHCR_TOKEN` in the boot config for a private pull fallback).

## 1. Build the appliance image (Linux/WSL2/Docker host)

```bash
cd deploy/pi
cp config/pi-gen-config.example config/pi-gen-config   # edit IMG_NAME etc. (no secrets)
./build.sh                                              # wraps pi-gen Docker build; pulls+saves arm64 images
# → produces deploy/pi/deploy/<IMG_NAME>.img (flashable, arm64)
```

## 2. Configure first boot (single file, no interactive setup)

After flashing (next step) the FAT boot partition contains `vandaemon-config.txt.example`. Copy it to
`vandaemon-config.txt` and fill in hostname / WiFi / SSH pubkey / (optional) broker auth. See
`contracts/boot-config.md`.

## 3. Flash the NVMe (bench machine + USB-NVMe adapter)

```bash
# Raspberry Pi Imager CLI (or use the GUI):
rpi-imager --cli <IMG_NAME>.img /dev/sdX        # /dev/sdX = the USB-NVMe adapter — DOUBLE-CHECK!
# then edit /boot partition's vandaemon-config.txt before first boot
```

## 4. Enable NVMe boot on the Pi 5 (one-time firmware — Class C, human)

```bash
sudo raspi-config        # Advanced → Bootloader → NVMe/USB boot order
# or: sudo rpi-eeprom-config --edit   → set BOOT_ORDER=0xf416 (try NVMe, then SD)
```
This is firmware on the Pi, NOT in the flashed image — it cannot be baked in. Reversible.

## 5. Boot & verify (Class C checklist — see pi-appliance-setup.md)

```bash
ssh <user>@<HOSTNAME>.local
docker ps                                   # api, web, mqtt all Up (healthy)
curl -f http://localhost:5000/health        # healthy
curl -f http://localhost:8080/              # web UI served
mosquitto_sub -h <HOSTNAME>.local -t '$SYS/#' -C 1   # broker reachable on 1883
# power-cycle → confirm all services return automatically
```

## Local dev (unchanged path)

```bash
docker compose up            # full stack incl. broker, from the consolidated root file
# or bare-metal:
cd src/Backend/VanDaemon.Api && dotnet run     # still uses appsettings.json localhost broker
```

## Update procedure

- **Fast**: on the appliance, `cd /opt/vandaemon && docker compose pull && docker compose up -d`.
- **Full**: re-build the image and re-flash (for OS/base changes).

## Backup

```bash
# on the appliance — back up the persistent volumes
docker run --rm -v vandaemon_api-data:/d -v "$PWD":/b alpine tar czf /b/api-data.tgz -C /d .
docker run --rm -v vandaemon_mqtt-data:/d -v "$PWD":/b alpine tar czf /b/mqtt-data.tgz -C /d .
```
