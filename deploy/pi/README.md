# VanDaemon Pi-5 Appliance Image Builder

Builds a **flashable arm64 Raspberry Pi OS Lite image** that boots a Raspberry Pi 5 (from NVMe) straight
into a running VanDaemon stack (API + web + local Mosquitto broker) on the van LAN — no keyboard,
monitor, or interactive setup. This is the implementation of **ADR-001** action items 1–3.

> **Operator guide** (flash / verify / update / back up): see
> [`docs/deployment/pi-appliance-setup.md`](../../docs/deployment/pi-appliance-setup.md).
> **On-hardware verification** (Class C): see
> [`docs/deployment/pi-appliance-verification-checklist.md`](../../docs/deployment/pi-appliance-verification-checklist.md).

## Important constraints

- **Build host must be Linux / WSL2 / Docker.** pi-gen does **not** run natively on Windows. On the
  Windows dev box, run this under WSL2 (with Docker) or any Linux host with Docker.
- **Target is arm64.** The builder may be x86; `build.sh` pulls the `linux/arm64` images and bakes them
  in. No x86 assumption reaches the appliance.
- **GHCR is a build-time-only dependency.** The api/web images are pulled from GHCR **on the builder**
  and baked into the image as `docker save` tarballs. The **appliance itself runs fully offline** — no
  internet is needed at boot (Constitution III). An online `docker compose pull` is only a fallback.
- **No secrets are baked in.** First-boot configuration (hostname, WiFi, SSH key, broker creds) is read
  from a single file on the boot partition (`vandaemon-config.txt`). The repo ships only
  `boot/vandaemon-config.txt.example`.
- **Reproducible & reversible.** The build runs in pi-gen's Docker container; it performs nothing
  irreversible on the build host and can be re-run.

## Build

```bash
cd deploy/pi
cp config/pi-gen-config.example config/pi-gen-config   # edit IMG_NAME / locale (NO secrets)
./build.sh                                              # clones pi-gen, bakes the stack, builds the .img
# → deploy/pi/pi-gen/deploy/<IMG_NAME>.img
```

`build.sh` will:

1. Clone/refresh pi-gen (arm64) into `deploy/pi/pi-gen/`.
2. Stage the consolidated `docker-compose.yml` + `docker/mosquitto/config` into the custom stage.
3. Pull the `linux/arm64` `vandaemon-api` / `vandaemon-web` (GHCR) + `eclipse-mosquitto:2.0` images and
   `docker save` them into the stage so first boot is offline-capable.
4. Run pi-gen with `STAGE_LIST="stage0 stage1 stage2 stage-vandaemon"` to produce the `.img`.

## What gets baked into the image (`stage-vandaemon`)

| Step | Effect |
|------|--------|
| `00-install-docker` | Installs Docker Engine + compose plugin; enables `docker` on boot. |
| `01-vandaemon-stack` | Copies the stack to `/opt/vandaemon`; `docker load`s the saved image tarballs. |
| `02-firstboot` | Installs `vandaemon.service` (compose up on every boot) + `vandaemon-firstboot.service` (one-shot config applier); writes NVMe `config.txt` params; stages `vandaemon-config.txt.example` on the boot partition. |

## NVMe boot (one-time firmware step — NOT baked in)

The rootfs-side NVMe enablement is baked into `config.txt`. The **Pi 5 bootloader EEPROM** `BOOT_ORDER`
is separate firmware and **cannot** live in the flashed image — it is a one-time human step documented
in the operator guide and the verification checklist.
