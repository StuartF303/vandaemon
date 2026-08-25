# VANDIMMER-4CH+2A — session handoff

State as of commit `3ba46d5`. Phase 3 (layout) — signals complete, pours placed,
power-rail distribution unfinished.

## Verified state

| Check | Value |
|---|---|
| Schematic | 5 sheets, 96 parts, **ERC 0/0** |
| DRC | **1 error** — `zones_intersect`, two `+5V` F.Cu zones at equal priority |
| Phantom pads | **0** |
| Unconnected | **56** (was 197) |
| Tracks | 261 segments, 1504 mm |
| Vias | 133 |
| Zones | 7 |

### 8.5 gate — all measured from the saved file after `--refill-zones`

| Criterion | Gate | Measured |
|---|---|---|
| Cin hot loop | < 15 mm² | **2.51 mm²** |
| Vias in hot loop | 0 | **0** (nearest 0.39 mm outside; a shunt to plane, not in series) |
| SW to nearest non-buck net | ≥ 3 mm | **12.75 mm** (U3.2 `+3V3`) |
| B.Cu GND under buck (fp +5 mm) | unbroken | **100.0%** of 6400 points; 0 foreign tracks, 0 foreign vias |
| GND pour | continuous | **7367 mm², 1 connected region** (0.2 mm raster flood fill) |
| Buck output +5V copper | 645 mm² | **686 mm²** |
| Q_REV / VIN_PROT copper | 645 mm² | **2437 mm²** |
| FB divider to L1 / SW | away | R21 **2.63 / 2.63 mm**, R22 5.39 / 5.00, C27 4.76 / 4.76 |
| Bottom layer = pour + stitching | — | **fails**: 9 jumper segments, 26.4 mm; see below |

**The 2.51 mm² hot loop is not comparable to the 4.60 mm² recorded earlier** — that used
a different method. 2.51 is the reproducible figure from `scratchpad/measure.py`
(pad-centre quadrilateral U2.3, U2.4, C23.2, C23.1).

## Konnect gotchas — these cost hours, do not rediscover them

1. **`update_pcb_from_schematic` injects phantom pads** on every footprint it *adds*:
   unnumbered `thru_hole` pads with an empty `(layers)` list. Only fix is KiCad's
   right-click, Update Footprint (targeted, per part — the global
   Tools, Update Footprints from Library re-anchors and scrambles placement).
2. **`place_component`** creates a footprint with no schematic identity, so the sync
   refuses it and it can never receive nets. Not a workaround for anything needing nets.
3. **`get_component_pads` / `query_traces` read the saved file.** `save_project` first.
4. **IPC needs `kicad.exe` AND the PCB editor open inside it.** The project manager alone
   is not enough — `check_kicad_ui` reports `ipc_responsive: true` and vias still fail
   with `GetOpenDocuments AS_UNHANDLED`. The PCB editor must be opened by hand.
5. **`add_zone` / `add_copper_pour` are file inserts and refuse while KiCad holds the
   board.** Zones need the editor *closed*, vias need it *open* — they cannot be done in
   one pass. Plan the order.
6. **There is no tool to delete or re-prioritise a zone.** Get the polygon right first
   time, or it needs a GUI fix.
7. **`kicad-cli pcb drc --refill-zones --save-board` fills zones** — no GUI step needed
   to keep fills current. Already wired into `scratchpad/drc.sh`.
8. **Verify every move and route by reading it back.** A placement/trace coordinate
   desync went undetected for a whole routing pass once.
9. Pin-header footprints anchor at **pin 1, not centre**.

## Design decisions already made — do not re-litigate

- **12 V only.** D5 = SMBJ18A (29.2 V clamp, under the buck's 32 V abs max).
- **Buck = AP63301WU-7** (C2158003, TSOT-23-6, 3 A, 500 kHz, spread spectrum).
  Internally compensated, so R25/R26/C28/C29 were deleted.
- **Cin = C23, 0402** (100nF/25V) bridging pin3 to pin4 from below.
- Connectors: J2 screw terminal; J3-J6 JST XH `S2B-XH-A`; J7/J8 `S4B-XH-A`.
- USB ESD: D8 USBLC6-2SC6.
- Input LC filter damping: R31 1 Ω in series with C20, **both DNP with L3**.
- M3 mounting holes 4.5 mm in from each corner.
- 8.4's "USB D± 90 Ω differential" ignored — unachievable on 2-layer 1.6 mm and
  unnecessary at 12 Mbit full-speed.

### Pin reassignments made during layout — firmware-visible, no BOM change

Three reshuffles, each because measured geometry made the original unroutable: U1's GPIO
map was changed twice, and U5's buffer channels 3 and 4 were swapped (both OEs grounded,
so functionally identical). ERC still 0/0. **Firmware must be regenerated from the current
schematic, not from the original pin plan.**

## Remaining work

