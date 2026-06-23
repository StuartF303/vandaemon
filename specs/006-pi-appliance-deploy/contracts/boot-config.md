# Contract: Boot-partition Config File

File: `vandaemon-config.txt` on the FAT boot partition. Repo ships `vandaemon-config.txt.example` only.
Format: simple `KEY=VALUE` lines (shell-sourceable), `#` comments. Consumed by `firstboot.sh`.

## Fields

See `../data-model.md` "Boot-partition config file" for the authoritative field table. Summary:

```sh
# --- Identity ---
HOSTNAME=vandaemon

# --- Network (optional; omit for Ethernet/DHCP) ---
WIFI_SSID=
WIFI_PASSWORD=
WIFI_COUNTRY=GB

# --- Access ---
SSH_PUBKEY="ssh-ed25519 AAAA... user@host"

# --- MQTT broker auth (default: open on trusted van LAN) ---
MQTT_AUTH=false
MQTT_USERNAME=
MQTT_PASSWORD=

# --- Registry (only if GHCR images are private AND relying on first-boot pull) ---
GHCR_TOKEN=
```

## Behavioural contract

1. File present + well-formed → apply each field; secrets written `0600`; SSH key installed.
2. Field absent → documented default (see data-model); never prompt, never hang.
3. File absent entirely → all defaults; appliance still boots and serves locally.
4. `MQTT_AUTH=true` with blank `MQTT_PASSWORD` → generate, store `0600`, log location once; never commit.
5. First boot succeeds → `vandaemon-firstboot.service` disables itself; subsequent boots use
   `vandaemon.service` only.

## Static acceptance (Class B)

- `shellcheck` clean on `firstboot.sh`.
- The `.example` contains every documented key and **no real secret values**.
- Parser ignores unknown keys with a warning (covered by script review).
