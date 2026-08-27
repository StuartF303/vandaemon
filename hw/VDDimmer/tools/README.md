# VDDimmer layout tooling

Run everything with **KiCad's Python 3.11**:
`"C:/Program Files/KiCad/10.0/bin/python.exe"` — the system Python is 3.6 and will fail.

| Script | Use |
|---|---|
| `drc.sh` | kicad-cli DRC, refills zones and saves. **Only run with KiCad closed** — it writes the board. |
| `geom.py` | board model: `load()` -> pads, tracks, vias, plus distance helpers |
| `route.py` | offline clearance validator; `check(routes)` before pushing anything |
| `measure.py` | the section 8.5 gate table |
| `pads.py` `crtyd.py` `cut.py` | pad dumps, courtyard boxes, routing capacity across a cut line |
| `vias.py` `gndstub.py` | via and stub generators |

## Known blind spot in route.py

It validates track polylines but does **not** synthesise a via at each layer transition,
so a via can sit too close to a neighbouring trace and still pass. Sweep proposed vias
explicitly against every pad, track and via before pushing.

## Refilling zones

Zone fills go stale whenever copper is added. With KiCad **closed**:

    bash hw/VDDimmer/tools/drc.sh

With KiCad open, that would be overwritten by KiCad's next save — press `B` then
`Ctrl+S` in the PCB editor instead.
