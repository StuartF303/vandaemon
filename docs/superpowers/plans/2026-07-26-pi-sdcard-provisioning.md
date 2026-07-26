# Pi SD Card Provisioning Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make an SD card boot a Raspberry Pi into the containerised VanDaemon stack by pulling prebuilt arm64 images from Docker Hub, with a one-shot provisioner and a manual update path.

**Architecture:** GitHub Actions builds `linux/arm64` images and pushes them to Docker Hub under `stuartf303/vandaemon-{api,web}`. The Pi runs a pull-only compose file plus two shell scripts (provision once, update on demand) and a systemd unit for autostart. The SD card holds only OS + Docker + these files; the app lives in the registry.

**Tech Stack:** Docker + Docker Compose v2, Bash, systemd, GitHub Actions (`docker/build-push-action`, `buildx`, QEMU), Raspberry Pi OS Lite 64-bit.

## Global Constraints

- Docker Hub namespace is `stuartf303`; images are `stuartf303/vandaemon-api` and `stuartf303/vandaemon-web`.
- Pi images MUST be built for `linux/arm64`. Amd64 is optional and out of scope now.
- The Pi NEVER builds from source — compose uses `image:`, never `build:`.
- Development tags are `:dev` and `:latest`; pinned `:vX.Y.Z` only on git `v*` tags.
- Runtime install directory on the Pi is `/opt/vandaemon`.
- Target hostname is `vandaemon` (so `vandaemon.local` resolves for the tablet launcher).
- Shell scripts start with `#!/usr/bin/env bash` and `set -euo pipefail` (except the test harness, which uses `set -uo pipefail` so assertions can continue after a failure).
- No secret values in the repo — `DOCKERHUB_TOKEN` is a GitHub repo secret only.

---

### Task 1: Pi runtime compose file

**Files:**
- Create: `docker/compose.pi.yml`

**Interfaces:**
- Consumes: nothing.
- Produces: a compose file at `docker/compose.pi.yml` referencing `stuartf303/vandaemon-api:${VANDAEMON_TAG:-dev}` and `stuartf303/vandaemon-web:${VANDAEMON_TAG:-dev}`; consumed by `provision-pi.sh` (Task 4), `update-vandaemon.sh` (Task 3), and `vandaemon.service` (Task 4). Mosquitto reads config from `./mosquitto/config` relative to the file (i.e. `/opt/vandaemon/mosquitto/config` on the Pi).

- [ ] **Step 1: Create the compose file**

```yaml
# docker/compose.pi.yml
# Pi runtime compose — PULLS prebuilt images from Docker Hub (never builds).
# Select the tag with VANDAEMON_TAG (default: dev). Pin to :vX.Y.Z in production.
# Deployed to /opt/vandaemon/compose.pi.yml by scripts/provision-pi.sh.
services:
  api:
    image: stuartf303/vandaemon-api:${VANDAEMON_TAG:-dev}
    container_name: vandaemon-api
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:80
    ports:
      - "5000:80"
    volumes:
      - api-data:/app/data
      - api-logs:/app/logs
    restart: unless-stopped
    networks:
      - vandaemon

  web:
    image: stuartf303/vandaemon-web:${VANDAEMON_TAG:-dev}
    container_name: vandaemon-web
    ports:
      - "8080:80"
    depends_on:
      - api
    restart: unless-stopped
    networks:
      - vandaemon

  mqtt:
    image: eclipse-mosquitto:2.0
    container_name: vandaemon-mqtt
    ports:
      - "1883:1883"
      - "9001:9001"
    volumes:
      - ./mosquitto/config:/mosquitto/config
      - mqtt-data:/mosquitto/data
      - mqtt-logs:/mosquitto/log
    restart: unless-stopped
    networks:
      - vandaemon
    command: mosquitto -c /mosquitto/config/mosquitto.conf

volumes:
  api-data:
  api-logs:
  mqtt-data:
  mqtt-logs:

networks:
  vandaemon:
    driver: bridge
```

Note: the obsolete `version:` key from `docker-compose.yml` is intentionally omitted (Compose v2 ignores it and warns).

