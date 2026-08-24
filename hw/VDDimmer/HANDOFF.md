# VANDIMMER-4CH+2A — session handoff

State as of commit `12da60b`. Phase 3 (layout) in progress.

## Verified state

| Check | Value |
|---|---|
| Schematic | 5 sheets, 96 parts, **ERC 0/0** |
| Footprints placed | 96/96, all match `LAYOUT-STATE.md` exactly |
| DRC | **0 errors** |
| Phantom pads | **0** |
| Hot loop (Cin+ → VIN → GND → Cin−) | **4.60 mm²** (target <15, 69% margin), 0 vias |
| Buck ↔ WROOM | 27.1 mm (target ≥20) |
| Routed | 42 segments: VIN_RAW, VIN_FUSED, QREV_G, VIN_BUCK, VIN_DAMP, BST, SW, buck GND |
| Unconnected | 197 items |

## Konnect gotchas — these cost hours, do not rediscover them

1. **`update_pcb_from_schematic` injects phantom pads** on every footprint it *adds*:
   unnumbered `thru_hole` pads with an empty `(layers)` list, at the footprint's
   staging position. They become spurious drills. Only fix is KiCad's
   **right-click → Update Footprint…** (targeted, per part — the global
   *Tools → Update Footprints from Library* re-anchors footprints by
   footprint-type-specific offsets and scrambles placement).
2. **`place_component` has no such bug** but creates a footprint with no schematic
   identity, so the sync refuses it (`reference identity conflict`) and it can
   never receive nets. Not a workaround for anything needing connectivity.
3. **`get_component_pads` reads the saved file, not live state.** Always
   `save_project` before measuring.
4. **IPC only works with `kicad.exe` (project manager) AND the PCB editor open
   inside it.** Standalone `pcbnew.exe` never binds the API socket. Launch via
   `launch_kicad_ui`, then the PCB editor must be opened by hand once.
5. **Verify every move and route by reading it back.** Return values have been
   accurate, but a placement/trace coordinate desync went undetected for a whole
   routing pass once.
6. Pin-header footprints anchor at **pin 1, not centre** — courtyards extend
   asymmetrically downward.

## Design decisions already made (don't re-litigate)

- **12 V only.** D5 = SMBJ18A (29.2 V clamp, under the buck's 32 V abs max).
- **Buck = AP63301WU-7** (C2158003, TSOT-23-6, 3 A, 500 kHz, spread spectrum).
  Replaced AP64350, whose SO-8EP pinout has no valid 2-layer routing.
  Internally compensated → R25/R26/C28/C29 deleted.
- **Cin = C23, 0402** (100nF/25V) bridging pin3→pin4 from below.
- Connectors: J2 screw terminal (VIN); J3–J6 JST XH `S2B-XH-A` 2P TH right-angle
  (C7432691, 3 A); J7/J8 `S4B-XH-A` 4P (C7429643). Lights use JST SM wire-to-wire,
  which has no PCB-mount form, so an adapter pigtail is expected either way.
- USB ESD: D8 USBLC6-2SC6 added (was absent entirely).
- Input LC filter damping: R31 1Ω in series with C20, both DNP with L3.
- M3 mounting holes 4.5 mm in from each corner (not the spec's 3.5).
- §8.4's "USB D± 90 Ω differential" is unachievable on 2-layer 1.6 mm and
  unnecessary at 12 Mbit full-speed. D± are in a 0.25 mm `USB` netclass, routed
  short and matched, impedance target ignored.

## Remaining work

1. **Route FB (U2 pin1) and EN (U2 pin2)** — both on the free left edge now.
   FB divider R21/R22/C27 currently sits right of the buck; §8.5.4 wants FB away
   from SW and the inductor, so consider moving them left of U2.
2. +5 V and +3V3 rails (U3 LDO, C30–C32, U4/U5, D7, J9/J10).
3. Channel drains DRAIN1–4 (1.0 mm), gate drive GATE_IN/GDRV via U4.
4. Addressable section: U5, PF1/PF2, R9–R13, J7/J8.
5. Signals: USB D± pair, I2C, UART, buttons, BOOT/EN.
6. **Pours last**: GND on B.Cu (solid, keepout under buck per §8.5.3),
   VIN_PROT and +5V on F.Cu. `refill_zones` after every copper change.
7. Stitching vias every 10 mm around the perimeter; **0.4 mm thermal via ring
   around U2's GND pad** (barrel area scales with diameter — a ring of 0.4 mm
   vias beats enlarging small ones).
8. Re-measure and report: hot loop after pours, switch-node clearance to real
   signals (≥3 mm), buck/Q_REV pour areas vs 645 mm², bottom pour continuity.

## Known open items

- **Q5 tab is VIN_FUSED, not GND**, so §8.4's "thermal vias under the P-FET tab"
  would need a VIN_FUSED island in the bottom GND pour. Currently planned as
  top-side copper only — flag in the final report.
- R30 is an 0805 0 Ω jumper carrying the full buck input current (~1.2 A);
  0805 jumpers are rated 1–2 A. Recommend 1206 or larger.
- F1 is a 10 A PPTC on a 5 A board — recommend ~6.3 A/63 V 2410 fuse.
- `bom-mapping.csv` only covers the Power sheet. MCU/PWM/Addressable have no
  LCSC mapping; needed before phase 4.
- ERC has 4 suppressed checks in the `.kicad_pro`, including
  `single_global_label`. Re-run with it enabled before sign-off.
- AP63301 stock was 417 at time of selection — fine for prototypes, thin for
  volume. Siblings AP63356/57 (VDFN-13) have ~2.5k.
- kicad-happy is installed at `C:\Projects\kicad-happy` (run with KiCad's
  Python 3.11, system Python is 3.6). Its schematic pass flagged 8 false-positive
  "5 V into 3.3 V" errors — those are ESP32 3.3 V *outputs* driving 74HCT125
  *inputs*, which is why the family is HCT. Independent verification with its
  `emc` and `kicad` skills is still owed, ideally run in a subagent that isn't
  told the expected numbers.
