# SDD ledger — plan: hw/VDDimmer/REV-B-PLAN-2026-09-12.md

Spec: `hw/VDDimmer/REV-B-DESIGN-2026-09-12.md` (read, reachable)
Branch: `vddimmer-rev-b`, forked from `vddimmer-fab-prep` at `4e42970`

## Pre-flight conflict scan

### Task-pair rows (tasks sharing a file or an interface)

| tasks | shared | produces → consumes | finding |
|---|---|---|---|
| T2 → T3 | `bom-lcsc.csv` | T2 writes the U1 row; T3 writes the PF1/PF2 rows | clean — different rows, sequential execution, each re-reads |
| T4 → T5 | J3–J6 pin order | T4 sets pin 1 = `DRAINn`; T5 silkscreens `−`/`+` to match | clean — correct dependency order |
| T4 → T6 | schematic → netlist | T4 edits `PWM.kicad_sch`; T6 re-exports `.net` | clean |
| T2,T3,T4 → T6 | schematic → ERC | all three edit sheets; T6 runs ERC over them | clean — T6 correctly last in Phase 2 |
| T5 → T7 | `.kicad_pcb` | T5 adds silkscreen; T7 changes layer count | **CONFLICT — see R2** |
| T7 → T8 | `.kicad_pcb` | T7 sets 4 layers; T8 draws outline/keepouts on them | clean |
| T8 → T9–T12 | `.kicad_pcb` placement | T8 fixes U1 + outline; T9–T12 place against it | clean — U1 first is correct, its antenna edge constrains the rest |
| T9 → T10 | J2 position | T9 places J2 on the left edge; T10 places J3 relative to it | clean |
| T10 → T13 | courtyards | T10's spacing feeds T13's render and checks | clean |
| T12 → T13 | placement complete | T13 renders and gates | clean |
| T14 → T15 | zones → traces | T14 needs KiCad closed, T15 needs it running | clean — plan states both explicitly |
| T15 → T16 | copper → connectivity | T16's `connect.py` runs after T14's refill | clean |

### Per-task self-consistency rows

| task | own text agrees with itself? |
|---|---|
| T1 | yes — creates `analysis/rev-a-baseline.txt`, commits the same path |
| T2 | yes — symbol, footprint, LCSC and BOM all named consistently |
| T3 | yes — search, edit, read-back, commit are consistent; correctly refuses to drop the 30 V rating |
| T4 | yes — verification asserts exactly the swap the step makes |
| T5 | **no — says "KiCad running" inside a phase headed "KiCad closed". See R2** |
| T6 | yes |
| T7 | yes |
| T8 | yes — M2.5 now stated, blocker cleared |
| T9 | yes — gate (`measure.py`, hot loop < 15 mm²) matches the work |
| T10 | yes — measured widths match the spec's §5 figures (71.04 mm, 4.16 mm gaps) |
| T11 | yes |
| T12 | yes |
| T13 | yes — closes KiCad before `kicad-cli`, consistent with the global constraint |
| T14 | yes — KiCad closed for zone inserts, matches the constraint |
| T15 | yes — notes `route.py`'s via blind spot |
| T16 | yes — `connect.py` after refill, matches the spec's §9 gate |

## Rulings

Ruling: use a **branch, not a git worktree** — the skill's default isolation. KiCad GUI
work, the renders, and Stuart's visual verification all happen against the working path he
already has open; a worktree would put the board at a different path than the one he opens.
`vddimmer-rev-b` off a non-main branch is isolation enough. Cost if wrong: the main
checkout carries in-progress board state, so a mid-plan switch to another branch would need
a stash.

Ruling: **move Task 5 (J3–J6 silkscreen) to run after Task 12**, not inside Phase 2. Its own
text says "with KiCad running" while the Phase 2 header says all of Phase 2 runs with KiCad
closed — a direct contradiction, and the global constraint forbids having it both ways.
Placement is the natural home: silkscreen text anchors to the footprint and travels with it,
and running it after Task 12 means Task 13's render shows the polarity marks, which is
exactly what Stuart needs to see at the visual checkpoint. Task order becomes
1,2,3,4,6,7,8,9,10,11,12,**5**,13. Cost if wrong: a wasted round if the marks need moving
after placement — cheap, and cheaper than a KiCad open/close cycle mid-phase.

