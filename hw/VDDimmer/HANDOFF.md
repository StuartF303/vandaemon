# VANDIMMER-4CH+2A — session handoff

Phase 3 (layout) is **electrically complete**. Schematic ERC 0/0, board DRC 0 errors, and
every remaining unconnected item is accounted for. What is left is fab preparation, and
the critical path there is **assembly data, not copper**.

State as of 2026-08-29. `git log --oneline -6 -- hw/VDDimmer` shows how it got here;
the working tree was clean when this was written.

## Start here

Everything below is verified against the tools in `tools/`. Re-derive rather than inherit —
two tooling bugs in earlier sessions each hid a defect that would have killed the board.

```bash
bash hw/VDDimmer/tools/drc.sh                                  # refill + DRC (KiCad CLOSED)
"C:/Program Files/KiCad/10.0/bin/python.exe" hw/VDDimmer/tools/connect.py   # all 61 nets
"C:/Program Files/KiCad/10.0/bin/python.exe" hw/VDDimmer/tools/emc.py
bash hw/VDDimmer/tools/export-bom.sh                           # 49-line BOM
```

## Current state

| | |
|---|---|
| Schematic | 5 sheets, 96 parts, **ERC 0 violations** |
| DRC | **0 errors**, 170 warnings |
| Unconnected | 15, every one benign — see the table below |
| Nets split into >1 group | 12 of 61, all benign |
| GND | **one connected group**; pours on both layers |
| BOM | **49 lines, 96 parts, $11.91/board**; 35 lines carry an LCSC code |
| Decisions | `DECISIONS-2026-08-29.md` — settled, do not re-litigate |

## Open work, in order

### 1. Two GUI-only items (no MCP tool writes either)

- **`(dnp yes)` on R11 and R12.** Their Assembly fields say so. All five text-only DNPs
  were in this state; L3/C20/R31 are now *fitted* by decision, so only these two remain.
  Without the real flag a generated BOM quotes and fits them.
- **Open U4 and U5's field dialogs** so KiCad propagates MPN/LCSC across their units.
  See the Konnect gotcha about multi-unit symbols — the BOM is correct today only
  because `export-bom.sh` groups by Value+Footprint rather than MPN.

### 2. The two I2C pull-ups — the last schematic change

Decision taken: fit 4.7 k from I2C_SDA and I2C_SCL to +3V3 near J13, LCSC **C17673**
(0805, Basic, 7 M stock). Provisional refs **R45/R46**.

This is the riskiest remaining step and needs planning, not improvisation:
adding symbols means `update_pcb_from_schematic`, which **injects a phantom pad on every
footprint it adds** — two new resistors, two phantom pads, cleared only by KiCad's
targeted right-click → Update Footprint. Never the global Tools → Update Footprints from
Library; that re-anchors and scrambles placement. Then both parts need placing and routing
near J13 on a board whose F.Cu is already full.

### 3. The fab blockers

- **Footprint `attr` is missing on all 96 footprints.** No `(attr smd)` anywhere, so
  `kicad-cli pcb export pos --smd-only` returns **0 rows** and the unfiltered export
  returns 96 including the four mounting holes. There is no usable CPL either way.
  Measured, not assumed. This is also what generates all 92 `lib_footprint_mismatch`
  warnings — **do not clear those with Tools → Update Footprints from Library.**
- **Six drill defects**, none previously investigated:
  - two GND vias at (176.112, 63.5) / (176.5, 63.5) — hole-to-hole **0.0000 mm**, the
    drills touch
  - pairs at 0.105 mm near (134.6, 126.8) and (145.9, 126.8)
  - J2 pad 2 vs a via at 0.200 mm against a 0.2495 mm minimum
  - a **duplicate EN_MCU via** — two drills co-located at exactly (150.6, 105.16)
- **No board name, no revision on silkscreen, 0 fiducials, 0 test points.**
- Silkscreen: 4 clipped by the board edge (H1/H2 refdes off the top, J1's outline off
  the left), 28 `silk_over_copper`, 24 `silk_overlap`.

### 4. Verify before ordering

- **L1/L3 land pattern.** The board land is two 2.50 × 6.00 mm pads on 4.90 mm centres
  (7.40 × 6.00 overall). The proposed PNLS6045-100M is a 6 × 6 mm body. Check the
  datasheet land before committing. Its 57 mΩ DCR also costs 0.51 W at 3 A — a 3.4 %
  efficiency hit on a 15 W converter, so a lower-DCR 10 µH in the same land is worth
  looking for.
