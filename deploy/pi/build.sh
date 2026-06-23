#!/usr/bin/env bash
# Build the VanDaemon Pi-5 appliance image with pi-gen (Docker build path).
#
# MUST run on Linux / WSL2 / Docker (pi-gen is NOT native to Windows).
# Produces an arm64 .img with the consolidated stack + the prebuilt GHCR images baked
# in (offline-capable first boot). Re-runnable; nothing irreversible to the build host.
#
#   VANDAEMON_REGISTRY  GHCR namespace (default: ghcr.io/stuartf303)
#   VANDAEMON_TAG       image tag to bake (default: latest)
#   PI_GEN_REF          pi-gen git ref (default: arm64)
#
# Usage:  cd deploy/pi && cp config/pi-gen-config.example config/pi-gen-config && ./build.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
PI_GEN_DIR="${SCRIPT_DIR}/pi-gen"
STAGE_SRC="${SCRIPT_DIR}/stage-vandaemon"
STAGE_DST="${PI_GEN_DIR}/stage-vandaemon"
CONFIG_SRC="${SCRIPT_DIR}/config/pi-gen-config"

VANDAEMON_REGISTRY="${VANDAEMON_REGISTRY:-ghcr.io/stuartf303}"
VANDAEMON_TAG="${VANDAEMON_TAG:-latest}"
PI_GEN_REF="${PI_GEN_REF:-arm64}"

log() { echo "[build] $*"; }
die() { echo "[build] ERROR: $*" >&2; exit 1; }

command -v docker >/dev/null || die "docker not found. Run on Linux/WSL2 with Docker."
[ -f "${CONFIG_SRC}" ] || die "missing ${CONFIG_SRC} — copy config/pi-gen-config.example to it first."
[ -f "${REPO_ROOT}/docker-compose.yml" ] || die "repo docker-compose.yml not found at ${REPO_ROOT}."

# 1. Clone / refresh pi-gen.
if [ ! -d "${PI_GEN_DIR}/.git" ]; then
	log "Cloning pi-gen (${PI_GEN_REF}) into ${PI_GEN_DIR}."
	git clone --depth 1 --branch "${PI_GEN_REF}" https://github.com/RPi-Distro/pi-gen.git "${PI_GEN_DIR}"
else
	log "pi-gen already present; fetching ${PI_GEN_REF}."
	git -C "${PI_GEN_DIR}" fetch --depth 1 origin "${PI_GEN_REF}" && git -C "${PI_GEN_DIR}" checkout "${PI_GEN_REF}"
fi

# 2. Copy our custom stage into pi-gen.
log "Staging custom stage-vandaemon."
rm -rf "${STAGE_DST}"
cp -a "${STAGE_SRC}" "${STAGE_DST}"

# 3. Stage the consolidated stack + broker config into the image's /opt/vandaemon.
STACK_FILES="${STAGE_DST}/01-vandaemon-stack/files/opt/vandaemon"
log "Staging docker-compose.yml + mosquitto config into ${STACK_FILES}."
mkdir -p "${STACK_FILES}/docker/mosquitto" "${STACK_FILES}/images"
cp "${REPO_ROOT}/docker-compose.yml" "${STACK_FILES}/docker-compose.yml"
cp -a "${REPO_ROOT}/docker/mosquitto/config" "${STACK_FILES}/docker/mosquitto/config"

# Pin the registry/tag the appliance pulls/runs (used by compose ${VANDAEMON_*}).
cat >"${STACK_FILES}/.env" <<EOF
VANDAEMON_REGISTRY=${VANDAEMON_REGISTRY}
VANDAEMON_TAG=${VANDAEMON_TAG}
EOF

# 4. Pull arm64 images and docker save them so first boot is offline-capable.
API_IMG="${VANDAEMON_REGISTRY}/vandaemon-api:${VANDAEMON_TAG}"
WEB_IMG="${VANDAEMON_REGISTRY}/vandaemon-web:${VANDAEMON_TAG}"
MQTT_IMG="eclipse-mosquitto:2.0"
for img in "${API_IMG}" "${WEB_IMG}" "${MQTT_IMG}"; do
	log "Pulling ${img} (linux/arm64)."
	docker pull --platform linux/arm64 "${img}" || die "failed to pull ${img} (check GHCR access / tag)."
done
log "Saving image tarballs."
docker save "${API_IMG}"  -o "${STACK_FILES}/images/vandaemon-api.tar"
docker save "${WEB_IMG}"  -o "${STACK_FILES}/images/vandaemon-web.tar"
docker save "${MQTT_IMG}" -o "${STACK_FILES}/images/eclipse-mosquitto.tar"

# 5. Stage the boot-partition config example.
BOOT_FILES="${STAGE_DST}/02-firstboot/files/boot/firmware"
mkdir -p "${BOOT_FILES}"
cp "${SCRIPT_DIR}/boot/vandaemon-config.txt.example" "${BOOT_FILES}/vandaemon-config.txt.example"

# 6. Compose the pi-gen config: operator settings + our stage list.
RUN_CONFIG="${PI_GEN_DIR}/config"
cp "${CONFIG_SRC}" "${RUN_CONFIG}"
{
	echo ""
	echo "# --- injected by deploy/pi/build.sh ---"
	echo "STAGE_LIST=\"stage0 stage1 stage2 stage-vandaemon\""
} >>"${RUN_CONFIG}"

# 7. Build (pi-gen's own Docker path — keeps the host clean / reproducible).
log "Running pi-gen build-docker.sh (this takes a while)."
( cd "${PI_GEN_DIR}" && CONFIG_FILE="${RUN_CONFIG}" ./build-docker.sh )

log "Done. Image(s) under: ${PI_GEN_DIR}/deploy/"
ls -1 "${PI_GEN_DIR}/deploy/" 2>/dev/null || true
