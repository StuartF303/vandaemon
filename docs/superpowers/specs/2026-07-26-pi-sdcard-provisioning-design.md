# Pi SD Card Provisioning — Design

**Date:** 2026-07-26
**Status:** Approved (design); implementation pending
**Author:** Stuart Fraser (with Claude)

## Problem

VanDaemon's architecture (decided 2026-07-18) is "everyone points at the Pi": the
Pi is the controller running the backend + web UI, and the tablet launcher
(`dev.vandaemon.ui`) loads that UI over LAN, defaulting to `vandaemon.local:8080`.
The Pi itself has never been provisioned. We need a repeatable, low-friction way to
build an SD card that boots into the containerised VanDaemon stack.

The existing `docker/docker-compose.yml` uses `build:` (compiles the API and Blazor
WASM from source on whoever runs it). Building on a Raspberry Pi is slow and can OOM
a 2GB Pi. So images must be built elsewhere and pulled.

## Decisions (locked)

| Decision | Choice |
|----------|--------|
| Registry | Docker Hub, namespace `stuartf303` |
| Image build | GitHub Actions (`buildx`, multi-arch, **linux/arm64** required; amd64 optional) |
| Pi runtime | Pulls prebuilt images (no source build on the Pi) |
| Update / OTA trigger | **Manual** — `update-vandaemon.sh` run over SSH when the user chooses |
| Tag strategy | `:dev` / `:latest` during development; add pinned `:vX.Y.Z` on git tags later |
| OS | Raspberry Pi OS **Lite 64-bit** |
| Provisioning | Raspberry Pi Imager custom settings + a single `provision-pi.sh` run over SSH |
| Autostart | systemd `vandaemon.service` |
| Hostname | Must be `vandaemon` (so `vandaemon.local` resolves for the tablet) |

## Architecture

```
  dev push (main)                Docker Hub                    Pi (in van)
 ┌──────────────┐   buildx     ┌──────────────┐   manual     ┌─────────────────┐
 │ GitHub CI    │─ arm64 ─────▶│ vandaemon-api│◀── pull ─────│ compose.pi.yml  │
 │              │   push       │ vandaemon-web│  (you decide)│ + update script │
 └──────────────┘              │  :dev/:latest│              │ + systemd start │
                                └──────────────┘              └────────┬────────┘
                                                                       │ :8080
                                                              tablet (vandaemon.local)
```

The SD card stays deliberately dumb — OS + Docker + a compose file + two scripts + a
systemd unit + hostname `vandaemon`. The application lives entirely in Docker Hub, so
the card rarely needs re-flashing. This keeps open a later move to a "dedicated baked
image" without changing any of the below.

## Components

### 1. CI publish workflow — `.github/workflows/publish-images.yml`
- Trigger: push to `main` (and `workflow_dispatch`). Later: git tags `v*`.
- Uses `docker/setup-qemu-action` + `docker/setup-buildx-action`.
- Builds **linux/arm64** (amd64 optional for dev-on-laptop) for two images:
  - `stuartf303/vandaemon-api` from `docker/Dockerfile.api`
  - `stuartf303/vandaemon-web` from `docker/Dockerfile.web`
- Tags: `:dev` and `:latest` now; `:vX.Y.Z` added when a `v*` tag is pushed.
- Auth via repo secrets `DOCKERHUB_USERNAME` (`stuartf303`) and `DOCKERHUB_TOKEN`
  (a Docker Hub access token, not the account password).
- Context is repo root; Dockerfiles are unchanged from today.

### 2. Pi compose file — `docker/compose.pi.yml`
- Same three services as `docker-compose.yml` but `image:` refs instead of `build:`:
  - `api`  → `stuartf303/vandaemon-api:${VANDAEMON_TAG:-dev}`
  - `web`  → `stuartf303/vandaemon-web:${VANDAEMON_TAG:-dev}`
  - `mqtt` → `eclipse-mosquitto:2.0` (already an image; unchanged)
- Preserves current ports, volumes (`api-data`, `api-logs`, `mqtt-*`), restart
  policies (`unless-stopped`), and the `vandaemon` bridge network.
- Tag selectable via a `VANDAEMON_TAG` env var (default `dev`) so pinning later is a
  one-line change, not a file rewrite.
- `mosquitto` config: the mosquitto config dir must be present on the Pi. Provision
  script copies `docker/mosquitto/` alongside the compose file (verify the relative
  volume path resolves under `/opt/vandaemon`).