- **WS2812B-V6 is not in JLC's catalogue.** Only V5/W and B/T. C2874885 (V5/W) is
  substituted in the BOM; confirm the pinout against the PLCC4 footprint.
- **R30's 0 Ω jumper carries ~1.2 A** and JLC lists only 125 mW for C17477. Confirm the
  current rating against UNI-ROYAL's datasheet or pick a jumper rated ≥ 2 A.
- **J1 is listed hand-fit** with the other connectors per spec §10 — but 16 pads at
  0.5 mm pitch plus four shield legs is not sensibly hand-soldered. Machine-place J1.

### 5. Documentation that contradicts the design

- `VANDIMMER-4CH-2ADDR-SPEC-v2.0.md` line 77 still says **SMBJ33A**. The board has an
  SMBJ18A and must. An SMBJ33A clamps at ~53 V, above the AP63301's 32 V absolute
  maximum, so a 24 V-capable version of this board cannot protect its own buck.
- The spec's channel-loss figure uses **15 mΩ** for the 20N06. The real part is **29 mΩ
  at the 4.5 V the 74HCT125 actually drives**; loss is 0.116 W per channel, not 0.06 W.
  Still only ~6 °C, so the no-thermal-vias decision stands, but the number was optimistic.
- **Firmware must be regenerated from the current schematic.** U1's GPIO map changed
  twice and U5's buffer channels 3↔4 were swapped during layout.

## Every remaining unconnected item, and why

`connect.py` with no arguments checks all 61 nets and agrees with KiCad exactly. None of
the twelve splits is a defect — each is a package-internal connection or a routing
limitation with a decision already taken.

| Net | Group | Verdict |
|---|---|---|
| +3V3 | U3.2 tab vs pin | benign, SOT-223 tab and pin 2 are one node inside the package |
| +5V | D9.1 | known, no USB-powered operation |
| +5V | 3 × zero-area prio-1 slivers | artefact, no copper |
| /MCU/BTN1 | J11.1 | known unroutable |
| /MCU/USB_DM, USB_DP | D8's pin pairs | benign, bonded inside the USBLC6 |
| /MCU/VBUS | J1.A4/B9, D9.2, C44.1 | known, 12 V-only board; USB is data-and-ESD only |
| /PWM/DRAIN1-4 | Q1-Q4 pad 2 | benign, DPAK centre lead |
| /Power/VIN_FUSED | Q5.2 | benign, same |
| VIN_PROT | J9.1, J10.1 | known, no 12 V for the addressable strips |

## What earlier sessions got wrong

Kept because each cost real time and would otherwise be rediscovered:

- **"DRC 0 errors" hid four dead-on-arrival defects.** KiCad reports a split net as an
  *unconnected item*, never an error. All 20 had been written off as benign; four were
  not — U4's gate drivers unpowered, U1's ESP32 unpowered, J1's GND floating, and a
  10.3 mm orphan +3V3 stub. `connect.py` exists to catch exactly this. Run it after
  every refill.
- **A via can be *exactly tangent* to a pad** — centre-to-pad-box distance equal to the
  via radius, zero overlap — and KiCad calls it unconnected while any `distance <= radius`
  check calls it connected. That is how the ESP32 read as powered when it was floating.
  `connect.py`'s `OVERLAP` is 1e-4 mm for this reason; `MARGINAL` (0.05 mm) reports copper
  thin enough that etch tolerance could open it.
- **The 33 "GND return vias" added at `1a1d10b` were inert.** There was no F.Cu GND pour,
  so 75 of 145 GND vias touched no F.Cu copper at all. A stitching via needs two GND
  references. Fixed by adding the F.Cu pour; `via_dangling` went 81 → 9.
- **`measure.py` did not run at all as committed** — missing `import os`, two calls using
  signatures `geom.py` does not have, and a zone parser matching two-space indentation
  against a tab-indented file. Numbers in the old gate table came from an unrecorded
  method. Any figure from a tool that does not currently run is unverified.
- **F1 was described as "a 10 A PPTC".** It is a 2410 *fuse*; PF1/PF2 are the PPTCs.
  That mattered — no 8 A PPTC exists in SMD at all, but an 8 A fuse dropped straight in.

## The F.Cu GND pour

Priority 0, board inset 0.5 mm, with a rectangular bite out of the top edge at
**x 172.5-186.0, y 60.5-78.5**. The bite keeps ground copper off SW and — because R21,
R22, C27 and R24 sit in that block with U2, C23, C24 and L1 — off the FB divider too.
Pad connection is **Solid** (`connect_pads yes`), which is what cleared six
`starved_thermal` errors. B.Cu GND under the buck is untouched: 100.0 % of 6400 sample
points, zero foreign tracks or vias.

