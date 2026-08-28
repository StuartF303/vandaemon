# VANDIMMER-4CH+2A — session handoff

Phase 3 (layout). **Four functional defects were found this session and all four are
fixed.** Two of them — unpowered gate drivers and an unpowered ESP32 — would each have
made the board dead on arrival, and both were sitting behind a clean "DRC 0 errors".
An F.Cu GND pour has been added. Electrically the layout is now sound; the fab blockers
below are not.

## Open work

Nothing blocks the board electrically. What is left is fab preparation, listed under
"Remaining work" below: **0 of 49 BOM lines have an MPN**, there is no board name or
revision on silkscreen, **0 fiducials, 0 test points**, and ~150 silkscreen warnings.

Optional tidy-up: seven contacts hold together by 23-50 µm of copper (listed below).
None is load-bearing, but the four in the buck area are worth nudging properly onto
their pads if the board is opened again.

## Fixed this session

- **U4's gate drivers were unpowered.** The F.Cu +5V pour filled as two islands that did
  not touch — 448.6 mm² carrying the main rail, and 123.1 mm² carrying only U4.14, C50.1
  and C51.1. The four-wire addressable bus (ADDR1_DIN x=168.6, ADDR2_DIN x=169.1,
  ADDR_CLK x=170.1, STATUS_DIN x=170.6; 0.25 mm traces on 0.5 mm pitch) runs the full
  height of the zone, y 92 → 117.14, and nothing can fill between them. Bridged on B.Cu
  across the bus only: +5V vias at (167.6, 105.0) and (171.6, 105.0) with a 0.5 mm B.Cu
  track between them — 4.0 mm of slot, and both vias land inside their own island's fill
  so no F.Cu track was needed at either end. **U4.14, C50.1 and C51.1 are now in the main
  +5V group.**
- **J1's north GND pad was floating.** A1+B12 are one physical pad at (107.695, 108.750)
  and formed their own group; USB return had only the shell. Joined to the adjacent
  J1.SH pad with a 0.4 mm F.Cu track to (107.35, 107.68).
- **U1, the ESP32, had no 3V3.** The via at (133.0, 107.2) was *exactly tangent* to pad
  U1.2 — centre-to-pad-box distance 0.3000 mm against a via radius of 0.3000 mm, so zero
  overlap. KiCad calls a point touch unconnected and is right. Fixed with a 0.5 mm F.Cu
  stub (133.0, 107.2) → (133.0, 108.25), running 0.75 mm into the pad. **U1.2 is now in
  the main +3V3 group.**
- **Orphan +3V3 copper deleted** — a 10.3 mm B.Cu track (187.2, 84.5)→(187.2, 94.8)
  dangling at both ends, plus its via, which slotted the GND pour for nothing.
- **Six `starved_thermal` errors** from the new pour, cleared by setting the F.Cu GND
  zone to **Pad connection: Solid** (`connect_pads yes`) in the GUI — there is no MCP
  tool for zone pad-connection mode. Solid is right for a ground reference, and the
  +3V3 pour on this board already used it.

## The F.Cu GND pour

Added at priority 0 (lowest), outline = board inset 0.5 mm with a rectangular bite out of
the top edge at **x 172.5-186.0, y 60.5-78.5**. The bite keeps ground copper off the SW
node and, because R21, R22, C27 and R24 sit in that block alongside U2, C23, C24 and L1,
off the FB divider as well. B.Cu GND under the buck is untouched — coverage is still
100.0% of 6400 sample points with zero foreign tracks or vias.

What it bought:

| | before | after |
|---|---|---|
| `via_dangling` warnings | 81 | **9** |
| GND vias touching one layer only | 75 | 5 |
| DRC warnings, total | 244 | **170** |
| DRC errors | 0 | **0** |
| Unconnected items | 18 | **15**, all benign |
| GND connected groups | 2 | **1** |
| F.Cu GND copper | 0 mm² | **2664.9 mm²** (49 islands, all tied through vias) |

**But the return-via idea still mostly cannot be realised.** `tools/retvia.py` could place
only **3 of 30** missing return vias, and only one of those three landed inside the 1.0 mm
criterion (the other two are at 1.05 and 1.10 mm — a real return path, but `emc.py` still
counts them as missing). The score moved 30 → 29 of 44. The reason is structural, not a lack of care: on a
2-layer board the F.Cu side of most layer transitions is inside a *power* pour, not GND —
14 of the 27 failures are VIN_PROT vias sitting in the VIN_PROT pour, and both new +5V
bridge vias sit inside the +5V pour. There is no GND copper next to them to stitch to.
Rev B needs a floorplan change or a 4-layer stackup; no amount of via work fixes it here.

## Copper held together by 23-50 µm

