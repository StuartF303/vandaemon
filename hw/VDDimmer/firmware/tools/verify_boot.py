"""
Confirm a board is actually running the binary that was just built.

Opens the serial port, resets the board, and watches the boot banner for the
"[build] <id>" line emitted by main.cpp. Exits 0 only if that id matches the id
recorded in $BUILD_DIR/build_id.txt.

This is the step that makes "flashed successfully" mean something. Upload
reporting success does not prove the application image changed -- a build that
died partway can leave a stale firmware.bin, or none at all, and esptool will
cheerfully write whatever it is given.

Usage:
    verify_boot.py --port COM4 --expect-file .pio/build/4ch2a/build_id.txt
                   [--timeout 20] [--baud 115200]
"""

import argparse
import sys
import time

try:
    import serial
except ImportError:
    print("verify_boot: pyserial not available in this interpreter", file=sys.stderr)
    sys.exit(2)


def reset_board(ser):
    """Pulse the classic DTR/RTS reset. Harmless if the board ignores it."""
    try:
        ser.dtr = False
        ser.rts = True     # EN low  -> held in reset
        time.sleep(0.15)
        ser.rts = False    # EN high -> boot
        time.sleep(0.05)
    except Exception as exc:
        print("verify_boot: reset pulse failed (%s) -- will still listen" % exc)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--port", required=True)
    ap.add_argument("--expect-file", required=True)
    ap.add_argument("--baud", type=int, default=115200)
    ap.add_argument("--timeout", type=float, default=20.0)
    args = ap.parse_args()

    with open(args.expect_file, encoding="utf-8") as fh:
        expected = fh.read().strip()
    if not expected:
        print("verify_boot: build_id.txt is empty", file=sys.stderr)
        return 2

    print("verify_boot: expecting build id %r on %s" % (expected, args.port))

    try:
        ser = serial.Serial(args.port, args.baud, timeout=0.5)
    except Exception as exc:
        print("verify_boot: cannot open %s: %s" % (args.port, exc), file=sys.stderr)
        return 2

    transcript = []
    with ser:
        time.sleep(0.2)
        ser.reset_input_buffer()
        reset_board(ser)

        deadline = time.time() + args.timeout
        while time.time() < deadline:
            raw = ser.readline()
            if not raw:
                continue
            line = raw.decode("utf-8", errors="replace").strip()
            if not line:
                continue
            transcript.append(line)
            print("  | " + line)

            if line.startswith("[build]"):
                seen = line[len("[build]"):].strip()
                if seen == expected:
                    print("verify_boot: MATCH -- board is running this build")
                    return 0
                print(
                    "verify_boot: MISMATCH"
                    + chr(10) + "  built  : " + expected
                    + chr(10) + "  running: " + seen,
                    file=sys.stderr,
                )
                return 1

    print("verify_boot: timed out after %.0fs without a [build] line" % args.timeout,
          file=sys.stderr)
    if not transcript:
        print("verify_boot: the port produced no output at all.", file=sys.stderr)
        print("  This board has no USB power path -- apply 12 V to J1/J2.", file=sys.stderr)
        print("  If it is powered, press reset while this runs.", file=sys.stderr)
    else:
        print("verify_boot: output was seen but no [build] line -- either the "
              "running image predates build-id stamping, or it is crashing "
              "before the banner.", file=sys.stderr)
    return 1


if __name__ == "__main__":
    sys.exit(main())