**The return-via idea still mostly cannot be realised.** `retvia.py` could place only
3 of 30, and only one landed inside the 1.0 mm criterion. The reason is structural: on
2 layers the F.Cu side of most transitions sits inside a *power* pour, not GND — 14 of
the 27 failures are VIN_PROT vias inside the VIN_PROT pour, and both +5V bridge vias sit
in the +5V pour. Rev B needs a floorplan change or 4 layers.

## 8.5 gate — last measured

| Criterion | Gate | Measured | |
|---|---|---|---|
| Cin hot loop | < 15 mm² | 2.51 mm² | pass |
| Vias in hot loop bbox | 0 | 1 — GND at (177.137, 75.95) on U2.4's own pad, a shunt to plane | pass in spirit |
| SW → nearest non-buck net | ≥ 3 mm | 0.25 mm to BST | gate is mis-stated, see note |
| B.Cu GND under buck (fp +5 mm) | unbroken | 100.0 % of 6400 points | pass |
| B.Cu GND pour | continuous | 1 island, 7194.2 mm² | pass |
| F.Cu GND pour | — | 2664.9 mm², 49 islands, all tied through vias | new |
| Buck output +5V copper | 645 mm² | 571.8 mm², one connected group | under gate, but connected |
| VIN_PROT copper | 645 mm² | 2212.7 mm² | pass |
| FB divider → L1 / SW | away | R21 6.73 / 4.80 mm, R22 8.95 / 4.91, C27 8.61 / 7.03 | pass |
| F.Cu nets crossing a B.Cu pour gap | — | 42 of 60, worst 6.50 mm (DRAIN1) | see EMC |
| Layer transitions w/o return via ≤ 1 mm | — | 29 of 44 | structural |

**SW → BST is 0.25 mm**, not the 12.75 mm an early handoff recorded — that figure was the
distance to the nearest *non-buck* net. BST is the buck's own bootstrap and 0.25 mm is the
design clearance. The gate should name BST/L1/FB as excluded rather than rely on "non-buck"
being obvious.

**+5V is 571.8 mm² against a 645 mm² gate**, but it is one connected group, so this is a
copper-area and thermal question rather than a connectivity one. The addressable bus wall
is what costs the area.

## Copper held together by 23-50 µm

`connect.py` reports seven contacts thinner than 50 µm, four of them in the buck area
(both C23 tracks at 23 µm, U2.1 FB and C24.2 bootstrap at 25 µm). **None is load-bearing** —
re-running every net with the threshold raised to 50 µm, i.e. asking what breaks if the fab
over-etches, splits no net that was not already split. A tidiness item, worth nudging the
four buck-area tracks properly onto their pads if the board is opened again.

## Tooling — now in the repo, was in a session temp dir

`hw/VDDimmer/tools/` — paths are relocatable; run with KiCad's Python 3.11
(`C:/Program Files/KiCad/10.0/bin/python.exe`; the system Python is 3.6 and will fail).

| Script | Use |
|---|---|
| `drc.sh` | kicad-cli DRC with `--refill-zones --save-board --schematic-parity`, filters phantom-pad artefacts, prints errors / warnings / phantom / unconnected / parity |
| `geom.py` | board model: `load()` → pads, tracks, vias; `seg_dist`, `seg_seg_dist`, `seg_rect_dist` |
| `route.py` | **offline clearance validator** — `check(routes)` before every push |
| `measure.py` | the 8.5 gate table |
| `pads.py` / `crtyd.py` / `cut.py` | pad dumps, courtyard boxes, routing-capacity across a cut |
| `vias.py` / `gndstub.py` | via and stub generators |
| `polyidx.py` | scanline point-in-polygon for zone fills, with pad-perimeter / via-disc / segment samplers. A pad *centre* test misses thermal-relief spokes and over-splits every net |
| `connect.py` | **per-net connected-component groups** over tracks, vias, pads and zone islands. No argument = sweep all 61 nets and list the split ones plus every marginal contact. Run after every refill |
| `emc.py` | pour continuity, F.Cu runs not backed by the B.Cu pour, return-via distances, GND coverage under the buck |
| `retvia.py` | proposes GND return vias that are clear of everything *and* land in GND fill on both layers |

**`route.py` has one known blind spot**: it validates track polylines but does **not**
synthesise a via at each layer transition, so a via can be too close to a neighbouring
trace and still pass. That bit me once (a DM via 0.075 mm from a DP run). Sweep proposed
vias explicitly against pads/tracks/vias — the pattern is in this session's history.

