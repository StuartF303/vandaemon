# Phase 1 Data Model: Headless Pi-5 Appliance Deployment

This feature has no application database entities. The "data model" is the set of **configuration and
artifact entities** the build and first-boot consume/produce.

## Entity: Boot-partition config file (`vandaemon-config.txt`)

The single operator-editable input, on the FAT boot partition. Repo ships only the `.example`.

| Field | Type | Required | Default (if absent) | Notes |
|-------|------|----------|---------------------|-------|
| `HOSTNAME` | string | no | `vandaemon` | DNS-safe; sets system hostname + mDNS. |
| `WIFI_SSID` | string | no | (none) | If set, configures WiFi; if absent, Ethernet/DHCP only. |
| `WIFI_PASSWORD` | secret | no | (none) | Written to NetworkManager with `0600`; never echoed/committed. |
| `WIFI_COUNTRY` | string | conditional | `GB` | Required by regulatory db when WiFi used. |
| `SSH_PUBKEY` | string | recommended | (none) | Installed to `authorized_keys`. If absent, SSH password auth stays disabled (key-only, locked-down). |
| `MQTT_AUTH` | bool | no | `false` | `true` flips broker to password+ACL using the example files. |
| `MQTT_USERNAME` | string | conditional | (none) | Used only when `MQTT_AUTH=true`. |
| `MQTT_PASSWORD` | secret | conditional | generated | When `MQTT_AUTH=true` and blank, a password is generated, stored `0600`, and surfaced in first-boot log on console/SSH (documented), never committed. |
| `GHCR_TOKEN` | secret | no | (none) | Only if GHCR images are private and a first-boot pull fallback is needed. Pre-loaded tarballs make this normally unnecessary. |

**Validation rules**: unknown keys ignored with a warning; secret fields never written world-readable;
malformed file → fall back to defaults and log loudly (never block boot).

## Entity: Appliance image artifact (`*.img`)

Produced by `deploy/pi/build.sh`. Contains: arm64 Pi OS Lite rootfs, Docker Engine + Compose plugin,
`/opt/vandaemon/` (compose + mosquitto config + image tarballs), systemd units, `config.txt` NVMe
params, and `vandaemon-config.txt.example` on the boot partition. **No secrets baked in.**

## Entity: GHCR image references

| Image | Repository | Tags | Platforms |
|-------|------------|------|-----------|
| API | `ghcr.io/<owner>/vandaemon-api` | `latest`, `<git-sha>`, `<semver>` | `linux/amd64`, `linux/arm64` |
| Web | `ghcr.io/<owner>/vandaemon-web` | `latest`, `<git-sha>`, `<semver>` | `linux/amd64`, `linux/arm64` |
| Broker | `docker.io/library/eclipse-mosquitto` | `2.0` | upstream multi-arch |

`<owner>` is parameterised (default the repo owner). The compose file references these by an env-overridable
tag so dev can use `latest` and the appliance can pin a sha.

## Entity: Persistent volumes (on NVMe)

| Volume | Mount | Survives reboot | Backup target |
|--------|-------|-----------------|---------------|
| `api-data` | `/app/data` (api) | yes | yes (config JSON) |
| `api-logs` | `/app/logs` (api) | yes | optional |
| `mqtt-data` | `/mosquitto/data` (mqtt) | yes | yes (retained msgs/sessions) |
| `mqtt-logs` | `/mosquitto/log` (mqtt) | yes | optional |

## Entity: systemd units

| Unit | Type | Trigger | Purpose |
|------|------|---------|---------|
| `vandaemon-firstboot.service` | oneshot | first boot only (self-disables) | apply boot-partition config, then start stack |
| `vandaemon.service` | oneshot, `RemainAfterExit=yes` | every boot, `After=docker.service` | `docker compose up -d` (idempotent) |

(Docker's own service is enabled at image-build time; `restart: unless-stopped` in compose handles
per-container resilience between boots.)
