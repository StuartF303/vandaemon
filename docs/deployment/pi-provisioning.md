# VanDaemon Pi Provisioning (Containerised Pull)

This is the recommended way to build a VanDaemon SD card. The Pi pulls prebuilt
images from Docker Hub — it never compiles from source. For the fuller manual
reference (I2C, Modbus, static IP, AP mode) see `raspberry-pi-setup.md`.

## 1. Flash the card (Raspberry Pi Imager)

- OS: **Raspberry Pi OS Lite (64-bit)**.
- In Imager's advanced settings (gear / Ctrl-Shift-X):
  - **Hostname:** `vandaemon`
  - **Enable SSH:** use your public key.
  - **WiFi + locale:** as needed.

Setting the hostname here means `vandaemon.local` works from first boot and the
provision script has nothing to fix.

## 2. Provision (once, over SSH)

```bash
ssh <user>@vandaemon.local        # or the Pi's IP on first boot
git clone https://github.com/StuartF303/vandaemon.git
cd vandaemon
./scripts/provision-pi.sh
```

The script installs Docker, deploys the runtime files to `/opt/vandaemon`,
enables the `vandaemon` systemd service, and checks the hostname (prompting to
fix it if it isn't `vandaemon` — that needs a reboot). Accept the reboot if asked.

## 3. Start / verify

```bash
sudo systemctl start vandaemon      # or just reboot
```

Open `http://vandaemon.local:8080` — the VanDaemon UI should load. The tablet
launcher (default address `vandaemon.local:8080`) will now find the controller.

## 4. Updating the van (manual OTA)

When you want the latest build:

```bash
ssh <user>@vandaemon.local
/opt/vandaemon/update-vandaemon.sh
```

It pulls the newest `:dev` images and restarts. If there is no internet, it
fails safe and leaves the running stack alone. To roll back or pin a version,
run with `VANDAEMON_TAG=v1.2.3 /opt/vandaemon/update-vandaemon.sh`.

## Manual end-to-end verification checklist

Run on real hardware; do not consider provisioning "done" until all pass:

- [ ] SD flashed with OS Lite 64-bit, hostname `vandaemon`, SSH key, WiFi.
- [ ] `provision-pi.sh` completes without error; reboot accepted if prompted.
- [ ] After reboot, `vandaemon.local` resolves (`ping vandaemon.local`).
- [ ] `http://vandaemon.local:8080` serves the VanDaemon UI.
- [ ] The tablet launcher connects to `vandaemon.local` and loads the live UI.
- [ ] `update-vandaemon.sh` pulls a newer `:dev` and restarts cleanly.
- [ ] Power-cycle: the stack comes back up automatically (systemd autostart).