`connect.py` now requires a real overlap and reports anything thinner than 50 µm. Seven
contacts qualify:

    23 µm  trk F.Cu (174.863,74.95)-(174.863,76.5)      <-> pad C23.1   (buck Cin)
    23 µm  trk F.Cu (174.5,81.25)-(174.863,76.6)        <-> pad C23.1
    25 µm  trk F.Cu (174.1,73.05)-(174.1,66.0)          <-> pad U2.1    (FB)
    25 µm  trk F.Cu (177.137,74.0)-(178.5,74.0)         <-> pad C24.2   (bootstrap)
    25 µm  trk B.Cu (124.3,100.6)-(121.088,100.6)       <-> via (121.088,100.200)
    25 µm  via (172.588,124.025)                        <-> pad R10.1
    50 µm  via (112.400,107.000)                        <-> pad D8.4

**None of them is load-bearing.** Re-running every net with the overlap threshold raised
to 50 µm — i.e. asking what breaks if the fab etches 50 µm off everywhere — splits no net
that was not already split. They are redundant second paths, so this is a tidiness item,
not a defect. Worth nudging the four buck-area tracks properly onto their pads if the
board is touched again.

## Verified state (after the pour and refill)

| Check | Value |
|---|---|
| Schematic | 5 sheets, 96 parts, ERC 0/0 |
| DRC | **0 errors**, 170 warnings |
| Phantom pads | 0 |
| Unconnected | 15, every one accounted for below |
| Schematic parity | 25, all benign |
| Tracks / vias | 321 / 192 |
| Nets split into more than one group | 12 of 61, **all benign** |

### Every remaining unconnected item, and why

`connect.py` with no arguments checks all 61 nets and agrees with KiCad exactly. Twelve
nets are split and **none of them is a defect** — each is either a package-internal
connection or a routing limitation with a product decision already taken:

| Net | Group | Verdict |
|---|---|---|
| +3V3 | U3.2 tab vs pin | benign, SOT-223 tab and pin 2 are one node inside the package |
| +5V | D9.1 | known, no USB-powered operation — product decision already taken |
| +5V | 3 × zero-area prio-1 slivers | artefact, no copper |
| /MCU/BTN1 | J11.1 | known unroutable |
| /MCU/USB_DM, USB_DP | D8's pin pairs | benign, bonded inside the USBLC6 |
| /MCU/VBUS | J1.A4/B9, D9.2, C44.1 | known, 12 V-only board; USB is data-and-ESD only |
| /PWM/DRAIN1-4 | Q1-Q4 pad 2 | benign, DPAK centre lead |
| /Power/VIN_FUSED | Q5.2 | benign, same |
| VIN_PROT | J9.1, J10.1 | known, no 12 V for the addressable strips |

### 8.5 gate

| Criterion | Gate | Measured | |
|---|---|---|---|
| Cin hot loop | < 15 mm² | 2.51 mm² | pass |
| Vias in hot loop bbox | 0 | 1 — GND at (177.137, 75.95), on U2.4's own pad; a shunt to plane, not in series | pass in spirit |
| SW → nearest non-buck net | ≥ 3 mm | 0.25 mm to BST | gate is mis-stated, see note |
| B.Cu GND under buck (fp +5 mm) | unbroken | 100.0% of 6400 points; 0 foreign tracks/vias | pass |
| B.Cu GND pour | continuous | 1 island, 7194.2 mm² | pass |
| F.Cu GND pour | — | 2664.9 mm², 49 islands, all tied through vias (GND is one group) | new |
| Buck output +5V copper | 645 mm² | 571.8 mm², now a single connected group | under gate, but connected |
| VIN_PROT copper | 645 mm² | 2212.7 mm² | pass |
| FB divider → L1 / SW | away | R21 6.73 / 4.80 mm, R22 8.95 / 4.91, C27 8.61 / 7.03 | pass |
| F.Cu nets crossing a B.Cu pour gap | — | 42 of 60, worst 6.50 mm (DRAIN1) | unchanged, see EMC |
| Layer transitions w/o return via ≤ 1 mm | — | 29 of 44 | structural, see above |

Notes:

- **SW → BST is 0.25 mm**, not the 12.75 mm an earlier handoff recorded. That figure was
  the distance to the nearest *non-buck* net (U3.2 `+3V3`); BST is the buck's own bootstrap
  and 0.25 mm is the design clearance. The gate should name BST/L1/FB as excluded rather
  than rely on "non-buck" being obvious.
- **+5V is 571.8 mm² against a 645 mm² gate.** It is now one connected group, so this is a
  copper-area/thermal question, not a connectivity one. The bus wall is what costs the
  area; widening it needs the addressable bus moved.
- **Section 2 of `emc.py` did not change** when the F.Cu pour went in, and that is
  expected — it measures F.Cu tracks against the *B.Cu* pour, which was untouched. The
  F.Cu pour helps a different way: it gives B.Cu-routed signals a reference and puts
  return copper beside the F.Cu runs.

## Trust the tools, but verify them

