# Resume prompt — VANDIMMER Rev B board rework

Paste the block below into a fresh Claude Code session started in `C:\Projects\vandaemon`.
It is self-contained: it names the plan, the state, and every ruling already made, so the
new session does not rediscover them.

---

```
Resume the VANDIMMER-4CH+2A Rev B board rework. Work as autonomously as you can and
only stop where I say.

REPO / BRANCH
  C:\Projects\vandaemon, branch `vddimmer-rev-b` (already created, do not re-create).
  Working tree should be clean. Commit after every task.

READ THESE FIRST, IN THIS ORDER
  1. hw/VDDimmer/REV-B-DESIGN-2026-09-12.md   <- the spec, binding authority
  2. hw/VDDimmer/REV-B-PLAN-2026-09-12.md     <- the 16-task plan
  3. .superpowers/sdd/REV-B-PLAN-2026-09-12/progress.md  <- ledger: what is done, and
     every ruling already made. Trust it over your own reconstruction.

SKILLS TO INVOKE
  - `konnect` — MANDATORY before touching any KiCad file. Non-negotiable.
  - `superpowers:subagent-driven-development` — the execution process, with the
    adaptation noted under RULINGS below.
  - `vddimmer-firmware` — only if firmware work comes up.

WHERE I AM
  Task 1 complete (commit f013c46) — Rev A baseline recorded.
  Task 2 complete (commit 6baafec) — U1 swapped to the on-board-antenna module.
  START AT TASK 3 (polyfuses PF1/PF2, 2 A -> 3 A).

REVISED TASK ORDER (Task 5 was moved, see rulings)
  3, 4, 6, 7, 8, 9, 10, 11, 12, 5, 13  -> THEN STOP

HARD STOP
  Task 13 renders the placement. STOP THERE and show me
  renders/rev-b-placement-top.svg and the 3D render. Do NOT start routing
  (Tasks 14-16) until I have approved the layout visually. Placement is the
  expensive thing to undo.

RULINGS ALREADY MADE — carry these, do not re-litigate
  1. Konnect MCP tools are NOT reachable from subagents. `load_toolset` succeeds
     server-side but the tools stay uncallable in a child agent. Do ALL Konnect work in
     your own session: call `load_toolset(...)`, then `ToolSearch` with
     "select:mcp__konnect__<tool>" to load the schema, then call it. Subagents can still
     do shell, file, verification and reporting work.
  2. `snapshot_project` writes the schematic PDF but NOT the pcb_snapshot it reports
     (reproduced 3x). Git history of the .kicad_pcb is the only PCB rollback.
  3. connect.py's baseline is 12 split nets and that is CORRECT, not a defect:
     3 real (J11.1 / J9.1+J10.1 / D9.1), 5 DPAK centre leads (DRAIN1-4, VIN_FUSED),
     2 D8 bonded pin pairs (USB_DM, USB_DP), 1 U3 SOT-223 tab (+3V3), 1 VBUS
     fragmentation from the deliberate no-USB-power decision. Only the first three are
     Rev B's job. Do not chase the others.
  4. Branch, not a git worktree — I open KiCad on the working path.
  5. Task 5 (J3-J6 silkscreen) moved to run after Task 12: its own text says "KiCad
     running" but Phase 2 is headed "KiCad closed".
  6. Mounting holes are M2.5 (2.7 mm hole, ~5.9 mm keepout). H1/H2/H3 currently use
     MountingHole:MountingHole_3.2mm_M3 and those footprints must change too — the plan
     does not spell this out.

HARD RULES
  - NEVER text-edit .kicad_sch / .kicad_pcb / .kicad_pro / .kicad_sym / .kicad_mod /
    fp-lib-table / sym-lib-table. Konnect MCP only. `.net` is an export and may be
    regenerated with kicad-cli.
  - VERIFY BY READ-BACK after every write. A Konnect success response is not evidence —
    it has lied twice on this project.
  - KiCad CLOSED for Tasks 3,4,6,7,8 and 13. KiCad RUNNING for Tasks 9,10,11,12,5.
    Never both. Tell me when to open and close it; I do that by hand.
  - After any KiCad save, re-check anything edited on disk. A KiCad session once wrote
    back its in-memory copy and silently reverted an on-disk edit, invisible to git diff.
  - Python for hw/VDDimmer/tools/ is KiCad's: "C:/Program Files/KiCad/10.0/bin/python.exe".
    The system Python is 3.6 and will fail.
  - Use the PowerShell tool for KiCad tooling, not Bash. Git Bash is MSYS and KiCad's
    Python refuses it.
  - Board <= 100 x 100 mm. Do not shrink below what the layout wants — within that
    bracket the PCB price is flat.

VERIFICATION GATES (DRC alone is NOT sufficient)
  - fpspace.py --courtyards after ANY footprint move
  - route.py before pushing traces
  - connect.py after the final zone refill — every net one connected group, package
    internal splits excepted. KiCad reports a split net as an unconnected ITEM, never an
    error; that is exactly how Rev A shipped three dead pads.
  - drc.sh, measure.py (hot loop < 15 mm2), emc.py

STYLE
  Keep going without checking in between tasks. Make rulings rather than stalling, and
  record each one in the ledger with what it costs if wrong. Stop only for: an
  irreversible operation, anything security-sensitive, a push to a shared branch, or a
  plan defect with no sensible path forward — plus the Task 13 checkpoint above.
```
