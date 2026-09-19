# VANDIMMER Rev B — verification checklist, learnings and outstanding work

**Updated:** 2026-09-13 · **Branch:** `vddimmer-rev-b` · **Status:** placement complete, pre-routing

Companion to `REV-B-DESIGN-2026-09-12.md` (the spec, binding) and
`REV-B-PLAN-2026-09-12.md` (the 16-task plan). This file is the working checklist: what
still has to happen, what each gate *actually* checks, and every tool trap found so far.

---

## 0. Session resume — read this first after a reboot

| | |
|---|---|
| Branch | `vddimmer-rev-b`, **13 commits ahead of `origin/vddimmer-rev-b`** (tip `8e4e196`), unpushed |
| Working tree | clean |
| Tasks done | 1, 2, 3, 4, 6, 7, 8, 9, 10, 11, 12, 5 — **all placement complete** |
| Next | Task 13 (render, KiCad **closed**) → **HARD STOP** for visual approval → Tasks 14–16 (routing) |
| Ledger | `.superpowers/sdd/REV-B-PLAN-2026-09-12/progress.md` — trust it over reconstruction |
| Geometry record | `analysis/rev-b-task8-geometry.md` — measured keepout, U1 window, harness arithmetic |

**KiCad open/closed matters.** Placement and routing need it **running** (IPC). Zone
inserts, `kicad-cli --save-board` and the renders need it **closed**. Never both.
Stuart opens and closes it by hand.

**The board on disk is stale whenever KiCad holds it with unsaved edits.** Every tool in
`tools/` reads disk. Always `save_project` over IPC before running any of them, or you
will analyse the previous state and never know.

---

## 1. Safety-critical — do these before fab, not after

### 1.1 ⚠️ Mark the board REV B, unmistakably

**This is the highest-value outstanding item on the list.**

Rev A and Rev B are physically near-identical boards with **opposite J3–J6 polarity**:

| | pin 1 | pin 2 |
|---|---|---|
| Rev A | `VIN_PROT` (V+) | `DRAIN` |
| **Rev B** | **`DRAIN` (LED−)** | **`VIN_PROT` (V+)** |

A harness built for one, plugged into the other, reverse-biases the strip. Worse, spec §6
records that *grounding pin 1 on a Rev A board is a dead short across the supply*. With
both revisions alive in the same van this is a real hazard, and silk is the only thing
standing between them.

Required: large, unambiguous **`REV B`** on `F.SilkS`, plus the existing board ID. Ideally
somewhere visible with the enclosure open. Consider also a visual difference at the
harness row so the two revisions cannot be confused at a glance.

### 1.2 Freeze the connector, then build the footprint

Open — see §4.1. Nothing routes until the J3–J8 footprints are final, because the pitch
change moves pads.

---

## 2. Verification gates — and what each tool *actually* does

The plan lists these as though each does what its name suggests. Three do not. Verified
behaviour:

| Gate | Tool | What it really does | Status |
|---|---|---|---|
| Courtyard overlap | `fpspace.py --courtyards` | Genuine pairwise overlap check, exit 1 on failure | ✅ **0 overlaps, exit 0** |
| Courtyard vs outline | `crtyd.py` | ⚠️ **Only lists courtyards — never tests Edge.Cuts.** Needs a separate check | ✅ own check: only U1 outside, by design |
| Silk clearance | `silkspace.py` | ⚠️ **Not a violation checker — it finds clear areas for placing text.** Use DRC for silk violations | use DRC |
| Hot loop | `measure.py` §1 | Real: C23 ↔ U2 pins 3/4 loop area + vias inside bbox | ✅ **2.51 mm², 0 vias** (gate <15) |
| Switch node / FB divider | `measure.py` §2–4 | Real, but measures **existing copper** — meaningless until re-routed | ⏸ after Phase 5 |
| Clearance pre-route | `route.py` | ⚠️ Blind spot: **does not synthesise a via at layer transitions.** Sweep proposed vias separately | ⏸ Phase 5 |
| Connectivity | `connect.py` | The gate Rev A failed. Every net one group. **Run only after final zone refill** | ⏸ Phase 5 |
| DRC | `drc.sh` / `kicad-cli pcb drc` | 0 errors. Review severity overrides rather than trust them | ⏸ 1037 violations = stale Rev A copper |
| EMC | `emc.py` | Expect 42-of-60 pour-gap crossings to collapse once In1.Cu is a plane. **Confirm, don't assume** | ⏸ Phase 5 |
| Drills | `drill.py` | Zero overlapping drills. Handles slot geometry | ⏸ Phase 5 |
| Plane necks | manual | At **every** mounting hole, against the current it carries. Rev A pinched to 1.9 mm ≈ 3.8 A at H2/H3 | ⏸ Phase 5 |

