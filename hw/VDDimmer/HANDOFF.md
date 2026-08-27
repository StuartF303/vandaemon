# VANDIMMER-4CH+2A — session handoff

State as of commit `1a1d10b`. Phase 3 (layout) — everything routable is routed.

## FIRST THING AFTER A REBOOT

The zone fills are **stale**: 33 GND vias, 4 re-drilled vias and several traces were
added after the last fill. With KiCad closed you can refill without any GUI step:

```bash
bash hw/VDDimmer/tools/drc.sh          # runs kicad-cli with --refill-zones --save-board
```

Then re-measure — the numbers in the 8.5 table below were taken **before** those vias
went in and must be re-taken:

```bash
"C:/Program Files/KiCad/10.0/bin/python.exe" hw/VDDimmer/tools/measure.py
```

## Verified state (at last full measurement, commit `ba33114`)

| Check | Value |
|---|---|
| Schematic | 5 sheets, 96 parts, **ERC 0/0** |
| DRC | **0 errors** |
| Phantom pads | **0** |
| Unconnected | 20 → expect ~18 after refill |
| Schematic parity | 25, all benign (see below) |
| Tracks / vias / zones | 319 / 188 / 7 |

### 8.5 gate — measured, but re-take after the refill

| Criterion | Gate | Measured |
|---|---|---|
| Cin hot loop | < 15 mm² | **2.51 mm²** |
| Vias in hot loop | 0 | **0** (nearest 0.39 mm outside; a shunt to plane, not in series) |
| SW → nearest non-buck net | ≥ 3 mm | **12.75 mm** (U3.2 `+3V3`) |
| B.Cu GND under buck (fp +5 mm) | unbroken | **100.0%** of 6400 points; 0 foreign tracks/vias |
| GND pour | continuous | **1 connected region, 7207 mm²** (0.2 mm raster flood fill) |
| Buck output +5V copper | 645 mm² | **≈686 mm²** |
| Q_REV / VIN_PROT copper | 645 mm² | **≈2426 mm²** |
| FB divider → L1 / SW | away | R21 **2.63 / 2.63 mm**, R22 5.39 / 5.00, C27 4.76 / 4.76 |
| Bottom layer = pour + stitching | — | **fails by design**, see EMC below |

The 2.51 mm² hot loop is **not** comparable to the 4.60 mm² recorded in an early session —
different method. 2.51 is the reproducible figure from `tools/measure.py` (pad-centre
quadrilateral U2.3, U2.4, C23.2, C23.1).

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

**`route.py` has one known blind spot**: it validates track polylines but does **not**
synthesise a via at each layer transition, so a via can be too close to a neighbouring
trace and still pass. That bit me once (a DM via 0.075 mm from a DP run). Sweep proposed
vias explicitly against pads/tracks/vias — the pattern is in this session's history.

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

1. **Refill zones** (see top) and re-run `measure.py`.
2. **Unroutable without placement changes** — all confirmed, not guesses:
   - `BTN1` → J11.1
   - `VIN_PROT` → J9.1 / J10.1 — **no 12 V option for the addressable strips**
   - `+5V` → D9.1 — **no USB-powered operation**
   - `VBUS` → C44.1, and J1's north VBUS pad (A4/B9)
   All are walled in by the VIN_PROT bottom-layer strip, the gate verticals, J14's pin
   row and CC1's lane. **Product decision needed**: with D9.1 unreachable, VBUS feeds
   nothing but D8's clamp — the board is 12 V-input-only and USB is data-and-ESD only.
3. **Six layer transitions still have no GND return via** (no room): EN_MCU ×3,
   VBUS ×2, USB_DM ×1.
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

- **32 of 57 routed F.Cu nets crossed a gap in the bottom GND pour.** Longest unbacked run
  4.64 mm (UART1_RX); USB_DM 4.62, USB_DP 4.61. Dominant cause is the VIN_PROT B.Cu strip
  — a 4.1 × 39 mm, 155 mm² slot at x 116.6-120.7 running through the MCU/USB region.
  **Re-measure after the refill**; the USB re-route changed which nets cross.
- **39 of 39 layer transitions had no GND return via within 1.0 mm** (nearest 2.47 mm,
  worst 8.43 mm). **33 have now been given one**; six had no room.

The strip exists because VIN_PROT has no F.Cu path from the input section to the LED
connectors: J1's through-hole shield pads block the left edge y 107.18-116.82, and the
right edge pinches to 1.9 mm (≈3.8 A) at the H2/H3 mounting holes. **This is a floorplan
consequence, not a routing mistake** — 8.3 puts power in at the top and outputs at the
bottom with the MCU between, and on 2 layers the ~5 A V+ rail must cross that. Rev B
should either move J1 off the left edge or go 4-layer.

kicad-happy's `PS-002 "GND plane split: 57 islands"` is **wrong** — a 0.2 mm raster flood
fill proves the pour is one connected region. Its companion claim about signals crossing
plane gaps is correct; do not discard the finding because the island count is bad.
