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
