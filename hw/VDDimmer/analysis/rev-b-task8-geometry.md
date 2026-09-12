# Rev B — Task 8 geometry record

All figures MEASURED, not remembered. Source is the KiCad 10 library footprint itself
(`RF_Module.pretty/ESP32-S3-WROOM-1.kicad_mod`, `MountingHole.pretty/
MountingHole_2.7mm_M2.5.kicad_mod`) read through `pcbnew`, per spec section 4
("Exact keepout dimensions to be taken from the WROOM-1 datasheet at layout time, not
from memory").

## Board outline — UNCHANGED at 100.00 x 84.00 mm

`Edge.Cuts` extents: x 100.00..200.00, y 56.00..140.00. Inside the <=100 x 100 mm price
bracket. Face assignment per spec section 5:

| edge | coordinate | contents |
|---|---|---|
| top (user face) | y = 56 | J11, J12 buttons + D7 status LED |
| left | x = 100 | J2 (12 V in), above J3 |
| bottom (harness face) | y = 140 | J3 J4 J5 J6 J7 J8 J1 |
| right (antenna edge) | x = 200 | U1, antenna overhanging |

## ESP32-S3-WROOM-1 antenna keepout — the real number

The footprint carries its own rule area covering **F.Cu, B.Cu and In1..In30.Cu** (so it
already spans the new 4-layer stack), with `tracks not_allowed`:

| item | local extents (rot 0) | size |
|---|---|---|
| all-layer keepout | x -24.00..24.00, y -27.75..-6.75 | **48.00 x 21.00 mm** |
| F.Fab body | x -12.80..12.80, y -9.05..9.05 (at rot 270) | **25.60 x 18.10 mm** |
| pads | x -8.75..8.75, y -5.26..12.50 | 17.50 x 17.76 mm |

At rot 0 the antenna points -y. Measured across all four rotations in `pcbnew`:

| rotation | keepout lands at | antenna points |
|---|---|---|
| 0 | y -27.75..-6.75 | up (-y) |
| 90 | x -27.75..-6.75 | left (-x) |
| 180 | y +6.75..+27.75 | down (+y) |
| **270** | **x +6.75..+27.75** | **right (+x)** <- what we want |

## U1 target placement: (193.50, uy) at rotation 270

Derived from the measured offsets, with the right board edge at x = 200:

| feature | absolute x | verdict |
|---|---|---|
| pads | 181.00 .. 198.76 | on board, 1.24 mm to edge |
| module body (F.Fab) | 180.70 .. **206.30** | **6.30 mm overhangs the edge** |
| all-layer keepout | 200.25 .. 221.25 | **100% off board** |

6.30 mm of overhang is the antenna section — Espressif's option (1), spec decision 5.
Constraint window is 193.25 <= ux <= 194.74 (lower bound puts the keepout exactly on the
edge, upper bound is the last position keeping every pad on board). 193.50 sits just
inside the lower bound for a 0.25 mm margin.

`uy` is set in Phase 4 with J13/J14. U1 spans uy +/- 9.05 in y. At a nominal uy = 88 the
body is y 78.95..97.05, which clears H2's keepout (y 61.55..67.45) and H3's (y
132.55..138.45) with room to spare.

## Mounting holes — M2.5, footprint changed in the schematic

`MountingHole:MountingHole_2.7mm_M2.5` measured: NPTH **2.7 mm** drill, courtyard circle
radius **2.95 mm** => **5.90 mm keepout diameter**. Matches spec section 5.1 exactly.

H1-H4 currently sit 4.5 mm in from each corner:

| ref | position | role |
|---|---|---|
| H1 | (104.5, 64.5) | top-left |
| H2 | (195.5, 64.5) | top-right |
| H3 | (195.5, 135.5) | bottom-right |
| H4 | (104.5, 135.5) | **bottom-left — separates J2 (left edge) from J3 (harness edge)**, spec section 5.1 |

Positions are reviewed again in Phase 4 against the real placement, and plane necks are
measured at every hole before routing (Rev A pinched to 1.9 mm ~ 3.8 A at H2/H3).
