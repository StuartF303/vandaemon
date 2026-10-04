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
