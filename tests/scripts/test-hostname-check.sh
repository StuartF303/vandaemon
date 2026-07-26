#!/usr/bin/env bash
set -uo pipefail
cd "$(dirname "$0")/../.."
source scripts/lib/hostname-check.sh

fails=0
assert_eq() { # $1 actual  $2 expected  $3 name
  if [[ "$1" == "$2" ]]; then
    echo "PASS: $3"
  else
    echo "FAIL: $3 (got '$1' want '$2')"
    fails=$((fails + 1))
  fi
}

marker="$(mktemp)"

# Case 1: hostname already correct -> OK, no set call
DESIRED_HOSTNAME=vandaemon
_hc_get_hostname() { echo vandaemon; }
_hc_prompt() { return 0; }
: > "$marker"
_hc_set_hostname() { echo called >> "$marker"; }
out="$(check_and_fix_hostname 2>/dev/null)"
assert_eq "$out" "OK" "already-correct returns OK"
assert_eq "$(cat "$marker")" "" "set NOT called when correct"

# Case 2: wrong hostname, user says yes -> FIXED, set called
_hc_get_hostname() { echo raspberrypi; }
_hc_prompt() { return 0; }
: > "$marker"
out="$(check_and_fix_hostname 2>/dev/null)"
assert_eq "$out" "FIXED" "wrong+yes returns FIXED"
assert_eq "$(cat "$marker")" "called" "set called on yes"

# Case 3: wrong hostname, user says no -> DECLINED, set NOT called
_hc_prompt() { return 1; }
: > "$marker"
out="$(check_and_fix_hostname 2>/dev/null)"
assert_eq "$out" "DECLINED" "wrong+no returns DECLINED"
assert_eq "$(cat "$marker")" "" "set NOT called on no"

rm -f "$marker"
echo "---"
[[ "$fails" -eq 0 ]] && echo "ALL PASS" || echo "$fails FAILURES"
exit "$fails"
