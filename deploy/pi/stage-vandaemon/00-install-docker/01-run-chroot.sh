#!/bin/bash -e
# Install Docker Engine + Compose plugin INSIDE the appliance rootfs (chroot), from
# Docker's official apt repository, and enable the docker service to start on boot.
# Runs in the chroot, so apt/systemctl target the appliance image, not the build host.

# Docker's official repo for Debian (Raspberry Pi OS Bookworm is Debian-based), arm64.
install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/debian/gpg \
	-o /etc/apt/keyrings/docker.asc
chmod a+r /etc/apt/keyrings/docker.asc

# shellcheck disable=SC1091  # os-release exists in the chroot at runtime.
. /etc/os-release
cat >/etc/apt/sources.list.d/docker.list <<EOF
deb [arch=arm64 signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/debian ${VERSION_CODENAME} stable
EOF

export DEBIAN_FRONTEND=noninteractive
apt-get update
apt-get install -y --no-install-recommends \
	docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin

# Start the stack's runtime on boot. (Per-container resilience is handled by
# `restart: unless-stopped` in the compose file.)
systemctl enable docker

# Let the appliance's first user manage Docker without sudo.
usermod -aG docker vandaemon || true

# Keep the image lean.
apt-get clean
rm -rf /var/lib/apt/lists/*
