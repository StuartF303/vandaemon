# Resume prompt — VANDIMMER Rev B

**Last session ended:** 2026-09-17 · **Tip:** `219bc50` · **Branch:** `vddimmer-rev-b`
**State:** Phases 1–4 complete. Stopped at the Task 13 placement checkpoint, awaiting
visual approval. **No routing has started.**

Paste the block below into a fresh session.

---

## PROMPT

```
Resume the VANDIMMER-4CH+2A Rev B board rework. Work as autonomously as you can and
only stop where I say.

REPO / BRANCH
  C:\Projects\vandaemon, branch `vddimmer-rev-b` (exists, do not re-create).
  Tip 219bc50, 16 commits ahead of origin/vddimmer-rev-b, tree clean.
  Commit after every task.

READ THESE FIRST, IN THIS ORDER
  1. hw/VDDimmer/REV-B-DESIGN-2026-09-12.md   <- the spec, binding authority
  2. hw/VDDimmer/REV-B-CHECKLIST.md           <- ***START HERE***: current state,
     every gate with what the tool ACTUALLY checks, 14 tool traps, open decisions,
     outstanding board additions. Section 0 is the resume summary.
  3. hw/VDDimmer/REV-B-PLAN-2026-09-12.md     <- the 16-task plan
  4. .superpowers/sdd/REV-B-PLAN-2026-09-12/progress.md  <- ledger: every ruling made.
     Trust it over your own reconstruction.

SKILLS TO INVOKE
  - `konnect` — MANDATORY before touching any KiCad file. Non-negotiable.
  - `superpowers:subagent-driven-development` — the execution process.
  - `vddimmer-firmware` — only if firmware work comes up.

WHERE I AM
  Tasks 1,2,3,4,6,7,8,9,10,11,12,5 COMPLETE. All 98 components placed.
  Task 13 rendered the placement and STOPPED for my approval. Renders are in
  hw/VDDimmer/renders/ (rev-b-placement-3d.png, rev-b-placement-outline.svg,
  rev-b-placement-top.svg).

  Placement gates PASS: fpspace.py --courtyards 0 overlaps exit 0; hot loop
  2.51 mm2 (gate <15, identical to Rev A); only U1's courtyard outside
  Edge.Cuts, by design.

  DRC's ~1037 violations and ~193 unconnected items are Rev A's copper still
  lying where the OLD placement put it while every component moved. Expected
  pre-routing state, NOT a regression. Phase 5 rips it up.

DO THESE FIRST — they were blocked on the Mouser delivery
  1. DECIDED 2026-09-19 (REV-B-CHECKLIST.md 4.1.1): Molex 22-05-3021, keyway
     high off the board, fitted to a Rev A board -> the pre-plugged lamp LIT.
     J3-J6 pin order REVERTS to Rev A's (pin 1 = VIN_PROT / V+). Schematic,
     new 1.5 mm silk marks, REV B mark and spec amendments are DONE (see
     checklist 4.1.1). STILL TO DO: the GUI pass there, which is F8, then
     tools/revb_silk_pass.py in the scripting console, then save. It deletes
     the old marks and "Rev A" and moves the title, which also clears both
     silk defects below.
  2. Same test on a 22-05-3041 with a tri-colour strip -> answers the J7/J8
     wire order (checklist 4.2). There V+ and GND sit at OPPOSITE ENDS of the
     4-way, so an end-for-end flip destroys a WS2815 rather than merely
     failing to light.
  3. Author the 2.54 mm horizontal footprints in the VANDIMMER library, for
     1x02 and 1x04. KiCad ships KK-254 in VERTICAL ONLY. Deliberately not
     started: the part had to be test-mated first.

TWO SILK DEFECTS THE RENDER FOUND — both need a GUI pass, Konnect cannot
edit or delete board text
  1. ** The board is silkscreened "Rev A" ** at (164.31..168.79, 99.75..101.45).
     Not a missing mark — an actively WRONG one, on a board whose J3-J6
     polarity is reversed from the Rev A it claims to be. Spec 6 records that
     grounding Rev A's pin 1 is a dead short across the supply. This is the
     single most important silk fix before fab.
  2. Title block "VANDIMMER-4CH+2A / v 1.0" at (103.92..123.08, 74.02..77.98)
     now sits on top of the relocated buck cluster.

  Replacement areas already VERIFIED clear of every courtyard and all
  footprint silk:
     title  -> x 102.3..122.3, y 95.8..101.8  (centre 112.3, 98.8)
     REV B  -> x 145..163, y 79..92           (large, ~3 mm, centre ~154, 85.5)

THEN, after I approve the placement: Tasks 14-16 (routing). Plan is in
REV-B-CHECKLIST.md section 6.

HARD RULES
  - NEVER text-edit .kicad_sch / .kicad_pcb / .kicad_pro / .kicad_sym /
    .kicad_mod / fp-lib-table / sym-lib-table. Konnect MCP only. `.net` is an
    export and may be regenerated with kicad-cli.
  - VERIFY BY READ-BACK after every write. A Konnect success response is not
    evidence — it has lied twice on this project.
  - Konnect MCP tools are NOT reachable from subagents. load_toolset succeeds
    server-side while the tools stay uncallable in a child agent. Do ALL
    Konnect work in your own session; subagents can still do shell, file,
    verification and reporting work.
  - KiCad CLOSED for file ops, zone inserts, kicad-cli --save-board and
    renders. KiCad RUNNING for placement and routing (IPC). Never both.
    TELL ME when to open and close it; I do that by hand.
  - THE DISK IS STALE whenever KiCad holds the board with unsaved edits, and
    every tool in tools/ reads disk. save_project over IPC first, or you will
    analyse the previous state and never know.
  - After any KiCad save, re-check anything edited on disk. A session once
    wrote back its in-memory copy and silently reverted an edit, invisible to
    git diff.
  - Python for hw/VDDimmer/tools/ is KiCad's:
    "C:/Program Files/KiCad/10.0/bin/python.exe". System Python is 3.6 and fails.
  - Use the PowerShell tool for KiCad tooling, not Bash. Git Bash is MSYS and
    KiCad's Python refuses it.
  - Board <= 100 x 100 mm; currently 100 x 84. If more room is needed, grow the
    TOP edge (y 56 -> 40) so every bottom-anchored item stays put.

STYLE
  Keep going without checking in between tasks. Make rulings rather than
  stalling, and record each in the ledger with what it costs if wrong. Stop
  only for: an irreversible operation, anything security-sensitive, a push to a
  shared branch, a plan defect with no sensible path forward — or a GUI action
  only I can perform.
```