## Progress

Task 1: dispatched (sonnet, BASE 4e42970). Branch already created by controller;
implementer starts at Step 2 (Konnect snapshot).

Task 1: complete (commit f013c46, controller-verified)

Ruling: the 12 split nets are NOT a defect and NOT tool noise — my brief's stated
expectation of "3 known" was wrong. It quoted the Rev B ordering gate, which lists only the
three blocking splits, as though it were the whole baseline. Decomposition, verified against
analysis/rev-a-baseline.txt: 3 real (J11.1 / J9.1+J10.1 / D9.1); 5 DPAK centre leads
(DRAIN1-4, VIN_FUSED — pad and tab are one node in-package); 2 D8 bonded pin pairs
(USB_DM, USB_DP — the signal passes through the ESD device); 1 U3 SOT-223 tab (+3V3 —
U3.2 and the tab are the same node); 1 VBUS fragmentation, a consequence of the deliberate
no-USB-power decision, including an isolated C44.1. All eight package-internal splits are
the exact categories HANDOFF.md names as waivable. Task 16's gate text already states them
correctly, so no plan change is needed. Cost if wrong: if any "package-internal" split is
actually a real open, Rev B would inherit it — mitigated because Rev A demonstrably works
(USB enumerates, the ESP32 is powered, all four channels drive).

Ruling: emc.py reported 43 of 60 against the handoff's 42. One net, and the handoff's figure
was taken after a zone refill which this baseline run did not do. Not chased. Cost if wrong:
negligible — Task 16 re-measures after an explicit refill, which is the number that counts.

Ruling: no task reviewer dispatched for Task 1. It records tool output to a text file and
changes no design artifact, so there is no spec surface to review; I verified the report,
the committed baseline and every concern myself. Tasks that touch the schematic or PCB will
get a dispatched reviewer. Cost if wrong: a baseline file with a bad number, which every
later task would contradict loudly rather than silently.

Task 1: concern (real, actionable) — Konnect snapshot_project writes the schematic PDF but
NOT the pcb_snapshot it reports; reproduced 3x by the implementer. Git history of the
.kicad_pcb is the rollback for all PCB tasks. Carry this into T7 onward.

Task 1: minor (deferred) — C44.1 is fitted and connected to nothing, a consequence of the
no-USB-power decision. Harmless. Rev B should either drop it or tie it deliberately.

Task 2: complete (commit see git log, controller-executed, read-back verified)

Ruling: **Konnect MCP tools are not reachable from subagents.** Task 2's implementer
reported load_toolset succeeding server-side (tools_added: 20, 0 errors) while every tool
remained uncallable, and ToolSearch found no matching deferred tools. In the controller
session the same load_toolset call registers them as deferred tools and ToolSearch loads
their schemas normally. So the deferred-tool registration is session-local and does not
propagate to a child agent. Consequence: every Konnect operation -- which is most of this
plan -- must run in the controller session. Subagents keep the shell, file, verification
and reporting work. Cost if wrong: I am doing implementation in the controller session,
which pollutes my context and skips the dispatched-reviewer gate; mitigated by read-back
verification on every write and by the fact that the alternative is not executing at all.

Task 2: two anomalies the implementer flagged, both resolved.
 - lib_id already read RF_Module:ESP32-S3-WROOM-1 under the old -1U value. Not a defect:
   one symbol serves both variants since they share pins. No replace_component needed.
 - A stale MPN field the brief did not mention. Updated alongside LCSC.

Task 2: incidental finding for Task 8 — H1/H2/H3 currently use
MountingHole:MountingHole_3.2mm_M3. The M2.5 decision means these footprints change too,
which the plan's Task 8 does not currently spell out.