- [ ] **Step 2: Validate the compose file**

Run (requires Docker; run on any machine with Docker, e.g. `tiny` or the Pi):
```bash
VANDAEMON_TAG=dev docker compose -f docker/compose.pi.yml config -q && echo "COMPOSE OK"
```
Expected: prints `COMPOSE OK` with no errors. (If Docker is unavailable locally, validate YAML instead: `python -c "import yaml; yaml.safe_load(open('docker/compose.pi.yml'))" && echo YAML_OK`.)

- [ ] **Step 3: Commit**

```bash
git add docker/compose.pi.yml
git commit -m "feat(010): Pi runtime compose that pulls prebuilt images"
```

---

### Task 2: Hostname-check library + unit test

**Files:**
- Create: `scripts/lib/hostname-check.sh`
- Test: `tests/scripts/test-hostname-check.sh`

**Interfaces:**
- Consumes: nothing.
- Produces: function `check_and_fix_hostname()` that prints exactly one of `OK`, `FIXED`, `DECLINED` to stdout (warnings to stderr) and always returns 0. Reads `DESIRED_HOSTNAME` (default `vandaemon`). Uses overridable indirection functions `_hc_get_hostname`, `_hc_set_hostname`, `_hc_prompt`. Sourced by `provision-pi.sh` (Task 4), which also calls `_hc_prompt` for its reboot question.

- [ ] **Step 1: Write the failing test**

```bash
# tests/scripts/test-hostname-check.sh
#!/usr/bin/env bash
set -uo pipefail
cd "$(dirname "$0")/../.."
source scripts/lib/hostname-check.sh

fails=0
assert_eq() { # $1 actual  $2 expected  $3 name
  if [[ "$1" == "$2" ]]; then
    echo "PASS: $3"
  else
    echo "FAIL: $3 (got '$1' want '$2')"
    fails=$((fails + 1))
  fi
}

marker="$(mktemp)"

# Case 1: hostname already correct -> OK, no set call
DESIRED_HOSTNAME=vandaemon
_hc_get_hostname() { echo vandaemon; }
_hc_prompt() { return 0; }
: > "$marker"
_hc_set_hostname() { echo called >> "$marker"; }
out="$(check_and_fix_hostname 2>/dev/null)"
assert_eq "$out" "OK" "already-correct returns OK"
assert_eq "$(cat "$marker")" "" "set NOT called when correct"

# Case 2: wrong hostname, user says yes -> FIXED, set called
_hc_get_hostname() { echo raspberrypi; }
_hc_prompt() { return 0; }
: > "$marker"
out="$(check_and_fix_hostname 2>/dev/null)"
assert_eq "$out" "FIXED" "wrong+yes returns FIXED"
assert_eq "$(cat "$marker")" "called" "set called on yes"

# Case 3: wrong hostname, user says no -> DECLINED, set NOT called
_hc_prompt() { return 1; }
: > "$marker"
out="$(check_and_fix_hostname 2>/dev/null)"
assert_eq "$out" "DECLINED" "wrong+no returns DECLINED"
assert_eq "$(cat "$marker")" "" "set NOT called on no"

rm -f "$marker"
echo "---"
[[ "$fails" -eq 0 ]] && echo "ALL PASS" || echo "$fails FAILURES"
exit "$fails"
```

- [ ] **Step 2: Run the test to verify it fails**

Run:
```bash
bash tests/scripts/test-hostname-check.sh
```
Expected: FAIL — `source: scripts/lib/hostname-check.sh: No such file or directory` (the library does not exist yet).

- [ ] **Step 3: Write the minimal library**