**Two thresholds that matter in `connect.py`**: `OVERLAP` (1e-4 mm) is what counts as
conducting — set it to 0 and a tangent via reads as connected, which is how the ESP32's
supply went missing. `MARGINAL` (0.05 mm) is the reporting threshold for copper thin
enough that etch tolerance could open it.

## Konnect gotchas — these cost hours, do not rediscover them

1. **`update_pcb_from_schematic` injects phantom pads** on every footprint it *adds*.
   Only fix is KiCad's right-click → Update Footprint (targeted; the global
   Tools → Update Footprints from Library re-anchors and scrambles placement).
2. **`place_component`** creates a footprint with no schematic identity — the sync
   refuses it and it can never receive nets.
3. **`get_component_pads` / `query_traces` read the saved file.** `save_project` first.
4. **IPC needs `kicad.exe` AND the PCB editor open inside it.** `check_kicad_ui` reports
   `ipc_responsive: true` with only the project manager up, and vias still fail with
   `GetOpenDocuments AS_UNHANDLED`. The board must be opened by hand.
5. **`add_zone` / `add_copper_pour` are file inserts and refuse while KiCad holds the
   board.** Zones need the editor *closed*, vias/traces need it *open*. Plan the order.
6. **No tool deletes or re-prioritises a zone.** In the GUI, Zone Manager's **list order
   IS the priority** — there is no separate field; the up/down arrows write `(priority N)`.
7. **`delete_trace` works on via UUIDs too**, which is the only way to move a via.
8. **Verify every move and route by reading it back.**
9. Pin-header footprints anchor at **pin 1, not centre**.
10. **`add_copper_pour` succeeds but writes malformed-looking output** — appended after
    `(embedded_fonts …)`, two-space-indented in a tab-indented file, `(layers "F.Cu")`
    where KiCad writes `(layer …)`, and a bare `(fill yes)`. KiCad parses it, but a
    parser keyed on a tab-indented `(zone` opener will not see it. Run `drc.sh` (which
    re-emits the board canonically) *before* reading the zone back, or the read-back lies.
11. **No tool sets a zone's pad-connection mode.** A new pour is always thermal-relief,
    which on a crowded ground pour gives `starved_thermal` errors. Zone Properties →
    **Pad connection: Solid** in the GUI.
12. **`edit_schematic_component` writes unit 1 only of a multi-unit symbol**, where
    KiCad's GUI propagates fields to every unit. U4/U5 are 5-unit 74HCT125s, so a BOM
    grouped by MPN splits U5 into two lines and orders 3 of a 2-off part. Group by
    Value+Footprint (`tools/export-bom.sh`) and open the field dialog in the GUI to heal it.
13. **No tool sets KiCad's `(dnp yes)` attribute.** DNP written only into a Value string
    or an Assembly field does not reach the BOM — five parts on this board were in that
    state and would all have been quoted and fitted.

## Design decisions already made — do not re-litigate