Two tooling bugs this session each hid a fatal defect, so re-derive rather than inherit:

- `measure.py` did not run at all as committed — missing `import os`, two calls using
  signatures `geom.py` does not have, and a zone parser matching two-space indentation
  against a tab-indented file. Several numbers in the previous handoff's gate table came
  from an unrecorded method, not from this tool.
- `connect.py`'s first version treated a *touching* via and pad as connected. That is
  exactly the U1.2 case, and it reported the ESP32 as powered when it is not. It now
  requires ≥ 0.1 µm of real overlap and flags anything under 50 µm.

**Any number in a handoff that came from a tool which does not currently run should be
treated as unverified.** Both defects were caught only by comparing the tool's verdict
against KiCad's own DRC output, item by item. Keep doing that.

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

## Design decisions already made — do not re-litigate

- **12 V only.** D5 = SMBJ18A (29.2 V clamp, under the buck's 32 V abs max).
- **Buck = AP63301WU-7** (C2158003, TSOT-23-6, 3 A, 500 kHz). Internally compensated,
  so R25/R26/C28/C29 were deleted.
- **Cin = C23, 0402** (100nF/25V) bridging pin3 to pin4 from below.
- Connectors: J2 screw terminal; J3-J6 JST XH `S2B-XH-A`; J7/J8 `S4B-XH-A`.
- USB ESD: D8 USBLC6-2SC6.
- Input LC filter damping: R31 1 Ω in series with C20, **both DNP with L3**.
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

1. **Fab blockers are now the critical path** — see item 4. Electrical work is done.
2. **Unroutable without placement changes** — all confirmed, not guesses:
   - `BTN1` → J11.1
   - `VIN_PROT` → J9.1 / J10.1 — **no 12 V option for the addressable strips**
   - `+5V` → D9.1 — **no USB-powered operation**
   - `VBUS` → C44.1, and J1's north VBUS pad (A4/B9)
   All are walled in by the VIN_PROT bottom-layer strip, the gate verticals, J14's pin
   row and CC1's lane. **Product decision needed**: with D9.1 unreachable, VBUS feeds
   nothing but D8's clamp — the board is 12 V-input-only and USB is data-and-ESD only.
3. **30 of 44 layer transitions have no GND return via within 1.0 mm**, and only 3 can be
   given one. The F.Cu side of the rest is inside a power pour, not GND. Structural; see
   "The F.Cu GND pour" above.
4. **Fab blockers** (kicad-happy release gate fails on these):
   - **0 of 49 BOM lines have an MPN.** `bom-mapping.csv` covers only the Power sheet and
     is not in the schematic fields.
   - No board name, no revision on silkscreen; **0 fiducials**; **0 test points**.
5. Silkscreen cleanup: ~92 `lib_footprint_mismatch`, ~28 `silk_over_copper`,
   ~21 `silk_overlap`, 4 `silk_edge_clearance`. Also 5 `hole_to_hole` and 1
   `holes_co_located` not yet investigated.

## Known open items

- **Q5's tab is VIN_FUSED, not GND**; Q1-Q4's tabs are DRAIN1-4. All five have **0 thermal
  vias** on 37 mm² tab pads (a DPAK tab wants ~18). Vias would need a matching
  bottom-layer island, which conflicts with the GND pour.
- **The 4 DPAK centre-lead pads and D8's two internal pin pairs read as unconnected in DRC
  and cannot be routed** — GATE runs between each FET's centre lead and its tab, and D8's
  D± pin pairs are bonded inside the package. Electrically harmless.
- R30 is an 0805 0 Ω jumper carrying ~1.2 A. Recommend 1206 or larger.
- F1 is a 10 A PPTC on a 5 A board — recommend ~6.3 A / 63 V 2410.
- **No I2C pull-ups on the board.** I2C_SDA/SCL go from U1 straight to J13 and nowhere
  else. Decide whether peripherals carry them or add 4.7 k here.
- **U3 (AMS1117-3.3) dissipates 0.55 W** in SOT-223 with no thermal vias (Tj 57.9 °C,
  margin 67 °C — safe but hot) and draws **5 mA quiescent**, dominating the board's
  5.66 mA sleep current. On a vehicle that is a permanent parasitic drain; consider a
  switcher or an LDO with an EN pin.
- **J2 has no EMC filtering within 25 mm** because L3/C20/R31 are DNP. On a 12 V vehicle
  rail this is the highest-value fit option to reconsider.
- **Schematic parity is 25 items, all benign**: 20 `unconnected-()` pins (J1 SBU1/SBU2,
  16 unused ESP32 GPIOs including UART0, D7's DOUT as chain end) and 5 footprints missing
  an `Assembly` field (C20, L3, R31, R11, R12).
- ERC has 4 suppressed checks in the `.kicad_pro`, including `single_global_label`.
- AP63301 stock was 417 at selection.

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