Ruling: PF1/PF2 part is **LUTE 1812L300/30GR, LCSC C48985875** (3 A hold / 5 A trip /
30 V, 1812, stock 34,960, $0.1696). Only three parts in the JLC DB meet >=3 A hold AND
>=30 V in 1812; chosen over C54300298 (33 V, 6.8k stock, $0.2172) and C47002391 (BNstar,
5.5k stock) because it is the same LUTE 1812L series as the outgoing 2 A part AND reports
the identical EasyEDA footprint UUID ab17c647a0a943b09958724a36a719c7 — i.e. demonstrably
an assembly part, not catalogue-only. That is the exact failure that already forced an F1
swap (LUTE LTC2410-1800TSK had no EasyEDA data and JLC refused to select it). Cost if
wrong: a rejected BOM line at order time, caught by JLC's own part check before money
moves.

Ruling: value string is **"PTC 3A 30V"**, not the plan's bare "3A". The plan's literal
would break the sheet's existing convention ("PTC 2A 30V") and drop the voltage rating
from the schematic face — and bom-lcsc.csv keys its first column on that exact string, so
a bare "3A" would orphan the BOM row. Cost if wrong: cosmetic, a one-field re-edit.

Task 3: complete (commit f3a2cf0, controller-executed, read-back verified)

Task 3: note — PF1/PF2 carry MPN and Manufacturer fields the plan does not mention, the
same stale-field trap Task 2 hit. All four fields (Value, LCSC, MPN, Manufacturer) were
updated together; grep confirms zero occurrences of "PTC 2A 30V", "C18198342" or
"1812L200/30GR" remain in Addressable.kicad_sch. Footprint unchanged
(Resistor_SMD:R_1812_4532Metric) — the 3 A part shares the 1812 land.

Task 4: complete (commit 55af04c, controller-executed, netlist-diff verified)

Ruling: Task 4 also swaps the **Value strings** ("CH1 V+/LED-" -> "CH1 LED-/V+") on all
four connectors. The plan does not mention them, but those strings encode the pin order on
the schematic face; leaving them would have the drawing asserting Rev A polarity while the
netlist asserts Rev B — the precise wrong-polarity hazard the task exists to remove, and
the drawing is what a human reads when wiring a harness. Cost if wrong: a cosmetic field
re-edit, no netlist impact.