```bash
# scripts/lib/hostname-check.sh
# Hostname check/repair for VanDaemon Pi provisioning.
# Pure-ish functions with overridable indirection points for testing.
# Sourced by scripts/provision-pi.sh. Do NOT set -e here (it is sourced).

DESIRED_HOSTNAME="${DESIRED_HOSTNAME:-vandaemon}"

# --- Indirection points (overridden in tests) ---
_hc_get_hostname() { hostname; }

_hc_set_hostname() {
  local new="$1"
  sudo hostnamectl set-hostname "$new"
  # Keep /etc/hosts in sync or sudo complains about unresolved host.
  sudo sed -i "s/^127\.0\.1\.1.*/127.0.1.1\t$new/" /etc/hosts
}

_hc_prompt() { # $1 = message; return 0 for yes
  local reply
  read -r -p "$1 [y/N] " reply
  [[ "$reply" =~ ^[Yy]$ ]]
}

# Prints OK | FIXED | DECLINED to stdout; warnings to stderr; always returns 0.
check_and_fix_hostname() {
  local current
  current="$(_hc_get_hostname)"
  if [[ "$current" == "$DESIRED_HOSTNAME" ]]; then
    echo "OK"
    return 0
  fi
  echo "WARN: hostname is '$current'; the tablet expects '${DESIRED_HOSTNAME}.local'." >&2
  if _hc_prompt "Set hostname to '${DESIRED_HOSTNAME}' (needs reboot to take effect)?"; then
    _hc_set_hostname "$DESIRED_HOSTNAME"
    echo "FIXED"
    return 0
  fi
  echo "DECLINED"
  return 0
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run:
```bash
bash tests/scripts/test-hostname-check.sh
```
Expected: all `PASS` lines then `ALL PASS`, exit 0.

- [ ] **Step 5: Shellcheck the library (if available)**

Run:
```bash
command -v shellcheck >/dev/null && shellcheck scripts/lib/hostname-check.sh && echo LINT_OK || echo "shellcheck not installed — skipping"
```
Expected: `LINT_OK`, or the skip message. (If shellcheck flags SC2317/unreachable on the overridable functions, that is acceptable — they are called via indirection.)

- [ ] **Step 6: Commit**

```bash
git add scripts/lib/hostname-check.sh tests/scripts/test-hostname-check.sh
git commit -m "feat(010): testable hostname check/repair library"
```

---

### Task 3: Manual update (OTA) script

**Files:**
- Create: `scripts/update-vandaemon.sh`

**Interfaces:**
- Consumes: `/opt/vandaemon/compose.pi.yml` (Task 1 file, deployed by Task 4).
- Produces: an executable `update-vandaemon.sh` that pulls and restarts, failing safe on pull error. Deployed to `/opt/vandaemon/` by `provision-pi.sh` (Task 4).

- [ ] **Step 1: Create the update script**

```bash
# scripts/update-vandaemon.sh
#!/usr/bin/env bash
# VanDaemon manual OTA — pull the latest images and restart.
# Fails safe: if the pull fails (e.g. no internet in the van), the running
# stack is left untouched. Rollback = set VANDAEMON_TAG to a prior tag + re-run.
set -euo pipefail

INSTALL_DIR="/opt/vandaemon"
COMPOSE="$INSTALL_DIR/compose.pi.yml"
TAG="${VANDAEMON_TAG:-dev}"
cd "$INSTALL_DIR"

echo "== Current images =="
docker compose -f "$COMPOSE" images || true

echo "== Pulling latest (tag: $TAG) =="
if ! VANDAEMON_TAG="$TAG" docker compose -f "$COMPOSE" pull; then
  echo "ERROR: pull failed (no internet?). Running stack left untouched." >&2
  exit 1
fi

echo "== Restarting stack =="
VANDAEMON_TAG="$TAG" docker compose -f "$COMPOSE" up -d

echo "== Pruning dangling images =="
docker image prune -f

