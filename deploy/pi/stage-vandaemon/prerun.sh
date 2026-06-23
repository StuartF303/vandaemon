#!/bin/bash -e
# pi-gen stage entry point. Standard pattern: clone the previous stage's rootfs if
# this stage hasn't started yet, so our steps layer on top of stage2 (Lite).
# shellcheck disable=SC2154  # ROOTFS_DIR is provided by pi-gen at runtime.

if [ ! -d "${ROOTFS_DIR}" ]; then
	copy_previous
fi