### 3. Provision script — `scripts/provision-pi.sh`
Run once over SSH after first boot. Steps:
1. **Preflight** — confirm not-root-but-has-sudo, confirm arch is arm64, confirm
   internet reachability to Docker Hub.
2. **Hostname check** — read current hostname. If not `vandaemon`, warn that the
   tablet expects `vandaemon.local` and prompt `[y/N]` to fix. On yes:
   `hostnamectl set-hostname vandaemon` **and** rewrite the `127.0.1.1` line in
   `/etc/hosts`. Flag that `vandaemon.local` will not resolve until reboot. On no:
   continue with a loud reminder.
3. **Docker** — install Docker + compose plugin via `get.docker.com` if absent
   (skip if present); add the invoking user to the `docker` group.
4. **Files** — copy `compose.pi.yml`, `update-vandaemon.sh`, and `mosquitto/` into
   `/opt/vandaemon`.
5. **Autostart** — install and enable `vandaemon.service`.
6. **Finish** — offer to reboot (needed for hostname + docker group to take effect).

Idempotent: re-running skips Docker install, overwrites `/opt/vandaemon`, re-enables
the service without error.

### 4. Update script — `scripts/update-vandaemon.sh`
The manual OTA button:
1. Print currently-running image digests.
2. `docker compose -f /opt/vandaemon/compose.pi.yml pull`.
3. On pull success: `docker compose ... up -d`, then `docker image prune -f`.
4. On pull failure (e.g. no internet in the van): leave running containers untouched,
   print a clear error, exit non-zero.
5. Print new image digests so the change is visible. Rollback = set `VANDAEMON_TAG`
   to a previous tag and re-run.

### 5. Autostart unit — `vandaemon.service`
systemd oneshot (`RemainAfterExit=yes`) that runs
`docker compose -f /opt/vandaemon/compose.pi.yml up -d` on boot and `down` on stop,
after `docker.service` and `network-online.target`. Modelled on the unit already
documented in `docs/deployment/raspberry-pi-setup.md`.

## Data flow

1. Developer merges to `main` → CI builds arm64 images → pushes to Docker Hub `:dev`.
2. In the van, when the user chooses: SSH to the Pi → `update-vandaemon.sh` → pulls
   new images → restarts the stack.
3. On boot/power-cycle, `vandaemon.service` brings the stack up automatically.
4. The tablet launcher reaches the UI at `vandaemon.local:8080`.

## Error handling & edge cases

- **Wrong hostname** — detected and prompted in provision (see component 3); the
  single most common cause of "tablet can't find the Pi".
- **Reboot dependency** — hostname change and docker-group membership both need a
  reboot / re-login; the script says so explicitly and offers to reboot.
- **No internet during update** — update script fails safe, leaving the running stack
  in place.
- **Re-provisioning** — idempotent; safe to re-run.
- **Not root / no sudo / wrong arch** — preflight fails fast with a clear message.

## Testing

- **Scripts** — `bash -n` + `shellcheck` in CI. Hostname-check logic factored into a
  function so it is unit-testable against a faked `hostname`.
- **CI workflow** — proven by one real run: arm64 images appear at
  `stuartf303/vandaemon-api:dev` and `-web:dev`.
- **End-to-end (human-gated, on real hardware)** — explicit manual checklist, NOT
  claimed as done until Stuart confirms (same discipline as head-unit SC-006/007):
  1. Flash SD (Imager: OS Lite 64-bit, hostname `vandaemon`, SSH key, WiFi).
  2. Boot, SSH in, run `provision-pi.sh`, accept hostname + reboot.
  3. After reboot, `vandaemon.local:8080` serves the UI.
  4. Tablet launcher connects to `vandaemon.local` and loads the live UI.
  5. `update-vandaemon.sh` pulls a newer `:dev` and restarts cleanly.

## Out of scope (YAGNI, for now)

- Auto-update (Watchtower) and scheduled polling — explicitly deferred; manual only.
- Pinned `:vX.Y.Z` release tags and rollback tooling — added when the van goes into
  service, not during development.
- A dedicated baked/golden SD image — possible later; this design keeps it open but
  does not build it.
- WiFi access-point mode, static IP, HTTPS — covered by the existing Pi setup guide
  and not required for first light.

## Open items

- `DOCKERHUB_TOKEN` must be created on Docker Hub and added as a GitHub repo secret
  (human step; the token value never enters the repo).