1. **`zones_intersect`** — two `+5V` F.Cu zones overlap at equal priority. Fix in
   Tools, Zone Manager: set one to priority 1, or delete the smaller
   (x 174.5-198, y 96.5-117; it currently fills to 0 mm²).
2. **26 GND pad-to-via stubs** — generated and clearance-checked, ready in
   `scratchpad/gndstub.json`, validated by `scratchpad/b8.py`. Needs IPC.
3. **VIN_PROT top-to-bottom link vias** — 6 at y ≈ 90.4/91.5 and 4 at y ≈ 126.6/127.6,
   x 117.2-119.8, into the B.Cu strip. Needs IPC.
4. **+3V3 distribution** — 12 pads. F.Cu is saturated; plan is a 0.5 mm B.Cu spine at
   y ≈ 84.5 from U3 west to x ≈ 124, with vias down to the C40-C43 / R43 / R44 / R40
   cluster, J13.1, J14.1, R29.1, U1.2. Do **not** use a zone for the spine — a 1.4 mm bar
   cuts the GND return across the whole top band.
5. **BTN1/BTN2 to J11/J12** — B.Cu runs at x 113.1 and x 115.0 are clear and validated.
6. **USB VBUS branch** — J1 VBUS to C44.1 to D9.2, and D9.1 to +5V. **D9.1 is currently
   unreachable**; until it is, the board cannot be powered from USB.
7. **17 GND return vias**, one beside each layer-transition via (see EMC below).
8. **10 vias at 0.45/0.25** give a 0.100 mm annular ring, below IPC Class 2's 0.125 mm.
   Re-drill to 0.20 (0.45/0.20 = 0.125, still meets the board's own min_via_drill).
9. Silkscreen: **no board name, no revision, no fiducials, no test points**.

## Known open items

- **Q5's tab is VIN_FUSED, not GND.** Same problem on Q1-Q4, whose tabs are DRAIN1-4. All
  five have **0 thermal vias** on 37 mm² tab pads; a DPAK tab wants about 18. Any vias
  need a matching bottom-layer island, which conflicts with the GND pour.
- **The 4 DPAK centre-lead pads and D8's two internal pin pairs read as unconnected in DRC
  and cannot be routed** — GATE runs between each FET's centre lead and its tab, and D8's
  D+/D− pin pairs are bonded inside the package. Electrically harmless.
- R30 is an 0805 0 Ω jumper carrying about 1.2 A. Recommend 1206 or larger.
- F1 is a 10 A PPTC on a 5 A board — recommend about 6.3 A / 63 V 2410.
- **No MPNs anywhere.** `bom-mapping.csv` covers only the Power sheet and is not in the
  schematic fields; kicad-happy reports 0 of 49 lines with an MPN. Fab blocker.
- **No I2C pull-ups on the board.** I2C_SDA/SCL go from U1 straight to J13 and nowhere
  else. Decide whether peripherals carry them or add 4.7 k here.
- **U3 (AMS1117-3.3) dissipates 0.55 W** in SOT-223 with no thermal vias (Tj 57.9 °C,
  margin 67 °C — safe but hot) and has **5 mA quiescent**, which dominates the board's
  5.66 mA sleep current. On a vehicle that is a permanent parasitic drain; consider a
  switcher or an LDO with an EN pin.
- **J2 has no EMC filtering within 25 mm** because L3/C20/R31 are DNP. On a 12 V vehicle
  rail this is the highest-value fit option to reconsider.
- ERC has 4 suppressed checks in the `.kicad_pro`, including `single_global_label`.
- AP63301 stock was 417 at selection.

## EMC — the risk the 2-layer decision bought

Independently verified with kicad-happy (raw output in `analysis/`). Two findings
confirmed by direct measurement:

- **32 of 57 routed F.Cu nets cross a gap in the bottom GND pour.** Longest unbacked run
  4.64 mm (UART1_RX); USB_DM 4.62 mm, USB_DP 4.61 mm. The dominant cause is the VIN_PROT
  B.Cu strip — a 4.1 × 39 mm, 155 mm² slot at x 116.6-120.7 running straight through the
  MCU and USB region.
- **17 of 17 layer transitions have no GND return via within 1.0 mm.** Nearest is 2.47 mm,
  worst 8.43 mm. Cheap to fix (item 7 above) and worth doing.

The strip exists because VIN_PROT has no F.Cu path from the input section to the LED
connectors: J1's through-hole shield pads block the left edge from y 107.18 to 116.82, and
the right edge pinches to 1.9 mm (about 3.8 A) at the H2/H3 mounting holes. **This is a
floorplan consequence, not a routing mistake** — 8.3 puts power in at the top and outputs
at the bottom with the MCU between them, and on 2 layers the roughly 5 A V+ rail must
cross that region. Rev B should either move J1 off the left edge or go 4-layer.

kicad-happy's `PS-002 "GND plane split: 57 islands"` is **wrong** — a 0.2 mm raster
flood-fill proves the pour is 1 connected region. Its companion claim about signals
crossing plane gaps is correct; do not discard the finding because the island count is bad.
