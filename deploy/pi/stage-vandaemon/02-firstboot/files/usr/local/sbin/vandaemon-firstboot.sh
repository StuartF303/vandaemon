#!/bin/bash
# VanDaemon first-boot configurator (one-shot, headless, NO interactive prompts).
#
# Reads a single operator-editable file on the FAT boot partition
# (/boot/firmware/vandaemon-config.txt) and applies: hostname, WiFi, SSH public key,
# and optional MQTT auth. Then loads the baked container images and lets
# vandaemon.service bring the stack up. Self-disables on success.
#
# Contract: specs/006-pi-appliance-deploy/contracts/boot-config.md
# Safety: missing/blank fields fall back to documented defaults; we never block on input.
# Secrets are written 0600 and never committed.
set -uo pipefail

CONFIG_FILE="/boot/firmware/vandaemon-config.txt"
STATE_DIR="/var/lib/vandaemon"
DONE_MARKER="${STATE_DIR}/firstboot-done"
STACK_DIR="/opt/vandaemon"
MOSQ_CONF="${STACK_DIR}/docker/mosquitto/config/mosquitto.conf"
USER_NAME="vandaemon"

log() { echo "[vandaemon-firstboot] $*"; }

mkdir -p "${STATE_DIR}"

# --- safe KEY=VALUE getter (does NOT source the file; avoids arbitrary execution) ---
cfg() {
	local key="$1" default="${2-}" line val
	[ -f "${CONFIG_FILE}" ] || { printf '%s' "${default}"; return; }
	line="$(grep -E "^[[:space:]]*${key}=" "${CONFIG_FILE}" | tail -n1)"
	if [ -z "${line}" ]; then printf '%s' "${default}"; return; fi
	val="${line#*=}"
	val="$(printf '%s' "${val}" | sed -e 's/^[[:space:]]*//' -e 's/[[:space:]]*$//' -e 's/^"\(.*\)"$/\1/')"
	printf '%s' "${val}"
}

if [ ! -f "${CONFIG_FILE}" ]; then
	log "No ${CONFIG_FILE} found — applying safe defaults (hostname=vandaemon, DHCP, anonymous broker on trusted LAN)."
fi

# --- Hostname ---
HOSTNAME_VAL="$(cfg HOSTNAME vandaemon)"
if [ -n "${HOSTNAME_VAL}" ]; then
	log "Setting hostname to '${HOSTNAME_VAL}'."
	hostnamectl set-hostname "${HOSTNAME_VAL}" || log "WARN: hostnamectl failed."
	if grep -qE '^127\.0\.1\.1' /etc/hosts; then
		sed -i "s/^127\.0\.1\.1.*/127.0.1.1\t${HOSTNAME_VAL}/" /etc/hosts
	else
		printf '127.0.1.1\t%s\n' "${HOSTNAME_VAL}" >>/etc/hosts
	fi
fi

# --- WiFi (optional; Ethernet/DHCP works with no config) ---
WIFI_SSID="$(cfg WIFI_SSID)"
WIFI_PASSWORD="$(cfg WIFI_PASSWORD)"
WIFI_COUNTRY="$(cfg WIFI_COUNTRY GB)"
if [ -n "${WIFI_SSID}" ]; then
	log "Configuring WiFi for SSID '${WIFI_SSID}' (country ${WIFI_COUNTRY})."
	iw reg set "${WIFI_COUNTRY}" 2>/dev/null || true
	rfkill unblock wifi 2>/dev/null || true
	if nmcli -t -f NAME connection show 2>/dev/null | grep -qx vandaemon-wifi; then
		nmcli connection delete vandaemon-wifi || true
	fi
	if nmcli connection add type wifi con-name vandaemon-wifi ifname wlan0 ssid "${WIFI_SSID}" 2>/dev/null; then
		if [ -n "${WIFI_PASSWORD}" ]; then
			nmcli connection modify vandaemon-wifi \
				wifi-sec.key-mgmt wpa-psk wifi-sec.psk "${WIFI_PASSWORD}"
		fi
		nmcli connection modify vandaemon-wifi connection.autoconnect yes
		nmcli connection up vandaemon-wifi || log "WARN: WiFi did not connect now; will retry on autoconnect."
	else
		log "WARN: nmcli not available/failed; leaving network as-is (Ethernet/DHCP still works)."
	fi