### 2.1 `connect.py` — the 12-split baseline is CORRECT

Do not chase these. Only the first three are Rev B's job:

| Category | Count | Waivable? |
|---|---|---|
| `J11.1` (BTN1), `J9.1`+`J10.1` (12 V select), `D9.1` | 3 | ❌ **Rev B must fix** |
| DPAK centre leads (DRAIN1-4, VIN_FUSED) | 5 | ✅ pad and tab are one node in-package |
| D8 bonded pin pairs (USB_DM, USB_DP) | 2 | ✅ signal passes through the ESD device |
| U3 SOT-223 tab (+3V3) | 1 | ✅ U3.2 and tab are the same node |
| VBUS fragmentation | 1 | ✅ consequence of the no-USB-power decision |

**KiCad reports a split net as an unconnected *item*, never an error.** That is exactly how
Rev A shipped three dead pads.

---

## 3. Tool traps — hard-won, do not rediscover

### KiCad / tooling
1. **Never run DRC on a `.kicad_pcb` separated from its `.kicad_pro`.** KiCad silently
   falls back to default design rules and invents violations — a scratch-dir copy produced
   16 phantom `drill_out_of_range` + 4 `via_diameter` that vanish under the project's own
   rules.
2. **KiCad 10 copper numbering is not physical order**: `F.Cu=0, B.Cu=2, In1.Cu=4,
   In2.Cu=6`. The file lists inner layers last. Verify with `pcbnew` `CuStack()`, never by
   reading the `(layers)` block.
3. **Disk is stale while KiCad holds unsaved edits.** Save over IPC first.
4. After any KiCad save, re-check anything edited on disk — a session once wrote back its
   in-memory copy and silently reverted a board-outline edit, invisible to `git diff`
   because the save landed 53 s before the commit.
5. This project grades **`courtyards_overlap = error`** and **`min_text_height = 0.8 mm`**.
6. `.kicad_prl` is tracked and churns when the layer set changes — commit it to keep the
   tree clean.

### Konnect MCP
7. **Konnect tools are NOT reachable from subagents.** `load_toolset` succeeds server-side
   while every tool stays uncallable in a child agent. Do all Konnect work in the
   controller session.
8. **`snapshot_project` writes the schematic PDF but NOT the `pcb_snapshot` it reports**
   (reproduced 3×). Git history of the `.kicad_pcb` is the only PCB rollback.
9. **`update_pcb_from_schematic` cannot handle multi-unit symbols.** U4/U5 are 5-unit
   74AHCT125s; it raised `reference_identity_conflict` and refused to apply. The board was
   fine — its U4 correctly pointed at sheet `a2332e94` / symbol `67ad5e47`, one of the five
   units. **Use KiCad's native Update PCB from Schematic (F8)**, with *"Replace footprints
   with those specified in the schematic"* ticked (it is **off by default**, and nothing
   happens without it).
10. **No Konnect tool deletes board text.** `add_board_text` is one-way; removal is a GUI
    action. Get the size right first time.
11. Konnect success responses are not evidence — two documented lies on this project.
    Verify by read-back, always.

### Footprint facts measured this session
12. **All four connector families on this board face +y at rotation 0** (JST XH 2- and
    4-way, Phoenix MC, USB-C). Harness edge = rot 0; left edge = rot 270.
