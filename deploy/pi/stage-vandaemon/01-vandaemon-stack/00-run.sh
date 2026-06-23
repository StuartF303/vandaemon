#!/bin/bash -e
# Copy the VanDaemon stack into the appliance rootfs at /opt/vandaemon.
# The contents of files/opt/vandaemon are populated by deploy/pi/build.sh at build time:
#   - docker-compose.yml          (the consolidated root stack)
#   - docker/mosquitto/config/*   (broker config; bind-mounted by compose)
#   - images/*.tar                (docker-saved arm64 api/web/mqtt images)
#   - .env                        (VANDAEMON_REGISTRY / VANDAEMON_TAG pin)
#
# NOTE: image tarballs are loaded by firstboot.sh (the Docker daemon is not running
# inside the pi-gen chroot, so `docker load` cannot happen here).
# shellcheck disable=SC2154  # ROOTFS_DIR is provided by pi-gen at runtime.

SRC="$(dirname "$0")/files/opt/vandaemon"

if [ ! -f "${SRC}/docker-compose.yml" ]; then
	echo "ERROR: ${SRC}/docker-compose.yml missing — run deploy/pi/build.sh (it stages the compose + images)." >&2
	exit 1
fi

install -d "${ROOTFS_DIR}/opt/vandaemon"
cp -a "${SRC}/." "${ROOTFS_DIR}/opt/vandaemon/"

# Sanity: the saved image tarballs should be present so first boot is offline-capable.
if ! ls "${ROOTFS_DIR}/opt/vandaemon/images/"*.tar >/dev/null 2>&1; then
	echo "WARNING: no image tarballs staged in /opt/vandaemon/images — first boot will fall back to 'docker compose pull' (needs network)." >&2
fi