Task 4: implementation note — the sheet does not wire J3-J6 pin 1 with wires. Each pin 1
carried a VIN_PROT *global label* placed on the pin coordinate and each pin 2 a DRAINn
*local label*, so the swap was a label exchange (8 deletes + 8 adds at rot 180, matching
the pins' 180-degree orientation), not the disconnect/reconnect the plan describes. J3 was
the one exception: its VIN_PROT label sat at x=242.57 with a 2.54 mm stub wire to the pin.
That wire was deleted so all four connectors are now wired identically.

Task 4: verification was a full before/after net-membership diff, stronger than the plan's
per-connector check: 81 nets before and after, zero nets added or removed, and the only
membership deltas are the eight intended pin moves (DRAIN1-4 each lost Jn.2 / gained Jn.1;
VIN_PROT lost J3-J6.1 / gained J3-J6.2). Nothing else on the sheet moved.

Task 6: complete (commit 511002a) — netlist regenerated, ERC exit 0 / 0 violations.
Regenerated .net confirmed to carry the Task 4 swap. Phase 2 (schematic) complete.

Task 7: complete (commit 4511596) — In1.Cu + In2.Cu added as power-type planes.

Ruling: verify the stack with pcbnew's CuStack(), not the file's (layers) listing. KiCad 10
numbers copper F.Cu=0, B.Cu=2, In1.Cu=4, In2.Cu=6, so the new layers appear last in the
file and numerically out of order while being physically correct. CuStack() reports
F.Cu -> In1.Cu -> In2.Cu -> B.Cu. Reading the file listing alone would have looked like a
defect and invited a pointless "fix". Cost if wrong: none — cross-checked by a DRC diff.

Task 7: verification — DRC pre vs post with IDENTICAL project context is bit-identical
across every violation class (15 unconnected errors, 89 lib_footprint_mismatch, 30
silk_over_copper, 25 silk_overlap, 9 via_dangling, 7 track_dangling, 3 lib_footprint_issues,
2 silk_edge_clearance). The layer add introduced nothing. All of these are pre-existing
Rev A state that Phases 4-5 rework.

Task 7: note for Task 16 — a first comparison was contaminated by running DRC on a board
copied WITHOUT its .kicad_pro; KiCad silently fell back to default design rules and
invented 16 drill_out_of_range + 4 via_diameter violations that vanish under the project's
own rules (min_via_diameter 0.45, min_through_hole_diameter 0.2, min_hole_to_hole 0.25).
No rule_severities are set to "ignore" for drill or via classes, and drc_exclusions is
empty, so nothing is being suppressed — but Task 16's "severity overrides reviewed rather
than trusted" gate should confirm these constraint VALUES are what Rev B wants, and
drill.py remains the real drill gate. Lesson: never DRC a .kicad_pcb separated from its
.kicad_pro.

Task 8: complete (commit 0e4ce50). Phase 3 (board setup) complete.

Ruling: **board outline stays 100 x 84 mm** (Edge.Cuts x 100..200, y 56..140). The spec
caps at 100 x 100 and says not to shrink below what the layout wants; 84 mm is the
existing height and the four floorplan bands (user face / power+MCU / protection+level
shifters / harness) fit it, as Rev A demonstrated in the same envelope. Growing y to 156
(100 x 100) is free within the price bracket. Cost if wrong: if Phase 4 proves it tight,
the outline must be grown BEFORE the harness row is placed at y = 140, otherwise every
harness component moves. Flagged in Task 9's notes.

Ruling: **footprint identity changes go through the schematic, not the PCB.** H1-H4 are
schematic symbols (MCU.kicad_sch), not PCB-only items, and Konnect's delete_component /
move_component require live IPC while edit_schematic_component is file-based and safe with
KiCad closed. So Task 8 changed the footprint FIELDS (H1-H4 -> MountingHole_2.7mm_M2.5)
and the PCB side is applied by Update PCB from Schematic at the top of Phase 4. This also
carries U1's -1U -> -1 swap. Cost if wrong: if the sync does not preserve footprint
positions, H1-H4 and U1 need repositioning — which Phase 4 does anyway.

Task 8: incidental finding — **U1's PCB footprint was still RF_Module:ESP32-S3-WROOM-1U.**
Task 2 changed only the schematic field; nothing had pushed it to the board. Task 8 Step 3
("position U1 so the antenna overhangs") was therefore unachievable as written, because the
-1U footprint carries no antenna keepout. Resolved by the sync route above. This is the
same class as Ruling 6 (mounting holes) — the plan assumes schematic edits are already on
the board.

Task 8: measurements recorded in analysis/rev-b-task8-geometry.md (committed via a
.gitignore negation, matching the convention Task 1 set for rev-a-baseline.txt). Key
figures: antenna keepout 48.00 x 21.00 mm spanning F.Cu/B.Cu/In1..In30.Cu; rot 270 puts it
at +x (verified in pcbnew across all four rotations, not derived); U1 target (193.50, uy)
rot 270 giving keepout 100% off-board, pads 1.24 mm from the edge and 6.30 mm of module
overhang; M2.5 hole = 2.7 mm drill / 5.90 mm keepout diameter.

Task 8: prep for Task 10 committed (b90d76e) — courtyards measured from the live
footprints, not taken from the plan. Every harness connector is ~0.09 mm wider than the
plan states (J3-J6 8.49 not 8.40; J7/J8 13.49 not 13.40; J1 10.73 not 10.64), putting the
row at 71.67 mm and the six gaps at 4.06 mm. Also found: H4's 5.90 mm keepout at
(104.5, 135.5) currently occupies the space J3 needs; spec section 5 draws that hole
above-left of J3, so H4 moves inboard in Phase 4.

BLOCKED ON HUMAN ACTION: Phase 4 (Tasks 9-12, then 5) needs KiCad RUNNING. Confirmed by
probe, not assumed: update_pcb_from_schematic returned
"KiCad IPC is unreachable ... live-IPC-only and never edits the board file directly",
and move_component / rotate_component / delete_component are all IPC-only. Stuart opens
and closes KiCad by hand per his standing rule.

First action when KiCad is open: update_pcb_from_schematic (dry run, then apply with the
returned plan revision) to carry U1 -1U -> -1 and H1-H4 M3 -> M2.5 onto the board. Nothing
in Phase 4 can proceed before that — U1's antenna keepout does not exist on the board
until the -1 footprint lands.

Task 11: complete (commit adaa6e4). Task 12: complete (commit aa10987, fpspace 0 overlaps).
Task 5: complete (commit 563cc3d) — ran after Task 12 per the pre-flight ruling.

Ruling: U1's COURTYARD (41.25 x 48.05 mm) encloses the antenna keepout, so on-board it
blocks x 180.00..200, y 61.95..110.05 — far more than the 18.1 mm module body. With
courtyards_overlap graded as an ERROR in this project, that strip is unusable for
components and the whole right side was laid out around it. The library courtyard was NOT
trimmed to the body: editing a placed instance's courtyard diverges it from the library
silently, and lib_footprint_mismatch is already 90-strong on this board. Cost if wrong:
U1's decoupling C40-C44 sits 9-12 mm from the module instead of 3-5 mm. Mitigation is
short wide vias to the In1.Cu GND plane, which the 4-layer stackup makes cheap. Recorded
as a routing constraint in REV-B-CHECKLIST.md section 6.

Ruling: Q1-Q4 are NOT aligned per-channel with J3-J6; the block is right-shifted to
x 108.57..153.65. H4's 5.90 mm keepout occupies the bottom-left corner channel 1 would
want, and H4 has to be there to do the J2-to-J3 separation job spec 5.1 gives it. Cost if
wrong: slightly longer diagonal drain runs on channel 1. Rev A did the same.

Ruling: the plan's Task 5 gate (silkspace.py) does not test what the plan thinks.
silkspace.py is a CLEAR-AREA FINDER, not a violation checker; crtyd.py likewise only lists
courtyards and never tests Edge.Cuts. Both were replaced with explicit checks: DRC for silk
violations, and a purpose-written Edge.Cuts containment test (result: only U1 outside, by
design). Cost if wrong: none — the substitutes are strictly stronger.

Task 5: first pass used 0.7 mm text against the project's min_text_height of 0.8, giving 8
text_height violations. Fixed by moving R1-R8 up 0.7 mm to widen the band from 1.54 to
2.23 mm, then re-adding at 0.8 mm / y 125.91. Konnect has NO tool to delete board text —
Stuart removed the eight undersized marks by hand. Also rotated R1 180 -> 0; it was the
only one of R1-R8 at 180, which rendered its refdes below the part and onto J4's minus
mark. Final: text_height 0, zero silk violations involving any polarity mark, fpspace 0.

DESIGN INPUT from Stuart (2026-09-13), mid-Phase-4:
 - Connector pitch decision: switch J3-J8 to 2.54 mm to match his 2510 lamp plugs.
   Board-side parts verified on Mouser: Molex 22-05-3021 (2-way R/A) and 22-05-3041
   (4-way R/A), both Active. KiCad ships KK-254 in VERTICAL ONLY, so a custom horizontal
   footprint must be authored in the VANDIMMER library. NOT started — deliberately
   blocked on test-mating a lamp plug, since KF2510/CNDZ "2510" are KK 254 clones whose
   ramp detail varies by vendor and sets latch orientation.
 - Latch orientation reverses polarity for an identically crimped plug (180 deg about the
   insertion axis swaps left/right). Rev B ALSO reverses pin order vs Rev A, so two flips
   can cancel and appear to work. Connector-agnostic test recorded: J3-J6 pin 2 is one
   common net, pin 1 is four distinct drains, so an ohmmeter identifies V+ unpowered.
 - J7/J8 should be wired to match how the tri-colour strips present. Board is currently
   pin1=V+, 2=DATA, 3=CLK, 4=GND with V+ and GND at OPPOSITE ENDS, so a mismatch destroys
   a WS2815. AWAITING the strips' actual wire order from Stuart. May require a schematic
   reorder equivalent to Task 4.

Deliverable added: hw/VDDimmer/REV-B-CHECKLIST.md — full verification checklist, the 14
tool traps found this session, open decisions, and outstanding board additions. Top item
is SAFETY-CRITICAL and newly identified: Rev A and Rev B are near-identical boards with
OPPOSITE J3-J6 polarity, and spec 6 notes grounding Rev A's pin 1 shorts the supply. The
board must be marked REV B unmistakably before fab.

Task 13: complete (commit 219bc50). Renders in hw/VDDimmer/renders/. Placement gates pass:
fpspace 0 overlaps exit 0; hot loop 2.51 mm2; only U1's courtyard outside Edge.Cuts.

*** HARD STOP REACHED — Task 13 visual checkpoint. Awaiting Stuart's placement approval.
Do NOT start Tasks 14-16 (routing) until he approves. ***

Task 13: two silk defects the render found that NO numeric gate would have caught.
 1. The board is silkscreened "Rev A" at (164.31..168.79, 99.75..101.45) — not a missing
    Rev B mark but an actively WRONG one, on a board whose J3-J6 polarity is reversed from
    the Rev A it claims to be. Spec 6: grounding Rev A's pin 1 is a dead short across the
    supply. Highest-priority silk fix before fab.
 2. Title block "VANDIMMER-4CH+2A / v 1.0" at (103.92..123.08, 74.02..77.98) now sits on
    the relocated buck cluster.
Both are free board texts; Konnect can neither edit nor delete board text, so both need a
GUI pass. Replacement areas verified clear of all courtyards and footprint silk:
title -> x 102.3..122.3 y 95.8..101.8; REV B (large) -> x 145..163 y 79..92.

Ruling: exported an EXTRA clean render (renders/rev-b-placement-outline.svg, silk/fab/edge
only) beyond the plan's F.Cu view. Rev A's stale copper obscures the placement in the
F.Cu plot, and the whole purpose of Task 13 is visual placement approval. Cost if wrong:
one extra file.

SESSION PAUSED 2026-09-17 at Stuart's request, pending a Mouser delivery so he can check
connector orientation. State saved in three places:
 - hw/VDDimmer/RESUME-REV-B.md  — REWRITTEN for the current state; paste-in resume prompt
   carrying all 10 rulings, the two blocked tasks, and the two silk defects.
 - hw/VDDimmer/REV-B-CHECKLIST.md — working checklist (gates, 14 tool traps, open items).
 - Persistent memory: vddimmer-rev-b-state.md (new), vddimmer-connector-polarity-trap.md
   (new), kicad10-copper-layer-order.md (new), plus appends to kicad-drc-hides-split-nets.md
   (never DRC a .kicad_pcb separated from its .kicad_pro — it invents violations) and
   konnect-pcb-workflow-traps.md #16 (the F8 dialog's "Replace footprints" is OFF by
   default). Konnect traps #11/#14/#16 already covered the board-text and multi-unit
   findings, so those were not duplicated.