fi

# --- SSH public key (key-based login; password auth stays off by default) ---
SSH_PUBKEY="$(cfg SSH_PUBKEY)"
if [ -n "${SSH_PUBKEY}" ]; then
	log "Installing SSH authorized key for ${USER_NAME}."
	SSH_DIR="/home/${USER_NAME}/.ssh"
	install -d -m 0700 -o "${USER_NAME}" -g "${USER_NAME}" "${SSH_DIR}"
	printf '%s\n' "${SSH_PUBKEY}" >"${SSH_DIR}/authorized_keys"
	chmod 0600 "${SSH_DIR}/authorized_keys"
	chown "${USER_NAME}:${USER_NAME}" "${SSH_DIR}/authorized_keys"
else
	log "No SSH_PUBKEY supplied — leaving SSH key-only auth as configured by the image."
fi

# --- Load baked container images (offline-capable first boot) ---
if ls "${STACK_DIR}/images/"*.tar >/dev/null 2>&1; then
	for tar in "${STACK_DIR}/images/"*.tar; do
		log "Loading image $(basename "${tar}")."
		docker load -i "${tar}" || log "WARN: docker load failed for ${tar}."
	done
else
	log "No baked image tarballs found — vandaemon.service will 'docker compose pull' (needs network)."
fi

# --- Optional MQTT auth (default: anonymous on trusted van LAN) ---
MQTT_AUTH="$(cfg MQTT_AUTH false)"
if [ "${MQTT_AUTH}" = "true" ] && [ -f "${MOSQ_CONF}" ]; then
	MQTT_USERNAME="$(cfg MQTT_USERNAME vandaemon)"
	MQTT_PASSWORD="$(cfg MQTT_PASSWORD)"
	if [ -z "${MQTT_PASSWORD}" ]; then
		MQTT_PASSWORD="$(head -c 18 /dev/urandom | base64 | tr -dc 'A-Za-z0-9' | head -c 24)"
		printf 'username=%s\npassword=%s\n' "${MQTT_USERNAME}" "${MQTT_PASSWORD}" \
			>"${STATE_DIR}/mqtt-credentials"
		chmod 0600 "${STATE_DIR}/mqtt-credentials"
		log "Generated MQTT password for '${MQTT_USERNAME}' (stored 0600 at ${STATE_DIR}/mqtt-credentials)."
	fi
	log "Enabling MQTT password auth."
	docker run --rm -v "${STACK_DIR}/docker/mosquitto/config:/c" eclipse-mosquitto:2.0 \
		mosquitto_passwd -b -c /c/passwd "${MQTT_USERNAME}" "${MQTT_PASSWORD}" \
		|| log "WARN: failed to create passwd file."
	sed -i 's/^allow_anonymous true/allow_anonymous false/' "${MOSQ_CONF}"
	sed -i 's|^# password_file /mosquitto/config/passwd|password_file /mosquitto/config/passwd|' "${MOSQ_CONF}"
	sed -i 's|^# acl_file /mosquitto/config/acl.conf|acl_file /mosquitto/config/acl.conf|' "${MOSQ_CONF}"
	if [ -f "${STACK_DIR}/docker/mosquitto/config/acl.conf.example" ] && \
		[ ! -f "${STACK_DIR}/docker/mosquitto/config/acl.conf" ]; then
		cp "${STACK_DIR}/docker/mosquitto/config/acl.conf.example" \
			"${STACK_DIR}/docker/mosquitto/config/acl.conf"
	fi
fi

# --- Done: mark complete and disable this one-shot ---
touch "${DONE_MARKER}"
systemctl disable vandaemon-firstboot.service 2>/dev/null || true
log "First-boot configuration complete; vandaemon.service will start the stack."
exit 0
