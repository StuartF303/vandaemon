"""
Stamp each build with a unique identity, so "is the board running the binary I
just built?" is a question with an actual answer.

FW_VERSION alone cannot answer it -- it is a static "1.0.0" and stays that way
across every rebuild, so a board that silently kept an old image looks identical
to one that was just flashed. That is precisely the failure this project hit:
builds that produced no firmware.bin left a stale image on the board with
nothing reporting a problem.

Injects -DBUILD_ID="<short-sha><-dirty> <UTC timestamp>" and writes the same
string to $BUILD_DIR/build_id.txt. tools/Build-Flash.ps1 reads that file and
matches it against the boot banner over serial after flashing.
"""

import datetime
import os
import subprocess

Import("env")  # noqa: F821  (injected by SCons)


def _git(*args, default=""):
    try:
        out = subprocess.run(
            ("git",) + args,
            cwd=env.subst("$PROJECT_DIR"),  # noqa: F821
            capture_output=True,
            text=True,
            timeout=10,
        )
        return out.stdout.strip() if out.returncode == 0 else default
    except Exception:
        return default


# Build-Flash.ps1 pins this so the build step and the upload step agree.
#
# Without the pin, `pio run` and `pio run -t upload` each generate their own
# timestamp. The upload's differs from the build's, which (a) changes a global
# -D and so recompiles every object, and (b) means the binary that was verified
# is not the binary that gets flashed -- the sha256 on the receipt would belong
# to a file that no longer exists. Observed 2026-09-10: verified 14:08:20Z,
# flashed 14:08:59Z.
build_id = os.environ.get("VANDIMMER_BUILD_ID", "").strip()

if not build_id:
    sha = _git("rev-parse", "--short=8", "HEAD", default="nogit")
    dirty = "-dirty" if _git("status", "--porcelain", "--untracked-files=no") else ""
    stamp = datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
    build_id = "%s%s %s" % (sha, dirty, stamp)

env.Append(CPPDEFINES=[("BUILD_ID", env.StringifyMacro(build_id))])  # noqa: F821

build_dir = env.subst("$BUILD_DIR")  # noqa: F821
try:
    os.makedirs(build_dir, exist_ok=True)
    with open(os.path.join(build_dir, "build_id.txt"), "w", encoding="utf-8") as fh:
        fh.write(build_id + "\n")
except OSError as exc:
    print("[build-id] WARNING: could not write build_id.txt: %s" % exc)

print("[build-id] %s" % build_id)
