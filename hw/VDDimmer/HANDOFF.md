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
bash hw/VDDimmer/tools/export-cpl.sh                           # CPL + JLC BOM
```

## Current state

| | |
|---|---|
| Schematic | 5 sheets, **98 parts**, **ERC 0 violations** |
| DRC | **0 errors**, 166 warnings — but see the severity warning below |
| Unconnected | 15, every one benign — see the table below |
| Nets split into >1 group | 12 of 61, all benign |
| GND | **one connected group**; B.Cu pour 1 island, 7179.8 mm² |
| BOM | **51 lines, 98 parts** |
| Buck gate (§8.5) | hot loop **2.51 mm²** (< 15), B.Cu GND **1 island, 7179.8 mm²** |
| Assembly data | **CPL 77 parts / JLC BOM 34 lines**, reconciled — `tools/export-cpl.sh` |
| Schematic parity | **0** — cleared entirely by KiCad's own F8 sync |
| Decisions | `DECISIONS-2026-08-29.md` — settled, do not re-litigate |

## "DRC 0 errors" is still not what it looks like

`hole_to_hole` and `holes_co_located` are **downgraded to `warning`** in `.kicad_pro`,
along with 27 other checks. That is why the board reports zero errors while carrying six
real drill defects, two of whose holes physically overlap. This is the same shape of trap
as the split-net lesson below — read the warning breakdown, not the error count:

The six drill defects are now fixed, but the severity override is still in the project
file, so the same trap will hide the next one. Current breakdown:

```
92 lib_footprint_mismatch   30 silk_over_copper   26 silk_overlap   9 via_dangling
 7 track_dangling            4 silk_edge_clearance   0 hole_to_hole   0 holes_co_located
