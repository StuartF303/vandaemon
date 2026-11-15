#!/usr/bin/env bash
# VANDIMMER-4CH+2A assembly-data export -- JLCPCB CPL + JLCPCB BOM.
# See tools/cpl.py for why the exclusion set comes from bom-lcsc.csv rather than
# from footprint attributes. Runs with KiCad's Python 3.11; the system Python is 3.6.
set -u
KICAD_BIN="${KICAD_BIN:-C:/Program Files/KiCad/10.0/bin}"
exec "$KICAD_BIN/python.exe" "$(dirname "$0")/cpl.py" "$@"