echo "== New images =="
docker compose -f "$COMPOSE" images
echo "Update complete."
```

- [ ] **Step 2: Syntax-check and lint the script**

Run:
```bash
bash -n scripts/update-vandaemon.sh && echo "SYNTAX OK"
command -v shellcheck >/dev/null && shellcheck scripts/update-vandaemon.sh && echo LINT_OK || echo "shellcheck skipped"
```
Expected: `SYNTAX OK`, then `LINT_OK` or the skip message.

- [ ] **Step 3: Commit**

```bash
git add scripts/update-vandaemon.sh
git commit -m "feat(010): manual OTA update script (fail-safe pull+restart)"
```

---

### Task 4: Systemd unit + provisioning script

**Files:**
- Create: `scripts/vandaemon.service`
- Create: `scripts/provision-pi.sh`

**Interfaces:**
- Consumes: `check_and_fix_hostname()` and `_hc_prompt()` from `scripts/lib/hostname-check.sh` (Task 2); `docker/compose.pi.yml` (Task 1); `scripts/update-vandaemon.sh` (Task 3); `docker/mosquitto/` (existing directory).
- Produces: a one-shot provisioner and a systemd unit; no downstream consumers.

- [ ] **Step 1: Create the systemd unit**

```ini
# scripts/vandaemon.service — installed to /etc/systemd/system/ by provision-pi.sh
[Unit]
Description=VanDaemon Control System (containerised)
Requires=docker.service
After=docker.service network-online.target
Wants=network-online.target

[Service]
Type=oneshot
RemainAfterExit=yes
WorkingDirectory=/opt/vandaemon
ExecStart=/usr/bin/docker compose -f /opt/vandaemon/compose.pi.yml up -d
ExecStop=/usr/bin/docker compose -f /opt/vandaemon/compose.pi.yml down
TimeoutStartSec=300

[Install]
WantedBy=multi-user.target
```

- [ ] **Step 2: Create the provisioning script**

```bash
# scripts/provision-pi.sh
#!/usr/bin/env bash
# VanDaemon Pi provisioner — run ONCE over SSH after first boot.
# Idempotent: safe to re-run. Installs Docker, deploys runtime files,
# enables autostart, and checks/repairs the hostname to 'vandaemon'.
set -euo pipefail

INSTALL_DIR="/opt/vandaemon"
REPO_DIR="$(cd "$(dirname "$0")/.." && pwd)"
REBOOT_NEEDED=0

# shellcheck source=scripts/lib/hostname-check.sh
source "$REPO_DIR/scripts/lib/hostname-check.sh"

log() { echo "[provision] $*"; }
die() { echo "[provision] ERROR: $*" >&2; exit 1; }

# --- Preflight ---
[[ "$(id -u)" -ne 0 ]] || die "Run as a normal user WITH sudo, not as root."
command -v sudo >/dev/null || die "sudo not found."
command -v curl >/dev/null || die "curl not found (needed to install Docker)."
arch="$(uname -m)"
[[ "$arch" == "aarch64" || "$arch" == "arm64" ]] || \
  log "WARN: arch '$arch' is not arm64 — the Docker Hub images are arm64."

# --- Hostname ---
status="$(check_and_fix_hostname)"
case "$status" in
  FIXED)    REBOOT_NEEDED=1 ;;
  DECLINED) log "WARN: 'vandaemon.local' will not resolve until the hostname is 'vandaemon'." ;;
esac

# --- Docker ---
if ! command -v docker >/dev/null; then
  log "Installing Docker..."
  curl -fsSL https://get.docker.com | sudo sh
  sudo usermod -aG docker "$USER"
  REBOOT_NEEDED=1
else
  log "Docker already installed; skipping."
fi

# --- Runtime files ---
log "Deploying runtime files to $INSTALL_DIR"
sudo mkdir -p "$INSTALL_DIR"
sudo cp "$REPO_DIR/docker/compose.pi.yml"        "$INSTALL_DIR/compose.pi.yml"
sudo cp "$REPO_DIR/scripts/update-vandaemon.sh"  "$INSTALL_DIR/update-vandaemon.sh"
sudo chmod +x "$INSTALL_DIR/update-vandaemon.sh"
sudo rm -rf "$INSTALL_DIR/mosquitto"
sudo cp -r "$REPO_DIR/docker/mosquitto"          "$INSTALL_DIR/mosquitto"

# --- Autostart ---
log "Installing systemd service"
sudo cp "$REPO_DIR/scripts/vandaemon.service" /etc/systemd/system/vandaemon.service
sudo systemctl daemon-reload
sudo systemctl enable vandaemon.service

