# VanDaemon Pi-5 Appliance — On-Hardware Verification Checklist (Class C)

**Status: NOT VERIFIED.** Every item below is **human-verified on the physical Raspberry Pi 5** and is
**unchecked** until someone records an observation. The build loop CANNOT complete these — they require
the hardware. Do **not** mark the feature "done on device" until this checklist is filled in.

- **Feature**: `006-pi-appliance-deploy`
- **Image built from**: commit `__________`  ·  builder host: `__________`
- **Pi 5 board / EEPROM date**: `__________`  ·  NVMe HAT + drive: `__________`
- **Verified by**: `__________`  ·  **Date**: `__________`

> Class reference: loop-playbook §4 (Class C — Human-Verified) and Constitution §XIII.5 (no green-washing).
> The Class-B static checks (compose/config/lint/buildx) are done in the repo; THIS file is the part that
> only the hardware can confirm.

## A. One-time firmware (cannot be baked into the image)

- [ ] **EEPROM updated & `BOOT_ORDER` set** to try NVMe (e.g. `0xf416`) via `raspi-config` /
      `rpi-eeprom-config`. Observed value: `__________`  ·  verified by `____` on `____`
- [ ] Pi powers on from the NVMe with **no SD/USB attached**. verified by `____` on `____`

## B. First boot (headless, no keyboard/monitor)

- [ ] Pi appears on the LAN at the configured **hostname** (`<HOSTNAME>.local`). verified by `____` on `____`
- [ ] **SSH** works with the key from `vandaemon-config.txt` (no password prompt). verified by `____` on `____`
- [ ] WiFi (if configured) connected; or Ethernet/DHCP if no WiFi set. verified by `____` on `____`
- [ ] No interactive prompt ever appeared (truly zero-touch). verified by `____` on `____`

## C. Services up & healthy

- [ ] `docker ps` shows `vandaemon-api`, `vandaemon-web`, `vandaemon-mqtt` all **Up (healthy)**.
      verified by `____` on `____`
- [ ] `curl -f http://localhost:5000/health` returns healthy. verified by `____` on `____`
- [ ] Web UI served: `curl -fsI http://localhost:8080/` → `200`. verified by `____` on `____`
- [ ] Broker reachable **from another LAN host**: `mosquitto_sub -h <HOSTNAME>.local -p 1883 -t '$SYS/#' -C 1`.
      verified by `____` on `____`
- [ ] Images came from the **baked tarballs** (first boot worked with no internet). verified by `____` on `____`

## D. Resilience (ACC-style power cycle)

- [ ] After an abrupt power cycle, all three services **return automatically**. verified by `____` on `____`
- [ ] Persisted config (`api-data`) survives the cycle. verified by `____` on `____`
- [ ] Retained MQTT messages / sessions (`mqtt-data`) survive the cycle. verified by `____` on `____`

## E. Optional auth (only if `MQTT_AUTH=true` was set)

- [ ] Anonymous connect is **refused**; the configured user connects. verified by `____` on `____`
- [ ] Generated password (if blank in config) was stored `0600` at
      `/var/lib/vandaemon/mqtt-credentials` and is NOT in any repo/commit. verified by `____` on `____`

## F. Fallback path (only if used)

- [ ] SD-installer wrote the rootfs to NVMe and the Pi then booted from NVMe. verified by `____` on `____`

## Sign-off

- [ ] All applicable items above are checked **with observations recorded**. Until then, the on-device
      outcome is **unproven** and must not be claimed as passing.
