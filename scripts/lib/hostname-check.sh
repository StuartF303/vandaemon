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