log "Provisioning complete."
log "Start now with: sudo systemctl start vandaemon   (or reboot)"
if [[ "$REBOOT_NEEDED" -eq 1 ]]; then
  log "A reboot is REQUIRED for the hostname and/or docker group to take effect."
  if _hc_prompt "Reboot now?"; then
    sudo reboot
  fi
fi
```

- [ ] **Step 3: Syntax-check and lint both artifacts**

Run:
```bash
bash -n scripts/provision-pi.sh && echo "SYNTAX OK"
command -v shellcheck >/dev/null && shellcheck -x scripts/provision-pi.sh && echo LINT_OK || echo "shellcheck skipped"
command -v systemd-analyze >/dev/null && systemd-analyze verify scripts/vandaemon.service && echo UNIT_OK || echo "systemd-analyze skipped"
```
Expected: `SYNTAX OK`; then `LINT_OK`/skip; then `UNIT_OK`/skip. (`systemd-analyze` is Linux-only — expect the skip message on Windows/macOS; it runs on `tiny`/Pi.)

- [ ] **Step 4: Re-run the hostname unit test (guard against regressions in the shared lib)**

Run:
```bash
bash tests/scripts/test-hostname-check.sh
```
Expected: `ALL PASS`, exit 0.

- [ ] **Step 5: Commit**

```bash
git add scripts/provision-pi.sh scripts/vandaemon.service
git commit -m "feat(010): Pi provisioner + systemd autostart unit"
```

---

### Task 5: GitHub Actions publish workflow

**Files:**
- Create: `.github/workflows/publish-images.yml`

**Interfaces:**
- Consumes: existing `docker/Dockerfile.api` and `docker/Dockerfile.web`; GitHub repo secrets `DOCKERHUB_USERNAME` and `DOCKERHUB_TOKEN`.
- Produces: `stuartf303/vandaemon-api` and `stuartf303/vandaemon-web` images on Docker Hub, tagged `:dev`+`:latest` on `main`, `:vX.Y.Z` on `v*` tags. Consumed at runtime by `docker/compose.pi.yml` (Task 1).

- [ ] **Step 1: Create the workflow**

```yaml
# .github/workflows/publish-images.yml
name: publish-images

on:
  push:
    branches: [main]
    tags: ['v*']
  workflow_dispatch:

jobs:
  publish:
    runs-on: ubuntu-latest
    strategy:
      matrix:
        include:
          - image: vandaemon-api
            dockerfile: docker/Dockerfile.api
          - image: vandaemon-web
            dockerfile: docker/Dockerfile.web
    steps:
      - uses: actions/checkout@v4

      - name: Set up QEMU (for arm64 emulation)
        uses: docker/setup-qemu-action@v3

      - name: Set up Buildx
        uses: docker/setup-buildx-action@v3

      - name: Log in to Docker Hub
        uses: docker/login-action@v3
        with:
          username: ${{ secrets.DOCKERHUB_USERNAME }}
          password: ${{ secrets.DOCKERHUB_TOKEN }}

      - name: Compute tags
        id: meta
        uses: docker/metadata-action@v5
        with:
          images: stuartf303/${{ matrix.image }}
          tags: |
            type=raw,value=dev,enable=${{ github.ref == 'refs/heads/main' }}
            type=raw,value=latest,enable=${{ github.ref == 'refs/heads/main' }}
            type=semver,pattern=v{{version}}

      - name: Build and push (linux/arm64)
        uses: docker/build-push-action@v6
        with:
          context: .
          file: ${{ matrix.dockerfile }}
          platforms: linux/arm64
          push: true
          tags: ${{ steps.meta.outputs.tags }}
          labels: ${{ steps.meta.outputs.labels }}
          cache-from: type=gha
          cache-to: type=gha,mode=max
