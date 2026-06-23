# Contract: Mosquitto Broker Configuration

File: `docker/mosquitto/config/mosquitto.conf` (edited in place). Existing `passwd.example` and
`acl.conf.example` are retained and referenced by the auth toggle.

## Default posture (trusted van LAN)

- `persistence true`, `persistence_location /mosquitto/data/` (retained msgs + sessions survive restart).
- `autosave_interval` set (periodic persistence flush).
- `persistent_client_expiration` set (bounded session retention).
- Listener `1883` (mqtt) + `9001` (websockets), as today.
- `allow_anonymous true` — explicitly documented as acceptable ONLY on a trusted private LAN.
- Last-will: broker-side persistence + retained support is enabled; LWT is set by clients (e.g. LED
  dimmer `status` topic) — the broker config does not block it.

## Off-by-default Cerbo bridge (Victron draft 0003 hook)

A documented, **commented-out** stanza, e.g.:

```conf
# --- Bridge from Victron Cerbo broker (DISABLED BY DEFAULT) ---
# connection cerbo-bridge
# address <CERBO_IP>:1883
# topic N/# in 0 cerbo/
# remote_username <user>
# remote_password <pass>
# bridge_protocol_version mqttv311
```

Contract: present, inert (commented), with placeholder values — must not attempt a connection by default.

## Single-flag auth path

Documented switch to enable auth using the existing example files:

```conf
# To enable auth on an untrusted network:
#   1) set: allow_anonymous false
#   2) uncomment: password_file /mosquitto/config/passwd
#   3) (optional) uncomment: acl_file /mosquitto/config/acl.conf
#   4) create passwd from passwd.example (mosquitto_passwd), copy acl.conf from acl.conf.example
#   5) restart the mqtt service
```

Contract: no real `passwd`/`acl.conf` committed — only `.example`. The conf documents the exact steps.

## Static acceptance (Class B)

- `mosquitto -c mosquitto.conf` config-parse passes (run in an `eclipse-mosquitto:2.0` container).
- `persistence true` present; bridge stanza present and commented; auth directives present and commented.
- No committed `passwd` or `acl.conf` (only `*.example`).
