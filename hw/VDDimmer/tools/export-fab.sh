#!/usr/bin/env bash
# VANDIMMER-4CH+2A -> JLCPCB upload bundle.
#
# Does NOT use Konnect's export_manufacturing_package. That tool reports
# "No warnings" and emits assembly data that would misbuild the board:
#   * CPL of all 98 footprints, including 4 mounting holes, 14 connectors and 3 DNP
#   * CPL in INCHES (C20 lands at 6.496, which is 165 mm / 25.4)
#   * BOM with no LCSC column at all, ungrouped
# Its gerbers are fine, but kicad-cli makes them directly with less noise, so this
# skips it entirely.
#
# BOM and CPL come from tools/export-cpl.sh, which derives the machine-placed set
# from bom-lcsc.csv so the two files cannot disagree. This script re-checks that
# they still agree, and exits non-zero if they do not.
set -euo pipefail
CLI="C:/Program Files/KiCad/10.0/bin/kicad-cli.exe"
PY="C:/Program Files/KiCad/10.0/bin/python.exe"
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(dirname "$HERE")"
PCB="$ROOT/VANDIMMER-4CH2A.kicad_pcb"
OUT="$ROOT/fab/jlcpcb"
TMP="$ROOT/fab/gerbers"

rm -rf "$TMP" "$OUT"
mkdir -p "$TMP" "$OUT"

# Only the layers JLC consumes. Fab/Courtyard/Adhesive/Margin/User are noise.
"$CLI" pcb export gerbers --no-protel-ext \
    --layers "F.Cu,B.Cu,F.Mask,B.Mask,F.Silkscreen,B.Silkscreen,Edge.Cuts" \
    -o "$TMP/" "$PCB" >/dev/null
"$CLI" pcb export drill --format excellon --excellon-separate-th \
    -o "$TMP/" "$PCB" >/dev/null

bash "$HERE/export-cpl.sh" >/dev/null
cp "$ROOT/VANDIMMER-4CH2A-jlc-bom.csv" "$OUT/VANDIMMER-4CH2A-BOM.csv"
cp "$ROOT/VANDIMMER-4CH2A-cpl.csv"     "$OUT/VANDIMMER-4CH2A-CPL.csv"

"$PY" "$HERE/fabcheck.py" "$TMP" "$OUT"
echo
echo "upload from: $OUT"