```

Consider putting `hole_to_hole` and `holes_co_located` back to `error` in
Board Setup → Violation Severity. Nothing legitimate on this board relies on them
being warnings.

## Open work, in order

### 1. ~~GUI-only items~~ — ALL DONE

`(dnp yes)` is set on R11/R12 and reaches the BOM. U4/U5's fields are healed. The two
mis-placed board texts were deleted and re-added at (166.55, 98.00) and (166.55, 100.60).

**Two things went wrong here that are worth not repeating:**

- **Opening a multi-unit symbol's properties on the wrong unit wipes its fields.** The
  dialog shows only the *clicked unit's* fields and writes them to all units on OK. U5 was
  clicked on unit 1 and propagated correctly; U4 was clicked on another unit, so the empty
  field set propagated and **deleted its MPN, LCSC and Manufacturer**. The BOM did not
  notice, because U4 and U5 collapse into one Value+Footprint line and U5 still carried
  the values. Restored. Always click unit 1, and diff the BOM afterwards.
- **A DNP part collapsing into its fitted siblings' BOM line.** R11/R12 share `47R` with
  R9/R10/R13, so grouping by Value+Footprint produced one line of five reading "DNP" —
  telling the assembler not to fit any of them. `export-bom.sh` now groups by
  `Value,Footprint,${DNP}`.

What remains here is cosmetic only — 4 silkscreen warnings, see §6.

### 2. ~~The two I2C pull-ups~~ — DONE, schematic and PCB

R45 and R46 (4.7 k, 0805, `C17673`, `0805W8F4701T5E`, UNI-ROYAL) are fitted, routed and
verified on both sides.

**Schematic**: `MCU.kicad_sch` at (283.21, 99.06) and (289.56, 99.06), tied with
`connect_to_net`. Verified against the exported netlist, not the tool's return value:

```
+3V3            15 nodes  ... J13.1 R43.1 R44.1 R45.1 R46.1 U1.2 U3.2
/MCU/I2C_SDA     3 nodes  J13.3  R45.2  U1.20
/MCU/I2C_SCL     3 nodes  J13.4  R46.2  U1.18
```

That +3V3 check mattered: the pins were tied with a plain `+3V3` **net label**, because
that is what J13's own supply pin uses on this sheet. Had it not merged with the power
net the pull-ups would have pulled to nothing — the exact silent failure the decision
existed to prevent.

**PCB**: R45 at (161.0, 100.0) and R46 at (161.0, 102.5), both **rot 180**, in a pocket
east of the GATE_IN fan-out. 3 vias (0.6/0.3) at (160.0875, 98.8), (160.0875, 104.2) and
(161.9125, 98.8); 27.35 mm of new track, 20.75 mm of it B.Cu.

**Why B.Cu was unavoidable.** `ADDR1_DIN` runs the full width of the board on F.Cu at
**y = 96.33, x 149.35–168.60**. It walls +3V3 (which reaches J13.1 at y = 95) off from
the I2C lines (y = 100.14 and 102.67). There is no F.Cu-only path; going round its east
end costs ~30 mm and still needs via hops over the GATE_IN verticals. B.Cu east of J13 is
bare GND pour, and **J13's pads 1/3/4 are through-hole**, so they are reachable from below
with no via at that end — which is what makes the tap cheap.

The rotation matters: the parts arrive with pad 1 (+3V3) **west**. Left that way, the SDA
return would cross the +3V3 spine on B.Cu. At rot 180 the three B.Cu runs are strictly
parallel and nothing crosses.

**Cost to the GND pour, measured**: B.Cu pour 7194.2 → **7179.8 mm², still one island**.
14.4 mm² of slot, in the digital region, far from the buck. I²C at 100–400 kHz does not
care about its return path, so this was the right trade — but it is a real cut and it is
recorded here rather than buried.

Both checks ran *before* anything was pushed: `route.py` reported 0 violations over 9
segments, and the 3 vias were swept separately against every pad, track and via on both
layers, because `route.py` does not synthesise vias.

### 3. The fab blockers

- **~~No usable CPL~~ — SOLVED, needs no GUI pass.** The `attr` problem is real and
  unchanged: no footprint carries `(attr smd)`, so `--smd-only` returns 0 rows and the
  unfiltered export returns all 96 including the mounting holes. But the fix is not to
  set 96 footprint types by hand. `tools/export-cpl.sh` takes the unfiltered export and
  derives the exclusion set from `bom-lcsc.csv` — a part is machine-placed iff its BOM
  line names a JLC library tier. **CPL and BOM then agree by construction**: nothing can
  be quoted-and-not-placed or placed-and-not-quoted. **98 = 78 placed + 18 hand-fit /
  mechanical + 2 DNP**, all top side.
  The `attr` flag is still what generates the 92 `lib_footprint_mismatch` warnings —
  **do not clear those with Tools → Update Footprints from Library.**
- **~~Six drill defects~~ — FIXED.** All five `hole_to_hole` and the one
  `holes_co_located` are gone; `drc.sh` now reports **0 of each**. Five vias deleted via
  IPC, each proven non-load-bearing first by checking which track endpoints actually land
  on it. In every overlapping pair only *one* via carried a track; the other was a bare
  stitching via with its twin 0.1–0.5 mm away. `d85c5d26` was redundant by construction —
  1 mm from J2's **PTH** GND pad, which already ties both layers. `afb88017` was an exact
  duplicate. Afterwards GND is **still one connected group** and the split-net count is
  unchanged at 12; via count 192 → 187. Kept for the record:

  | Defect | Refs | Fix |
  |---|---|---|
  | 0.0000 mm — drills touch | GND vias `098213e7` (176.112, 63.5) and `dd170b03` (176.5, 63.5) | delete one, re-run `connect.py` |
  | 0.1049 mm | GND vias `70c8a0bb` (134.744, 126.566) and `9b71116c` (134.486, 127.0) | delete one |
  | 0.1049 mm | GND vias `8d1d0912` (146.044, 126.566) and `f488f812` (145.786, 127.0) | delete one |
  | 0.2000 mm vs 0.2495 min | J2 pad 2 (112.92, 69.0) vs GND via `d85c5d26` (113.92, 69.0) | nudge the via ~0.06 mm east |
  | co-located | **duplicate** EN_MCU vias `a14092cd` and `afb88017`, both at (150.6, 105.16) | delete either; they are identical |

  The four GND deletions are stitching vias, so confirm with `connect.py` that GND still
  reports one connected group afterwards — do not assume redundancy.
- **~~No board name or revision~~ — DONE.** `VANDIMMER-4CH+2A` (1.2 mm) and `Rev A`
  (1.0 mm) on F.SilkS at (166.55, 98.00) / (166.55, 100.60). Note the board is 100 × 80 mm
  and **its whole perimeter is connectors** — there is no clear edge space anywhere, which
  is why the board ID sits mid-board.
- **Still absent: 0 fiducials, 0 test points, and an empty title block** (no title,
  revision, company or date — that prints on the fab drawing, and no MCP tool sets it).
  Fiducials are arguably optional here: the finest *machine-placed* pitch is SOIC-14,
  because J1's 0.5 mm USB-C is hand-fit and excluded from the CPL.
- Silkscreen: 4 clipped by the board edge (H1/H2 refdes off the top, J1's outline off
  the left), 30 `silk_over_copper`, 26 `silk_overlap`.

### 6. Cosmetic silkscreen, the only thing left that I introduced

Four warnings, no functional effect — JLCPCB clips silkscreen off exposed pads. Fixing
them needs one more KiCad-open pass, and **the board text cannot be moved by any tool**
(gotcha 14), so it is a GUI delete-and-re-add:

- **R45's auto-placed reference field** at (161.00, 101.65) clips R46's outline and prints
  over both of R46's pads — 3 of the 4. Cleanest fix is to move R46 from y = 102.5 to
  about y = 104.0 and re-route its two taps, or just drag R45's refdes in the GUI.
- **`VANDIMMER-4CH+2A` clips J13's silkscreen outline.** `silkspace.py` estimates stroke
  text width as `len × size × 0.85`, which was slightly optimistic here. Nudging the text
  ~1 mm east clears it.

### 4. ~~Verify before ordering~~ — both defects RESOLVED

**(a) R30 shorted out the input LC filter. Fixed — R30 is now DNP.**

```
VIN_PROT ─┬─ L3 ─┬─ VIN_BUCK ─ R31 ─ VIN_DAMP ─ C20 ─ GND
         └─ R30 ─┘        (0 Ω bypass jumper — now DNP)
