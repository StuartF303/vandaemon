# VANDIMMER-4CH+2A enclosure — Rev A board

Two-part printed case for the board going into the van. Designed 2026-09-20.

**Status: PRINTED 2026-09-23 and it fits.** Geometry verified against the built mesh (80
checks) and now against the real board. The connector heights below were assumed from
datasheets rather than measured, and the print settles that in practice — but they are still
assumptions in the source, so a Rev B re-run inherits them.

| file | what it is |
|---|---|
| `extract_board.py` | reads the KiCad board + its 3D models, writes `board_revA.scad` |
| `board_revA.scad` | **generated** — outline, mounting holes, connector footprints and heights |
| `case.scad` | the case; every board dimension comes from the generated file |
| `verify_case.py` | renders the STLs and measures them against case.scad's own parameters |
| `renders/` | review images |
| `build/` | STL output, not committed |

```powershell
& "C:/Program Files/KiCad/10.0/bin/python.exe" extract_board.py      # after any board change
& "C:/Program Files/KiCad/10.0/bin/python.exe" verify_case.py        # 76 checks, must be 0 failed
& 'C:\Program Files\OpenSCAD\openscad.exe' -o build\base.stl -D 'part="base"' case.scad
& 'C:\Program Files\OpenSCAD\openscad.exe' -o build\lid.stl  -D 'part="lid"'  case.scad
```

`part` selects what renders: `base`, `lid` (print orientation), `lid_asm`, `gauge`,
`section`, `assembly`, `params`.

## Decisions

**The board is Rev A**, from the `ordered-revA` tag. The in-progress Rev B board moves to
M2.5 mounting holes and shifts every connector, so it will need its own printed case — but
only a re-run of `extract_board.py`, not a redesign.

**Split at the top (approach A).** Base tub carries the board and all four walls; the lid is
a flat plate with a locating lip. Two alternatives were rejected: splitting at the top of the
cable opening removes all bridging but puts the joint line through the one opening that takes
load from people pulling on plugs, and a clamshell split at board level puts every cutout in
the part you remove for service.

**One full-width letterbox** for the six harness connectors rather than six windows, so a
plug body wider than its header cannot miss its slot. Ribs in the gaps between connectors
break the bridge into ~20 mm spans; `letterbox_pillars = false` gives one clear opening.

**Mounting: four external ears** at floor level, M4. The screws stay reachable with the lid
on and no fastener enters the enclosed volume. Each ear's root reaches *through* the corner
boss and into the flat wall: welded to the round boss alone it was a poor load path for a
case bolted inside a moving vehicle, and mesh connectivity alone does not catch that, since
a tangential joint still reads as one solid.

**Lid: four M3 self-tappers** into bosses at the outer corners. The bosses have to straddle
the corners: pulled inboard they foul the board, because the cavity is only 0.6 mm larger
than the PCB.

**Antenna inside.** A marked 42 × 11 mm recess inside the left wall, chosen over the lid
because a flat antenna lying parallel to and close above the ground plane is the worst place
for it. Clear of the USB opening and of the button headers.

## Dimensions

| | mm | from |
|---|---|---|
| Board | 100 × 84 × 1.6 | board file |
| Clearance around board | 0.6 per side | print tolerance |
| Wall / floor / lid | 3.0 | as specified |
| Board above floor | 4.0 | clears through-hole tails |
| Space above board | 17.0 | tallest part is 8.55 mm |
| Shell outer | 107.25 × 91.25 × 28.6 | derived |
| Overall with ears | ~142 × 101 | ears reach 17.75 mm each side |
| Bridge above letterbox | 5.5 | derived; the number to watch |

Part heights come from the KiCad 3D models, measured by `extract_board.py`: pin headers
8.55, J2 power 7.56, harness connectors 6.11, MOSFETs 2.33, LED 1.61. Parts whose model is
missing (J1, U1, L1, L3, U2, D7) use datasheet values from the `ASSUMED` table and are
labelled `assumed` in the generated file. The script refuses to emit a zero.

## Openings

| opening | where | serves |
|---|---|---|
| Letterbox, 77.5 × 12.3 | front wall | J3–J6 lamps, J7/J8 addressable |
| 16.3 wide | back wall | J2 power, sized for the mating plug |
| 13.7 wide | left wall | J1 USB-C plus plug overmould |
| Hatch 7.6 × 22.7 | lid | J9/J10 12 V/5 V jumpers |
| 5 mm hole | lid | D7 status LED |
| Slots | lid and back wall | over the buck cluster and the MOSFET row |

Channel numbers `1 2 3 4 A1 A2` are engraved under the letterbox, each at its connector's
own X centre.

## Verification

`verify_case.py` renders the parts and measures the **mesh**, not the constants. Expected
values are echoed by `case.scad` itself, so a parameter that is right while the geometry
using it is wrong still fails. 80 checks: single connected solid per part, outer envelope,
no case material inside any of the 21 components' volumes, a clear path out through the wall
for each harness connector, pillars only in the gaps, posts at the board's hole positions
with empty pilots, an unbroken load path from each ear into the wall, lid features over what
they serve, and lip-to-wall clearance.

The checks were proved non-vacuous by reintroducing four defects and watching them go red:

| injected defect | caught by |
|---|---|
| posts shifted 2 mm in X | 4 post-position checks |
| pillars 4 mm wider than their gaps | 5 connector-path checks + the pillar check |
| labels cut inside the wall | single-solid check (9 components) |
| ear root back on the boss edge | 4 ear-continuity checks |

Sampling a solid by ray casting needs care: a ray through a shared triangle edge is counted
twice or not at all, and that is systematic rather than rare — every sample along a
cylinder's centre line lands on the spokes of its cap fan, so a solid ear tip read as hollow.
`Mesh.inside` re-casts a few microns off when it grazes an edge.

That last one is a real bug this found: the engraving started 0.2 mm inside the wall, leaving
a skin over every glyph. It renders as perfect engraving and prints as eight sealed voids.

## Printing

PETG, not PLA: a van interior reaches 40 °C+ and PLA softens from about 60 °C. The board
dissipates 2.5–3 W, roughly 70 % of it in the buck corner.

- **base** — as oriented, floor down, no supports. The bridge above the letterbox is the
  only unsupported span; with the pillars the longest is about 22 mm.
- **lid** — `part="lid"` is already flipped for printing, lip upward. The legend is
  engraved, not raised, so it prints against the bed.
- 0.2 mm layers, 3 perimeters, 20–25 % infill.

## Open items

1. **Settled by the print (2026-09-23):** the case fits the board. That covers the hole
   pitch, the letterbox height and the pillar clearances, which were the things the fit
   gauge existed to test.
2. **Still assumptions in the source**, carried into any Rev B re-run:
   `harness_conn_h_min = 9.5` (Molex 22-05-3021, against the 6.11 mm JST XH in the model) and
   `power_conn_h_min = 12.0` (Phoenix mating plug). Neither was measured with calipers.
3. **Bridge reinforcement** is available if the 5.5 mm band above the letterbox proves weak
   in service: more pillars, or drop `head` and reduce the opening.
4. The antenna recess is a marked flat area only. Whether the antenna sticks there
   acceptably, and how the U.FL pigtail routes, is untested.
5. Nothing is recorded about how the case behaves hot. The vents are sized by judgement, not
   measurement, against 2.5–3 W concentrated in the buck corner.
