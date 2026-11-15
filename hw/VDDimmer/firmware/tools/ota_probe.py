#!/usr/bin/env python3
"""Is the board's ArduinoOTA listener up?

Used by Build-Flash.ps1 -Ota in two places: preflight, to fail fast with the
right diagnosis before spending minutes on a build; and confirm, to prove the
board rebooted and came back after an OTA push.

ArduinoOTA listens on **UDP** 3232. A TCP scan of that port shows "closed" even
on a perfectly healthy board, which is a trap worth knowing -- it sent one
session down the wrong path entirely. The only reliable test is the espota
handshake: send the invitation and see whether anything answers.

  "OK"           -> listening, no password
  "AUTH <nonce>" -> listening, password set
  (silence)      -> ArduinoOTA.begin() never ran, or nothing is there

SIDE EFFECT, and the reason this must never be used to sweep a subnet looking
for boards: on receiving the invitation the board calls Update.begin(), which
erases its *inactive* OTA partition, then fails to connect back to the host port
we deliberately do not serve, and aborts. Harmless -- it keeps running from the
active partition -- but it is a write, not a read.

Exit 0 if a listener answered, 1 if not, 2 on a usage or socket error.
"""
import argparse
import hashlib
import socket
import sys
import time

FLASH_COMMAND = 0
# A port we never bind, so the invitation cannot progress to a transfer.
DEAD_HOST_PORT = 48000


def probe(host, port, timeout):
    """Send one espota invitation. Returns the reply text, or None on silence."""
    payload = b"\0" * 1024
    msg = "%d %d %d %s\n" % (
        FLASH_COMMAND,
        DEAD_HOST_PORT,
        len(payload),
        hashlib.md5(payload).hexdigest(),
    )

    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    sock.settimeout(timeout)
    try:
        sock.sendto(msg.encode(), (host, port))
        reply, _ = sock.recvfrom(256)
        return reply.decode("utf-8", "replace").strip()
    except socket.timeout:
        return None
    finally:
        sock.close()


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--host", required=True, help="board IP or resolvable name")
    ap.add_argument("--port", type=int, default=3232)
    ap.add_argument(
        "--timeout", type=float, default=5.0, help="seconds per attempt"
    )
    ap.add_argument(
        "--retries",
        type=int,
        default=0,
        help="extra attempts after the first; use when waiting for a reboot",
    )
    ap.add_argument(
        "--interval", type=float, default=2.0, help="seconds between attempts"
    )
    args = ap.parse_args()

    try:
        socket.getaddrinfo(args.host, args.port, socket.AF_INET)
    except socket.gaierror as exc:
        print(f"cannot resolve '{args.host}': {exc}")
        return 2

    attempts = args.retries + 1
    for n in range(1, attempts + 1):
        reply = probe(args.host, args.port, args.timeout)
        if reply is not None:
            if reply.startswith(("OK", "AUTH")):
                print(f"OTA listener at {args.host}:{args.port} replied {reply!r}")
                return 0
            print(f"unexpected reply from {args.host}: {reply!r}")
            return 1
        if n < attempts:
            print(f"  no reply (attempt {n}/{attempts}), retrying...")
            time.sleep(args.interval)

    print(f"no OTA listener at {args.host}:{args.port} after {attempts} attempt(s)")
    return 1


if __name__ == "__main__":
    try:
        sys.exit(main())
    except KeyboardInterrupt:
        sys.exit(2)
