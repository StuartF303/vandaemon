# VanDaemon Pi-5 Appliance Setup

A near-zero-touch deployment of VanDaemon onto the **ADR-001 controller** (Raspberry Pi 5, 4 GB, booting
from **NVMe** — never microSD — hosting the **Mosquitto** broker locally). You flash a prebuilt image to
the NVMe on a bench machine, drop in one config file, and the Pi boots straight into a running VanDaemon
on the van LAN. No keyboard, monitor, or interactive setup.

> **Decision record**: [`ADR-001`](adr/ADR-001-controller-soc.md) (this guide implements action items 1–3).
> **Builder details**: [`deploy/pi/README.md`](../../deploy/pi/README.md).
> **On-hardware sign-off** (Class C): [`pi-appliance-verification-checklist.md`](pi-appliance-verification-checklist.md).

## 0. Prerequisites

- Raspberry Pi 5 (4 GB) + an NVMe HAT and NVMe drive.
- A **USB-NVMe adapter** + a bench machine to flash the NVMe (primary path). No bench machine? See the
  [SD-installer fallback](#fallback-sd-installer).
- A Linux / WSL2 / Docker host to **build** the image (pi-gen does not run natively on Windows).
- The api/web images published to GHCR (the `publish-images.yml` workflow does this on push to `main`).

> **GHCR is a build-time-only dependency.** The images are pulled on the *builder* and baked into the
> image. The **appliance runs fully offline** — no internet is needed at boot (Constitution III).

## 1. Build the image

```bash
cd deploy/pi
cp config/pi-gen-config.example config/pi-gen-config     # edit IMG_NAME / locale — NO secrets
./build.sh                                               # ~tens of minutes; baked offline-ready
# → deploy/pi/pi-gen/deploy/<IMG_NAME>.img(.zip)
```

Override the registry/tag if needed: `VANDAEMON_REGISTRY=ghcr.io/<owner> VANDAEMON_TAG=<sha> ./build.sh`.

## 2. Flash the NVMe (bench machine)

Using Raspberry Pi Imager (GUI) or its CLI; the device is your **USB-NVMe adapter** — double-check it:

```bash
# list disks first and identify the adapter (NOT your system disk!)
rpi-imager --cli deploy/pi/pi-gen/deploy/<IMG_NAME>.img /dev/sdX
# (or use Imager's GUI: "Use custom image" → select the .img → choose the USB-NVMe target)
```

## 3. Drop in the first-boot config

On the flashed NVMe's **boot (FAT) partition**, copy `vandaemon-config.txt.example` to
`vandaemon-config.txt` and fill in what you need (all fields optional):

| Field | Purpose | Default if blank |
|-------|---------|------------------|
| `HOSTNAME` | system + mDNS name (`<HOSTNAME>.local`) | `vandaemon` |
| `WIFI_SSID` / `WIFI_PASSWORD` / `WIFI_COUNTRY` | WiFi (omit for wired DHCP) | Ethernet/DHCP |
| `SSH_PUBKEY` | key-based SSH login for `vandaemon` | no key added |
| `MQTT_AUTH` | `true` enables broker password+ACL auth | `false` (anonymous on trusted LAN) |
| `MQTT_USERNAME` / `MQTT_PASSWORD` | broker creds when `MQTT_AUTH=true` | generated (stored `0600`) |

Field contract: [`specs/006-pi-appliance-deploy/contracts/boot-config.md`](../../specs/006-pi-appliance-deploy/contracts/boot-config.md).
This file lives only on your flashed media — never commit it; the repo ships only the `.example`.

## 4. Enable NVMe boot on the Pi 5 (one-time firmware — human step)

The Pi-5 bootloader **EEPROM** `BOOT_ORDER` is separate firmware and **cannot** be baked into the image.
Once per Pi:

```bash
sudo raspi-config       # Advanced Options → Bootloader Version → Latest; then Boot Order → NVMe/USB
# or directly:
sudo rpi-eeprom-config --edit      # set BOOT_ORDER=0xf416  (try NVMe, then SD)
```

This is reversible (`rpi-eeprom-config`) and low-risk — not a firmware-repack. It is **Class C** (you do
it on the hardware); it is tracked in the verification checklist.

## 5. Boot & verify

Move the NVMe into the Pi (remove any SD), power on, wait a few minutes, then:

```bash
ssh vandaemon@<HOSTNAME>.local
docker ps                                  # vandaemon-api, -web, -mqtt all Up (healthy)
curl -f http://localhost:5000/health       # {"status":"healthy",...}
curl -fsI http://localhost:8080/ | head -1 # 200 — web UI served
# from another host on the LAN, confirm the broker is reachable:
mosquitto_sub -h <HOSTNAME>.local -p 1883 -t '$SYS/#' -C 1
```

Full pass/fail sign-off: [`pi-appliance-verification-checklist.md`](pi-appliance-verification-checklist.md).

## 6. Update procedure

- **Fast (app images only)** — on the appliance:
  ```bash
  cd /opt/vandaemon && docker compose pull && docker compose up -d
  ```
- **Full (OS / base / stack layout changes)** — rebuild the image (`deploy/pi/build.sh`) and re-flash.

## 7. Back up the data

The persistent state lives in Docker volumes on the NVMe:

```bash
# config + retained MQTT messages (the things worth keeping)
docker run --rm -v vandaemon_api-data:/d -v "$PWD":/b alpine tar czf /b/api-data.tgz -C /d .
docker run --rm -v vandaemon_mqtt-data:/d -v "$PWD":/b alpine tar czf /b/mqtt-data.tgz -C /d .
```

Restore by reversing (`tar xzf` into the same volumes). `api-logs` / `mqtt-logs` are optional.

## Fallback: SD-installer

No bench machine / USB-NVMe adapter? Use the **secondary**
[`deploy/pi/sd-installer/`](../../deploy/pi/sd-installer/README.md) path: boot a microSD that images the
rootfs onto the NVMe, then reboot to NVMe and remove the SD. The bench-flash path above is preferred.

## Local development (unchanged)

The same consolidated `docker-compose.yml` runs the full stack on a dev box:

```bash
docker compose up --build      # api + web + mqtt locally
# or bare-metal (still uses appsettings.json localhost broker):
cd src/Backend/VanDaemon.Api && dotnet run
```