13. **U1's courtyard is 41.25 × 48.05 mm** because it encloses the whole antenna keepout.
    On-board it blocks **x 180.00–200, y 61.95–110.05** — far more than the 18.1 mm-tall
    module body. Nothing may be placed there. Do **not** trim the placed instance's
    courtyard; that diverges it from the library silently.
14. Antenna keepout is **48.00 × 21.00 mm**, declared across `F.Cu`, `B.Cu` and
    `In1..In30.Cu`, so it already covers the 4-layer stack. **Rotation 270 points it +x.**

---

## 4. Open decisions

### 4.1 Connector family for J3–J8 — BLOCKS ROUTING
Decision taken: **switch to 2.54 mm**. Blocked on confirming the exact part.

Board-side headers, verified on Mouser 2026-09-13:

| | Molex MPN | Mouser | Stock | @100 | Status |
|---|---|---|---|---|---|
| 2-way R/A (J3–J6) | **22-05-3021** | 538-22-05-3021 | 31,297 | $0.193 | Active |
| 4-way R/A (J7/J8) | **22-05-3041** | 538-22-05-3041 | 83,463 | $0.222 | Active |

Cable side: housings **22-01-3027** (2-way) / **22-01-3047** (4-way).
⚠️ Crimp terminal **08-50-0114 is End-of-Life** — find a live replacement before designing
it into a harness BOM. Pattern: `22-05-30xx` = right-angle, `22-27-20xx` = vertical.

**Caveat:** the lamps arrive with plugs already fitted, so the lamp housing chooses the
header. KF2510 / "CNDZ 2510" are KK 254 *clones*; ramp and rib detail varies by vendor and
that is exactly what sets latch orientation. **Test-mate a lamp plug before ordering.**

**Latch orientation reverses polarity.** A 2-pin latch stops rotation about the insertion
axis; flip which face the latch is on and the plug must enter rotated 180°, swapping left
and right. Same plug, same crimp, opposite pad. Rev B *also* reverses pin order vs Rev A,
so two flips can cancel and appear to work — then bite on the next restock.

**Connector-agnostic polarity test:** pin 2 of J3–J6 is one common net (`VIN_PROT`); pin 1
is four distinct drains. With an unpowered board, buzz between any two of J3–J6 — the pads
that read continuous are V+. No power, no datasheet, immune to connector choice.

**Footprint work still to do:** KiCad ships KK-254 in **vertical only**. A custom
horizontal footprint must be authored in the `VANDIMMER` library from the datasheet, for
both 1x02 and 1x04. Row grows ~0.4 mm; gaps go 4.06 → ~3.99 mm.

#### 4.1.1 ✅ RESOLVED 2026-09-19: the connector forces the pin order back to Rev A's

**Result:** Molex 22-05-3021 arrived and was fitted to a **Rev A** board (pin 1 =
`VIN_PROT`). The Molex keyway sits **high, away from the PCB**. A pre-plugged lamp mated
to it lit at 50 % (all channels set to 127 over the serial console). An LED lamp does not
light reverse-biased, so the lamp's **+** lands on board pin 1. **Rev A's order is correct
with this header, and Task 4 (`55af04c`) must be reversed.**

**Revert done 2026-09-19** (file side, KiCad closed):
- `PWM.kicad_sch`: J3–J6 labels swapped back so pin 1 = `VIN_PROT` and pin 2 = `DRAINn`.
  The Values are back to `CHn V+/LED-`. Checked by netlist diff: 81 → 81 nets, 294 → 294
  pins, and the **only** changes are the eight J3–J6 pin moves. ERC reports 0 errors.
- `.kicad_pcb`: eight new polarity marks at **1.5 mm** (the old ones were 0.8 mm), `+`
  beside pin 1 and `−` beside pin 2, in the same flanking positions. `REV B` added at
  3 mm, centred (154, 85.5).
- Spec §5.1, Rev B design §6 + decision 9, and HANDOFF §1 were amended explicitly, not
  reverted without comment.

**⚠️ Still needs a GUI pass** (Konnect can neither delete nor edit board text, and pad
nets update only through F8):
1. Open the PCB editor and press **F8** (Update PCB from Schematic). The J3–J6 pads
   still carry the old nets until this runs. Leave "Replace footprints" **off**.