```

Confirmed from the netlist: L3 and R30 both bridged VIN_PROT to VIN_BUCK, so R30 was
the filter's bypass — exactly what spec §415 describes ("optional input LC filter as
unpopulated", i.e. filter out, jumper in). The 2026-08-29 decision populated L3/C20/R31
but left R30 fitted and asked for it "rated ≥ 2 A", treating it as a series jumper.
Both fitted meant **no filter at all**: a 0 Ω thick-film part is ~20-50 mΩ, so it
paralleled L3 at DC and shunted it at the switching frequency.

`(dnp yes)` is set, and `export-cpl.sh` now reports all three DNPs via KiCad's own
attribute. This also closed the old "find a 0 Ω jumper rated ≥ 2 A" item — an unfitted
part carries no current.

**(b) L1/L3's land was investigated, changed, and CHANGED BACK. The original geometry
was right.** Read this before touching it again.

The footprint `VANDIMMER:L_cjiang_FXL0630_7.0x6.6mm` is **misnamed**: it is a 6045 land,
not an FXL0630 7.0 × 6.6. That wrong metadata caused a wrong conclusion — the land was
swapped to KiCad's curated `Inductor_SMD:L_Changjiang_FXL0630` on the reasoning that three
independent curated 6×6 lands agreed with each other and disagreed with it.

**That was wrong, and it was caught by reading the actual part's datasheet.** DMBJ
PNLS6045 (datasheet p3, dimension table): body A=6.0, B=6.0, C(max)=4.50, and the terminals
are **D=5.0 × E=1.65 mm**, so each terminal spans **1.35-3.00 mm** from the centreline.

| Land | Pad span | Contact | Area | % of terminal |
|---|---|---|---|---|
| **`L_cjiang_FXL0630_7.0x6.6mm`** (fitted) | 1.20-3.70 | 1.65 × 5.00 | 8.25 mm² | **100 %** |
| `Inductor_SMD:L_Changjiang_FXL0630` | 1.85-4.20 | 1.15 × 3.50 | 4.03 mm² | **49 %** |

The custom land covers the whole terminal with a sensible 0.70 mm toe. KiCad's covers half
of it and puts 1.20 mm of pad beyond the body edge. **KiCad's land is correct for an
FXL0630 — a different part — and must not be used here.** The footprint's `descr` now says
so in its first line; the filename was deliberately left alone to avoid board churn.

The lesson: a curated library land is only authority for *the part it is named after*.
Verify against the fitted part's own terminal dimensions. The name on a footprint is not
evidence.

The round trip cost a footprint swap and a courtyard fix, both reverted. Two things
survive it and are worth keeping:

- **`poppler` is now installed** (`winget install oschwartz10612.Poppler`). It is what read
  the PNLS6045 dimension table and caught the error. The pre-existing `pdftotext` in
  `/mingw64/bin` is **Xpdf 4.06, not poppler** — its `pdftoppm` emits PPM only, no `-png`.
  Binaries live under `AppData/Local/Microsoft/WinGet/Packages/oschwartz10612.Poppler_*/
  poppler-25.07.0/Library/bin/`. LCSC datasheet drawings are vector art, so `pdftotext`
  alone returns only dimension *labels*; render the page and read it.
- **C25/C26 stayed at x = 192.9** (moved 0.4 mm east during the swap). Harmless with the
  narrower courtyard restored, and already verified there, so they were not moved back.

**(c) ~~WS2812B pinout~~ — VERIFIED.** The WS2812B-V5/W (`C2874885`) pin function table
matches the board's `LED_SMD:LED_WS2812B_PLCC4_5.0x5.0mm_P3.2mm` exactly:

| Pin | Datasheet | Board net |
|---|---|---|
| 1 | VDD, power supply | `+5V` |
| 2 | DOUT, data out | `unconnected-(D7-DOUT-Pad2)` — chain end, correct |
| 3 | VSS, ground | `GND` |
| 4 | DIN, data in | `/Addressable/STATUS_D` |

The V5/W substitution for the uncatalogued V6 is safe. Note LCSC's `www.lcsc.com/datasheet/
...pdf` URL serves **HTML**, not a PDF; the real file is the `datasheet.lcsc.com/datasheet/
pdf/<hash>.pdf` link inside it.

**(d) J1** is hand-fit per spec §10, but 16 pads at 0.5 mm pitch plus four shield legs is
not sensibly hand-soldered. Machine-placing needs an LCSC part number it does not have;
`export-cpl.sh` warns if the tier changes without one.

### 5. Documentation that contradicts the design — DONE except the firmware

`VANDIMMER-4CH-2ADDR-SPEC-v2.0.md` has been corrected and now carries an as-built banner
pointing at `DECISIONS-2026-08-29.md` as authoritative. Twelve edits; the ones that
mattered beyond the two originally listed:

- **Line 30 advertised "10-30 V DC" input.** That is the most dangerous line the spec had:
  above ~20 V the SMBJ18A conducts continuously and dies, and above 32 V the buck does.
  Now "12 V nominal, 10-16 V DC", and the transient row names the real 29.2 V clamp
  rather than "40 V".
- SMBJ33A → SMBJ18A in both §4.1 and the §12 parts list; F1 7 A → 8 A in both.
- Channel loss 0.015 Ω / 0.06 W → 0.029 Ω / **0.116 W**, with the reason recorded (the
  old figure assumed a 10 V gate drive the 74HCT125 does not provide). LDO row 0.26 W →
  the measured 0.55 W; board total ~2.5 W → ~2.4-2.7 W.
- The "24 V variant" row said "commercial question, not technical". It is technical: D5
  *and* U2 must change together, and an SMBJ33A cannot be the answer.

**`bom-lcsc.csv` had drifted from the schematic too** — it still named the 10 A fuse and
the AMS1117 after both were superseded, though every LCSC code was already correct.
Fixed, and `tools/cpl.py` now cross-checks the two sheets on every run: the schematic owns
what a part *is* (Value, Assembly), `bom-lcsc.csv` owns where it is *bought* (LCSC, tier).
A superseded Value can no longer reach the BOM Comment.

Still outstanding:

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
| `fpspace.py --courtyards` | whole-board courtyard-overlap sweep. Run after ANY footprint swap or move — a courtyard overlap is a DRC *error* and is not predicted by pad clearance |
| `fpspace.py` | clear-space finder for a **footprint**. Tests pads, vias, **tracks** and silkscreen — an earlier ad-hoc check built a track list and never used it, reporting 4648 "clear" 0805 sites in a corridor packed with the GATE_IN fan-out |
| `silkspace.py` | clear-space finder for board text. Correct `F.SilkS` filter, includes refdes text, prints the item count so a broken filter is obvious |
| `export-cpl.sh` / `cpl.py` | **assembly data** — JLCPCB CPL + JLC BOM. Filters the unfiltered position export using `bom-lcsc.csv`'s library tier, so CPL and BOM cannot disagree. Cross-checks Value/LCSC against the schematic and warns on drift; refuses to write if any footprint has no BOM line |

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

14. **`add_board_text` is a one-way door.** No tool deletes, moves or edits a board-level
    `gr_text` — only `add_board_text` exists. Get the position right the first time or the
    only remedy is a GUI delete. Verify the target area *before* adding, not after.
15. **The silkscreen layer is `"F.SilkS"`, not `"F.Silkscreen"`.** The DRC report prints
    "F.Silkscreen", so a filter written from the report matches **nothing** and every
    region reads as empty. That is exactly how two board texts were placed into J7/J8's
    connector outlines after a clearance check that "passed". `tools/silkspace.py` has the
    correct filter and a self-check that prints the item count — if it says 0, the filter
    is wrong again. Occupancy must also include `fp_text` reference designators, which are
    silkscreen too.

16. **Use KiCad's own F8 sync, not Konnect's `update_pcb_from_schematic`.** Konnect's
    refuses this board outright with `reference_identity_conflict` on U4 and U5, because
    their board footprints carry a path built from a *non-unit-1* UUID and the sync expects
    unit 1. That state is byte-identical to what was committed, KiCad's own parity check
    does not object to it, and `footprints_added` comes back 0 — so nothing gets added.
    **Tools → Update PCB from Schematic (F8) has no such objection and does not inject
    phantom pads either**, so it sidesteps gotcha 1 completely. It also repaired all 106
    parity items in one go. This is now the preferred route for any schematic→PCB sync
    on this board.



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