2026-09-19 -- CONNECTOR TEST RESOLVED (checklist 4.1.1)
Molex 22-05-3021 fitted to a Rev A board (pin 1 = VIN_PROT). The keyway sits high, away
from the PCB. Firmware a9e0823b flashed; all four channels set to 127 over the serial
console; the pre-plugged lamp lit. So the lamp's + lands on pin 1, and Rev A's order is
correct with this header.
Ruling: J3-J6 revert to pin 1 = VIN_PROT, pin 2 = DRAINn. This is the exact reverse of
Task 4 (55af04c). Also: flip the eight +/- silk marks (GUI delete) and amend spec decision
9 and section 6 explicitly. NOT YET DONE. J7/J8 order (4.2) was not tested and is still open.

2026-09-23 -- SESSION END, state saved for a reboot
Enclosure (hw/VDDimmer/enclosure/, commits a36ef93 + b967772) PRINTED and FITS. Independent
of Rev B; regenerate for Rev B by re-running extract_board.py against the new board.
STILL OUTSTANDING on Rev B, unchanged: the KiCad GUI pass -- F8 (Update PCB from Schematic,
"Replace footprints" OFF), then tools/revb_silk_pass.py in the scripting console, then save.
Until F8 runs, J3-J6 pads on the PCB still carry the OLD nets while the schematic carries the
reverted ones. Routing not started. J7/J8 pin order (checklist 4.2) still untested.
