#!/usr/bin/env bash
# VANDIMMER-4CH+2A DRC baseline check.
#
# Runs kicad-cli DRC on the saved board and reports:
#   - error count (after filtering known phantom-pad artefacts)
#   - phantom pad count, counted directly in the .kicad_pcb
#   - unconnected-item count
#
# Phantom pads (see HANDOFF.md gotcha #1) are unnumbered `thru_hole` pads with
# an empty `(layers)` list, injected by update_pcb_from_schematic on every
# footprint it ADDS. They appear as spurious drills and generate DRC errors of
# type `padstack` / `drill_out_of_range` / `annular_width` on a pad with no
# number. Legitimate unnamed pads exist too — `np_thru_hole` mounting holes and
# `smd` mechanical/shield pads — so the count must be narrowed to thru_hole.
set -u

BOARD="${1:-C:/Projects/vandaemon/hw/VDDimmer/VANDIMMER-4CH2A.kicad_pcb}"
CLI="/c/Program Files/KiCad/10.0/bin/kicad-cli.exe"
OUT="$(dirname "$0")/drc-report.json"

# --- phantom pads: structural check on the board file -----------------------
PHANTOM=$(grep -c 'pad "" thru_hole' "$BOARD" 2>/dev/null; true)
EMPTY_LAYERS=$(grep -cE '^[[:space:]]*(layers)[[:space:]]*$' "$BOARD" 2>/dev/null; true)

# --- DRC --------------------------------------------------------------------
"$CLI" pcb drc --format json --output "$OUT" --severity-error --severity-warning \
    --schematic-parity --exit-code-violations --refill-zones --save-board "$BOARD" >/dev/null 2>&1

python_bin="/c/Program Files/KiCad/10.0/bin/python.exe"
"$python_bin" - "$OUT" "$PHANTOM" "$EMPTY_LAYERS" <<'PY'
import json, sys
report, phantom, empty_layers = sys.argv[1], int(sys.argv[2]), int(sys.argv[3])
d = json.load(open(report, encoding="utf-8"))

viol = d.get("violations", [])
unconn = d.get("unconnected_items", [])
sch_par = d.get("schematic_parity", [])

# Known phantom-pad artefacts: violations naming a pad with no pad number.
PHANTOM_TYPES = {"padstack", "drill_out_of_range", "annular_width",
                 "hole_near_hole", "hole_clearance", "drill_too_small"}
def is_phantom(v):
    if v.get("type") not in PHANTOM_TYPES:
        return False
    desc = " ".join(str(i.get("description", "")) for i in v.get("items", []))
    return "Pad  " in desc or "Pad []" in desc or "Pad of " in desc and "pad  " in desc.lower()

errs  = [v for v in viol if v.get("severity") == "error"]
warns = [v for v in viol if v.get("severity") == "warning"]
filtered = [v for v in errs if is_phantom(v)]
real_errs = [v for v in errs if not is_phantom(v)]

print("=== VANDIMMER-4CH+2A DRC ===")
print(f"errors            : {len(real_errs)}   (filtered phantom-pad artefacts: {len(filtered)})")
print(f"warnings          : {len(warns)}")
print(f"phantom pads      : {phantom}   (empty (layers) blocks: {empty_layers})")
print(f"unconnected       : {len(unconn)}")
print(f"schematic parity  : {len(sch_par)}")

if real_errs:
    print("\n--- errors ---")
    for v in real_errs[:40]:
        items = "; ".join(str(i.get("description","")) for i in v.get("items", []))
        print(f"  [{v.get('type')}] {v.get('description')}  :: {items}")
if warns:
    print("\n--- warnings (first 20) ---")
    for v in warns[:20]:
        items = "; ".join(str(i.get("description","")) for i in v.get("items", []))
        print(f"  [{v.get('type')}] {v.get('description')}  :: {items}")
PY