---

## Rulings carried (do not re-litigate)

1. **Konnect MCP is unreachable from subagents** — all Konnect work in the controller session.
2. **`snapshot_project` never writes the `pcb_snapshot` it reports** (3×). Git history is the
   only PCB rollback.
3. **`connect.py`'s 12 split nets are CORRECT, not defects.** 3 real (`J11.1`,
   `J9.1`+`J10.1`, `D9.1`), 5 DPAK centre leads, 2 D8 bonded pairs, 1 U3 SOT-223 tab, 1 VBUS
   fragmentation. Only the first three are Rev B's job.
4. **Branch, not a worktree** — KiCad is opened on the working path.
5. **Task 5 runs after Task 12**, not inside Phase 2 (done).
6. **Mounting holes are M2.5** (2.7 mm hole, 5.90 mm keepout). H1–H4 footprints changed.
7. **Board stays 100 × 84**; grow the top edge if needed, never the bottom.
8. **U1's courtyard (41.25 × 48.05 mm) encloses the antenna keepout**, blocking x 180–200,
   y 61.95–110.05. `courtyards_overlap` is an **error** in this project, so that strip is
   unusable for components. The library courtyard was deliberately **not** trimmed.
9. **Buck cluster was translated rigidly**, preserving Rev A's 2.51 mm² hot loop by
   construction.
10. **Three plan gates do not do what their names suggest** — `crtyd.py` never tests
    `Edge.Cuts`; `silkspace.py` is a clear-area finder, not a violation checker;
    `measure.py` §2–4 measure existing copper. Substitutes recorded in the checklist.