2. Scripting console: `exec(open(r"C:/Projects/vandaemon/hw/VDDimmer/tools/revb_silk_pass.py").read())`.
   It deletes the eight 0.8 mm marks and "Rev A" by UUID, sets the new marks' stroke to
   0.3 mm, and moves the title to (112.3, 98.8). Tested first on a copy of the board:
   silk_overlap 28 → 14, silk_over_copper 9 → 5, and no new silk violations.
3. Save, close KiCad, and read the J3–J6 pad nets back from disk.

When the KK 254 horizontal footprint replaces the XH land, re-check the marks against its
silk. They are placed for the XH outline.

The alternative below (a header with the opposite latch face) no longer applies. Stay on
22-05-3021 and change the pin order instead. J7/J8 (§4.2) were **not** part of this test
and are still open.

*Original analysis, kept for the reasoning:*

**Ordered 2026-09-14: Molex 22-05-3021 (2-way R/A).**

Stuart's observation: his existing 2510 stock has the keyway **toward the PCB**, while the
Molex appears to have it **away from the PCB**. If so, the same plug enters rotated 180°
and what used to reach pin 1 now reaches pin 2.

**The lamps arrive with plugs already fitted, so the wire-to-cavity mapping cannot be
changed.** The board is the only thing that can adapt. If the lamp's **+** lands on board
pin 1, then pin 1 must be assigned `VIN_PROT` — **which is Rev A's order, and undoes
Task 4 (`55af04c`).**

Note the distinction: board pin 1 is a *pad* with a fixed net. The connector does not
change which pad is pin 1; it changes which *wire* lands on it. So this is a decision to
reassign the pad, not an observation about the part.