```

Note: arm64 builds run under QEMU emulation and will be slow (the Blazor/.NET publish especially). The `type=gha` cache keeps re-runs cheap. This is acceptable for now; a native-arm runner is a later optimisation.

- [ ] **Step 2: Validate the workflow YAML**

Run:
```bash
python -c "import yaml; yaml.safe_load(open('.github/workflows/publish-images.yml'))" && echo YAML_OK
```
Expected: `YAML_OK`. (If `actionlint` is available: `actionlint .github/workflows/publish-images.yml` — optional.)

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/publish-images.yml
git commit -m "ci(010): publish arm64 images to Docker Hub (stuartf303)"
```

- [ ] **Step 4: Human-gated real run (NOT auto-verified)**

This proves the pipeline but needs the Docker Hub token. Record as a manual step, do not mark the plan complete on its behalf:
1. On Docker Hub, create an access token (Account Settings → Security → New Access Token).
2. In GitHub repo Settings → Secrets and variables → Actions, add `DOCKERHUB_USERNAME=stuartf303` and `DOCKERHUB_TOKEN=<token>`.
3. Trigger: `gh workflow run publish-images.yml --ref <branch>` (or merge to `main`).
4. Verify: `stuartf303/vandaemon-api:dev` and `stuartf303/vandaemon-web:dev` appear on Docker Hub as `linux/arm64`.

---

### Task 6: Provisioning guide + manual E2E checklist

**Files:**
- Create: `docs/deployment/pi-provisioning.md`
- Modify: `docs/deployment/raspberry-pi-setup.md` (add a pointer near the top of "Initial Setup")

**Interfaces:**
- Consumes: all prior task artifacts (by reference).
- Produces: human-facing docs; no code consumers.

- [ ] **Step 1: Write the new provisioning guide**

```markdown
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
```

- [ ] **Step 2: Add a pointer from the existing setup guide**

In `docs/deployment/raspberry-pi-setup.md`, immediately under the `## Initial Setup` heading (line ~25), insert:

```markdown
> **Recommended:** For the containerised pull-based install (Pi pulls prebuilt
> images from Docker Hub instead of building from source), follow
> [pi-provisioning.md](pi-provisioning.md). The steps below are the fuller
> manual reference.
```

- [ ] **Step 3: Verify links resolve**

Run:
```bash
test -f docs/deployment/pi-provisioning.md && grep -q "pi-provisioning.md" docs/deployment/raspberry-pi-setup.md && echo DOCS_OK
```
Expected: `DOCS_OK`.

- [ ] **Step 4: Commit**

```bash
git add docs/deployment/pi-provisioning.md docs/deployment/raspberry-pi-setup.md
git commit -m "docs(010): containerised Pi provisioning guide + E2E checklist"
```

---

## Self-Review

**Spec coverage:**
- Registry/namespace `stuartf303` → Tasks 1, 5. ✓
- GitHub Actions arm64 build → Task 5. ✓
- Pi pulls prebuilt (no build) → Task 1 (`image:` only). ✓
- Manual OTA → Task 3. ✓
- `:dev`/`:latest` now, `:vX.Y.Z` later → Task 5 metadata tags. ✓
- Raspberry Pi OS Lite 64-bit + Imager flow → Task 6 guide. ✓
- Provision via `provision-pi.sh` → Task 4. ✓
- systemd autostart → Task 4 unit. ✓
- Hostname `vandaemon` check/repair w/ reboot warning → Task 2 (logic+test) + Task 4 (wired). ✓
- Mosquitto config on Pi → Task 1 (volume) + Task 4 (copies `docker/mosquitto`). ✓
- Idempotency / preflight / fail-safe update → Tasks 3, 4. ✓
- `DOCKERHUB_TOKEN` as GitHub secret (human step) → Task 5 Step 4. ✓
- Manual E2E checklist, not auto-claimed → Task 6. ✓

**Placeholder scan:** none — all scripts, YAML, and docs are shown in full.

**Type/name consistency:** `check_and_fix_hostname` / `_hc_get_hostname` / `_hc_set_hostname` / `_hc_prompt` used identically in Tasks 2 and 4. `VANDAEMON_TAG`, `/opt/vandaemon`, `compose.pi.yml`, `stuartf303/vandaemon-{api,web}` consistent across Tasks 1, 3, 4, 5, 6. ✓
