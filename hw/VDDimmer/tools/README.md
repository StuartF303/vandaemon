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
| `polyidx.py` | scanline point-in-polygon for zone fills; pad-perimeter / via-disc / segment samplers. A pad *centre* test misses thermal-relief spokes -- always sample the perimeter |
| `connect.py` | per-net connected-component groups over tracks, vias, pads and zone islands. Run after every refill; this is what catches a split pour that DRC only whispers about |
| `emc.py` | pour continuity, F.Cu runs not backed by the B.Cu pour, return-via distances, GND coverage under the buck |
| `retvia.py` | proposes GND return vias that clear everything and land in GND fill on *both* layers |

## connect.py thresholds

`OVERLAP` (1e-4 mm) is what counts as conducting. Copper that merely *touches* -- a via
whose radius exactly equals its distance to a pad edge -- does not conduct, and KiCad
agrees; treating a touch as a connection once reported an unpowered ESP32 as fine.
`MARGINAL` (0.05 mm) reports contacts thin enough that etch tolerance could open them.
To ask "what breaks if the fab etches 50 um off everywhere", set `OVERLAP = 0.05` and
re-run.

## Known blind spot in route.py

It validates track polylines but does **not** synthesise a via at each layer transition,
so a via can sit too close to a neighbouring trace and still pass. Sweep proposed vias
explicitly against every pad, track and via before pushing.

## Refilling zones

Zone fills go stale whenever copper is added. With KiCad **closed**:

    bash hw/VDDimmer/tools/drc.sh

With KiCad open, that would be overwritten by KiCad's next save — press `B` then
`Ctrl+S` in the PCB editor instead.