Consequence if it flips — amend spec **decision 9** and **§6** explicitly, do not revert
quietly. It re-opens the hazard §6 closed ("grounding pin 1 on a Rev A board is a dead
short across the supply"). That remains acceptable because the real defence was never the
numbering — it is the `+`/`−` silk and never writing `GND` — but it must be recorded.

**Test when the parts arrive (30 s, no board, no power):** mate a lamp plug to a loose
header in hand; note which header pin the **+** wire sits on, counting from the pin-1 end.
Repeat for J7/J8 with a tri-colour strip and a 22-05-3041 — there V+ and GND are at
opposite ends, so an end-for-end flip destroys a WS2815 rather than merely failing to
light. That test also answers §4.2.

**Work if it flips** (schematic + silk only; nothing in the layout moves, cheap **only**
while pre-routing): swap J3–J6 pin nets back (exact reverse of Task 4) · flip the eight
`+`/`−` marks (needs a GUI delete — Konnect cannot remove board text) · amend spec
decision 9 and §6 · regenerate netlist and ERC.

**Alternative that preserves the spec:** source a right-angle header with the opposite
latch face, keeping pin 1 = LED−.

### 4.2 J7/J8 pin order vs how the strips present — POSSIBLE SCHEMATIC CHANGE
Board is currently **pin 1 = V+, 2 = DATA, 3 = CLK, 4 = GND**, so V+ and GND sit at
opposite ends. An end-for-end mismatch puts the supply backwards across a WS2815 and
destroys it — far worse than J3–J6, where the worst case is "doesn't light".

Agreed principle: wire J7/J8 to match how the strip presents. **Need: the actual wire order
out of Stuart's tri-colour strips.** If it is the usual WS2815 `12V / GND / DI / BI`, that
does not match, and J7/J8 must be reordered in the schematic the same way Task 4 reordered
J3–J6 — cheap now, expensive after routing.

### 4.3 Carried from spec §8
- Enclosure: PLA prototype with vents over the top-left hot corner; PETG/ASA for the
  vehicle. Board dissipates ~2.5–3 W, ~70 % of it U2 (1.1–1.4 W) and Q_REV (0.75 W).
- `C44.1` is fitted and connected to nothing (no-USB-power decision). Drop it or tie it
  deliberately in Rev B.

---

## 5. Board additions not yet made

| Item | Priority | Notes |
|---|---|---|
| **`REV B` silk marking** | **critical** | §1.1. Opposite polarity to Rev A on identical-looking hardware |
| Logo on silk | wanted | `import_svg_logo` (pcb_board toolset) imports an SVG as silk. Curves are flattened to polygons. Needs a clear area — check with `silkspace.py`, which reports clear 20×6 mm centres |
| Board name + date/order code | nice | Helps field diagnosis and matching boards to a JLC order |
| Fiducials | evaluate | Finest pitch is SOIC-14 / SOT-23-6, so probably not required. 3 fiducials are cheap insurance if JLC asks |
| Silk cleanup pass | should | DRC still reports 20 `silk_overlap`, 9 `silk_over_copper`, 1 `silk_edge_clearance` — refdes-on-refdes from the re-placement. **None involve the J3–J6 polarity marks** (verified) |
| Test points | evaluate | At minimum `VIN_PROT`, `+5V`, `+3V3`, GND would help bring-up |

---

## 6. Phase 5 — routing plan (Tasks 14–16)

**Task 14 — zones, KiCad CLOSED** (zone inserts are refused while KiCad holds the board)
1. `GND` zone filling all of `In1.Cu`.
2. `VIN_PROT` and `+5V` zones on `In2.Cu`, split by region.
3. Pull all four layers back from the antenna keepout.
4. Refill and read back — `add_copper_pour` writes a zone a tab-indentation-keyed parser
   will not see, so refill before trusting any read:
   `kicad-cli pcb drc --refill-zones --save-board`.
   Expect `In1.Cu` GND as **one island**; more means a keepout or hole split it.
5. ⚠️ New zones are **thermal-relief** by default (`add_zone` takes only `clearance` and
   `min_width`), which gives `starved_thermal` errors on a crowded pour. Pad connection →
   **Solid** is a GUI-only change. Batch it with any other GUI work.

**Task 15 — routing, KiCad RUNNING**
Order: `VIN_PROT` from J2 through F1/D5/Q5 → switch node with minimum copper area → the
four drain runs → addressable outputs and jumpers → USB `D+`/`D−` as a short pair →
remaining signals.

Watch: U1's decoupling (C40–C44) sits 9–12 mm away rather than the usual 3–5 mm, forced by
the courtyard. **Give those short, wide vias straight down to the In1.Cu GND plane** — the
4-layer stackup is what makes that cheap, and it is the mitigation for the distance.

**Task 16 — final gate**
Refill → `connect.py` (every net one group, §2.1 exceptions only) → `drc.sh` →
`measure.py` → `emc.py` → `drill.py` → plane necks → confirm `J11.1`, `J9.1`, `J10.1` are
alive.

---

## 7. Pre-fab / manufacturing

1. **Regenerate `VANDIMMER-4CH2A.net`** after any schematic change — it is an export, not a
   source. It was once four weeks stale and wrong about the GPIO map.
2. **Group the fab BOM by LCSC code.** A duplicated code once zeroed eight lines at JLC.
3. **Catalogue parts are not assembly parts.** Check `has_easyeda_footprint` before
   committing a part — this already cost F1 a swap, and is why PF1/PF2's replacement was
   chosen for sharing the outgoing part's footprint UUID.
4. **Assembly tier: Standard, not Economic.**
5. **Position file:** `pcb export pos --smd-only` returns **zero rows** unless footprint
   type (`attr smd`) is set — a GUI-only property. The existing tooling works around this;
   see commit `32695c9`.
6. Confirm the **4-layer stackup order** with JLCPCB (L1 sig / L2 GND / L3 power / L4 sig),
   1.6 mm.
7. Board is 100 × 84 mm — inside the ≤100 × 100 mm price bracket. If more room is ever
   needed, **grow the top edge** (y 56 → 40): every bottom-anchored item (harness row, J2,
   H3/H4) then stays put.
8. Re-quote: bare PCB is ~6 % of build cost at 2 layers, ~17 % at 4; the 4-layer delta was
   ~+$1.00/board at both qty 5 and qty 30.