- **12 V only.** D5 = SMBJ18A (29.2 V clamp, under the buck's 32 V abs max).
- **Buck = AP63301WU-7** (C2158003, TSOT-23-6, 3 A, 500 kHz). Internally compensated,
  so R25/R26/C28/C29 were deleted.
- **Cin = C23, 0402** (100nF/25V) bridging pin3 to pin4 from below.
- Connectors: J2 screw terminal; J3-J6 JST XH `S2B-XH-A`; J7/J8 `S4B-XH-A`.
- USB ESD: D8 USBLC6-2SC6.
- Input LC filter: L3 + C20 + R31 (1 Ω damping). **All three are now FITTED** — the
  2026-08-29 decision reversed the earlier DNP. See `DECISIONS-2026-08-29.md`.
- M3 mounting holes 4.5 mm in from each corner.
- 8.4's "USB D± 90 Ω differential" ignored — unachievable on 2-layer 1.6 mm and
  unnecessary at 12 Mbit full-speed.

### Changes made during layout that are firmware- or fab-visible

- **Three pin reshuffles**: U1's GPIO map changed twice, U5's buffer channels 3↔4 swapped
  (both OEs grounded, so functionally identical). ERC still 0/0. **Firmware must be
  regenerated from the current schematic, not the original pin plan.**
- **J1 was reversed and has been flipped.** It sat at (105.3, 112.0) rot 90, which put the
  solder tails at x 101.255 against the board edge and the mating opening at x 108.95
  facing *into* the board — the cable could only have gone in from the middle of the PCB.
  Now (103.65, 112.0) rot 270: opening at **x = 100.00**, flush with the west edge and
  facing out; tails at 107.695; shell pegs 102.60 / 106.78. Verified by pad read-back.
  If the enclosure wants the shell to overhang instead, shift J1 west accordingly.
- **R1 moved** (111.2, 111.5) → **(113.5, 111.5)** so it stopped blocking the USB fan-out.
  Its GDRV1 pad still lands on the existing y=111.2 lane.

## Remaining work

Superseded by **"Open work, in order"** at the top of this file. The only items from the
old list still outstanding and not repeated there:

- **Unroutable without placement changes** — all confirmed, not guesses: `BTN1` → J11.1;
  `VIN_PROT` → J9.1/J10.1 (no 12 V for the addressable strips); `+5V` → D9.1 (no
  USB-powered operation); `VBUS` → C44.1 and J1's north VBUS pad. Product decision taken:
  the board is 12 V-input-only and USB is data-and-ESD only.

## Known open items

Six items that used to live here were settled on 2026-08-29 — F1's rating, R30's package,
the I2C pull-ups, U3's quiescent drain, the input LC filter, and the DPAK thermal vias.
Read `DECISIONS-2026-08-29.md` rather than re-opening them. What remains:

- **Q5's tab is VIN_FUSED, not GND**; Q1-Q4's tabs are DRAIN1-4. All five have 0 thermal
  vias on 37 mm² tab pads. **This is correct and deliberate** — the spec's own numbers give
  0.116 W per channel (~6 °C) and 0.75 W for Q5 into 645 mm² (~34 °C). Vias would need
  bottom-layer islands that cut the GND pour and buy nothing.
- **The 4 DPAK centre-lead pads and D8's two internal pin pairs read as unconnected and
  cannot be routed** — GATE runs between each FET's centre lead and its tab, and D8's D±
  pin pairs are bonded inside the package. Electrically harmless.
- **U3 still dissipates 0.55 W** in SOT-223 (Tj 57.9 °C, margin 67 °C — safe but hot).
  The BL1117 swap fixed the quiescent drain, not the dissipation. Both share one cause:
  12 V → 5 V → 3.3 V linear feeding a 355 mA ESP32. **Deferred to Rev B**, where a
  12 V → 3.3 V converter fixes drain and heat together.
- **Schematic parity: 25 items, all benign** — 20 `unconnected-()` pins (J1 SBU1/SBU2,
  16 unused ESP32 GPIOs including UART0, D7's DOUT as chain end). The 5 footprints that
  were missing an `Assembly` field now have one.
- ERC has 4 suppressed checks in the `.kicad_pro`, including `single_global_label`.
- **Stock to watch**: ESP32-S3-WROOM-1U-N16 4667, AP63301WU-7 6114 (was 417 at original
  selection), PNLS6045-100M 5539. Fine for a batch of 5, thin for a production run.
- **16 BOM lines are on JLC's extended library**, each attracting a setup fee.

## EMC — the risk the 2-layer decision bought

Independently verified with kicad-happy (raw output in `analysis/`; invocation notes are
in the `kicad-happy-invocation` memory). Two findings confirmed by direct measurement:

- **F.Cu nets crossing a gap in the bottom GND pour: now 42 of 60** (`tools/emc.py`),
  worse than the 32 of 57 kicad-happy found before the refill. Longest unbacked run is now
  6.50 mm (DRAIN1), then USB_CC1 5.20, VIN_SENSE 4.80, UART1_TX/RX and USB_DP/DM 4.60.
  Dominant cause is still the VIN_PROT B.Cu strip — a 4.1 × 39 mm, 155 mm² slot at
  x 116.6-120.7 through the MCU/USB region.
- **Return vias: 15 of 43 within 1.0 mm, 12 with nothing inside 2 mm** — and none of them
  do anything without an F.Cu GND pour. The earlier "33 have now been given one" was not
  measured; see the top of this file.

The strip exists because VIN_PROT has no F.Cu path from the input section to the LED
connectors: J1's through-hole shield pads block the left edge y 107.18-116.82, and the
right edge pinches to 1.9 mm (≈3.8 A) at the H2/H3 mounting holes. **This is a floorplan
consequence, not a routing mistake** — 8.3 puts power in at the top and outputs at the
bottom with the MCU between, and on 2 layers the ~5 A V+ rail must cross that. Rev B
should either move J1 off the left edge or go 4-layer.

kicad-happy's `PS-002 "GND plane split: 57 islands"` is **wrong** — KiCad's own fill
emits the B.Cu GND pour as a single 7191.2 mm² polygon (`tools/emc.py` section 1), so no
flood fill is needed to say so. Its companion claim about signals crossing plane gaps is
correct and has got worse; do not discard the finding because the island count is bad.
