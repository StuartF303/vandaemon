#!/bin/bash -e
# Install the first-boot configurator + the stack auto-start unit into the appliance
# rootfs, enable them, write the Pi-5 NVMe boot params into config.txt, and stage the
# boot-partition config example.
# shellcheck disable=SC2154  # ROOTFS_DIR is provided by pi-gen at runtime.

HERE="$(dirname "$0")"
FILES="${HERE}/files"

# systemd units
install -d "${ROOTFS_DIR}/etc/systemd/system"
install -m 0644 "${FILES}/etc/systemd/system/vandaemon.service" \
	"${ROOTFS_DIR}/etc/systemd/system/vandaemon.service"
install -m 0644 "${FILES}/etc/systemd/system/vandaemon-firstboot.service" \
	"${ROOTFS_DIR}/etc/systemd/system/vandaemon-firstboot.service"

# first-boot script
install -d "${ROOTFS_DIR}/usr/local/sbin"
install -m 0755 "${FILES}/usr/local/sbin/vandaemon-firstboot.sh" \
	"${ROOTFS_DIR}/usr/local/sbin/vandaemon-firstboot.sh"

# enable both units (firstboot is ordered Before= the stack unit)
on_chroot <<-'EOF'
	systemctl enable vandaemon-firstboot.service
	systemctl enable vandaemon.service
EOF

# --- Pi 5 NVMe boot: rootfs-side config.txt params (the EEPROM BOOT_ORDER is a
#     SEPARATE one-time firmware step and CANNOT be baked in — see the verification
#     checklist). config.txt lives on the FAT boot partition (/boot/firmware). ---
CONFIG_TXT="${ROOTFS_DIR}/boot/firmware/config.txt"
if [ -f "${CONFIG_TXT}" ] && ! grep -q "VanDaemon NVMe" "${CONFIG_TXT}"; then
	cat >>"${CONFIG_TXT}" <<-'EOF'

		# --- VanDaemon NVMe (Pi 5) ---
		# Enable the PCIe/NVMe HAT. NOTE: booting FROM NVMe also requires a one-time
		# EEPROM BOOT_ORDER change on the Pi (e.g. 0xf416) — that is firmware, not in
		# this image. See docs/deployment/pi-appliance-verification-checklist.md.
		dtparam=pciex1
		dtparam=nvme
		# Optional PCIe Gen 3 (uncomment if your HAT/drive is rated for it):
		# dtparam=pciex1_gen=3
	EOF
fi

# --- Stage the boot-partition config EXAMPLE (no secrets). build.sh copies the repo's
#     deploy/pi/boot/vandaemon-config.txt.example into files/boot/firmware/ before build. ---
if [ -f "${FILES}/boot/firmware/vandaemon-config.txt.example" ]; then
	install -d "${ROOTFS_DIR}/boot/firmware"
	install -m 0644 "${FILES}/boot/firmware/vandaemon-config.txt.example" \
		"${ROOTFS_DIR}/boot/firmware/vandaemon-config.txt.example"
fi
