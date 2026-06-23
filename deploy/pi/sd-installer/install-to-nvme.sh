#!/usr/bin/env bash
# VanDaemon SD-installer (SECONDARY / FALLBACK path).
#
# For the no-bench-machine case: boot the Pi from a microSD carrying this script + the
# prebuilt appliance image, write that image onto the NVMe, then reboot to NVMe. The SD
# is removed afterward. The PRIMARY path is flashing the NVMe directly on a bench machine
# (see docs/deployment/pi-appliance-setup.md) — prefer it when you have a USB-NVMe adapter.
#
# Safety: refuses to run if no NVMe target is found; never writes to an SD/MMC device;
# requires an explicit confirmation unless ASSUME_YES=1.
set -euo pipefail

IMAGE="${1:-/boot/firmware/vandaemon-appliance.img}"
TARGET="${NVME_TARGET:-/dev/nvme0n1}"

log() { echo "[sd-installer] $*"; }
die() { echo "[sd-installer] ERROR: $*" >&2; exit 1; }

[ "$(id -u)" -eq 0 ] || die "must run as root."
[ -f "${IMAGE}" ] || die "appliance image not found at ${IMAGE} (pass a path as arg 1)."

# Guard: target must be a real NVMe block device, not an SD/MMC card.
case "${TARGET}" in
	/dev/nvme*) : ;;
	*) die "refusing: target '${TARGET}' is not an NVMe device." ;;
esac
[ -b "${TARGET}" ] || die "NVMe target '${TARGET}' not present. Is the HAT/drive seated?"

log "About to OVERWRITE ${TARGET} with ${IMAGE}."
if [ "${ASSUME_YES:-0}" != "1" ]; then
	read -r -p "Type 'yes' to continue: " ans
	[ "${ans}" = "yes" ] || die "aborted by user."
fi

log "Writing image to ${TARGET} (this takes several minutes)..."
dd if="${IMAGE}" of="${TARGET}" bs=4M conv=fsync status=progress

sync
log "Image written. Expanding is handled by the appliance on its first NVMe boot."
log "Remove the SD card, then reboot to boot from NVMe."

if [ "${ASSUME_YES:-0}" = "1" ]; then
	log "ASSUME_YES set — rebooting in 10s. Remove the SD now."
	sleep 10
	reboot
fi
