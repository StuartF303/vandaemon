# Task 1 Report: Branch, snapshot, and record the baseline

## Status: DONE_WITH_CONCERNS

## Step 1: Branch

Already done before this task started, per the controller's note. Confirmed on
entry:

```
$ git branch --show-current
vddimmer-rev-b
$ git log --oneline -1
4e42970 Clear the stale blocker note now M2.5 is settled
```

No branch was created by me.

## Step 2: Konnect snapshot

`list_toolboxes` confirmed the `project` toolset was already loaded (3 toolsets
active: project, config, manufacturing). No `load_toolset("project")` call was
needed since it was already active; calling it again would have been a no-op.

The brief says to call `snapshot_project` "on
`C:\Projects\vandaemon\hw\VDDimmer\VANDIMMER-4CH2A.kicad_pro`", but the actual
tool signature (fetched fresh from the MCP server) takes `schematic` (a
`.kicad_sch` path) and `output_dir`, with an optional `pcb` path — it does not
take a `.kicad_pro` path at all. I passed the project's top-level schematic and
PCB:

- `schematic`: `C:\Projects\vandaemon\hw\VDDimmer\VANDIMMER-4CH2A.kicad_sch`
- `pcb`: `C:\Projects\vandaemon\hw\VDDimmer\VANDIMMER-4CH2A.kicad_pcb`
- `output_dir`: `C:\Projects\vandaemon\.superpowers\sdd\REV-B-PLAN-2026-09-12\snapshots`
  (this directory is covered by `.superpowers/sdd/.gitignore` — the whole
  `snapshots/` subtree there is already ignored, so nothing needed changing
  to keep it out of git)
- `label`: `rev-a-baseline`

Call result:
```json
{
  "label": "rev-a-baseline",
  "pcb_snapshot": "C:\\Projects\\vandaemon\\.superpowers\\sdd\\REV-B-PLAN-2026-09-12\\snapshots\\VANDIMMER-4CH2A_pcb_rev-a-baseline_1789226534.pdf",
  "snapshot": "C:\\Projects\\vandaemon\\.superpowers\\sdd\\REV-B-PLAN-2026-09-12\\snapshots\\VANDIMMER-4CH2A_rev-a-baseline_1789226534.pdf",
  "timestamp": 1789226534
}
```

**Concern — verified by read-back, per the Konnect skill's own warning:** the
schematic PDF (`VANDIMMER-4CH2A_rev-a-baseline_1789226534.pdf`, 231511 bytes)
was actually written and is present on disk. The `pcb_snapshot` path in the
response was **not** written anywhere — I confirmed with a directory listing
and a repo-wide `find` for the filename. I reproduced this twice more (once
with an identical call under a different label, once with all paths given as
forward slashes instead of backslashes, in case of a path-quoting bug) — both
retries reported a `pcb_snapshot` path in the JSON response and neither one
materialized on disk. This is the "tool's return value is not evidence" trap
the Konnect skill warns about, applied to `snapshot_project`'s PCB half
specifically. I deleted the two throwaway retry PDFs, keeping only the
original schematic snapshot as the checkpoint.

**Net effect:** the schematic rollback checkpoint exists
(`.superpowers/sdd/REV-B-PLAN-2026-09-12/snapshots/VANDIMMER-4CH2A_rev-a-baseline_1789226534.pdf`,
not committed — the directory is gitignored, consistent with it being a
throwaway visual checkpoint rather than a tracked artifact). There is **no
working PDF rollback checkpoint for the PCB side**. Since Phase 2 (schematic)
runs before the PCB rework phases, this doesn't block Task 1 or Phase 2, but
whoever executes the PCB-touching tasks (T7 onward) should know the
`snapshot_project` PCB export is currently non-functional in this Konnect
build and get a rollback point some other way before those edits (e.g. relying
on git history of the `.kicad_pcb` file itself, which is always available
regardless of this tool).

## Step 3: Rev A baseline measurements

KiCad was confirmed already closed (per the controller). Ran from PowerShell,
working directory `C:\Projects\vandaemon\hw\VDDimmer`, using KiCad's own
Python (`C:\Program Files\KiCad\10.0\bin\python.exe`) against
`tools/connect.py`, `tools/measure.py`, `tools/emc.py`, output captured to
`analysis/rev-a-baseline.txt`. No exceptions or tracebacks in the output.

### connect.py

Reported `61 nets checked, 12 split into more than one group`. The three nets
named in the brief's expectation are all present among the twelve:

- `/MCU/BTN1` — g0 (R43.2, U1.33) / g1 (`J11.1` isolated) ✓
- `VIN_PROT` — g0 (bulk) / g1 (`J10.1` isolated) / g2 (`J9.1` isolated) ✓
- `+5V` — g0 (bulk) / g1 (`D9.1` isolated) / plus 3 empty zero-area zone
  groups ✓

**Concern:** the brief's expected result names these three as "the three known
split nets" but the actual run found nine more: `+3V3`, `/MCU/USB_DM`,
`/MCU/USB_DP`, `/MCU/VBUS`, `/PWM/DRAIN1`–`DRAIN4`, and
`/Power/VIN_FUSED`. This isn't a tool failure (no error, sane groupings, sane
pad names) — it's just more findings than the plan document anticipated. Several
of these look like a specific, repeatable artifact: `/PWM/DRAIN1..4` and
`/Power/VIN_FUSED` each split into a bulk group plus a **single-item group
containing the same pad already listed in the bulk group** (e.g. `/PWM/DRAIN1`
g0 lists `Q1.2` among its members, and g1 is just `Q1.2` again). That pattern
recurs identically four times (DRAIN1–4) plus once more on VIN_FUSED/Q5.2,
which reads like a footprint with a duplicated/stacked pad rather than five
independent design defects, but I did not investigate further since that is
diagnosis, not baseline-recording. Recommend whoever picks up the connectivity
rework tasks (T16 per the plan's own reference to `connect.py`) treats the
full 12 as the real baseline, not just the three named in the brief, and
verifies whether the DRAIN/VIN_FUSED pattern is a real short vs. a duplicate-pad
artifact.

Also reported: 7 "MARGINAL contacts" (copper overlap < 0.05 mm) — see the
committed file for the full list, none were called out as expected/unexpected
in the brief so I'm flagging them here for visibility only.

### measure.py

Ran clean, four sections reported (Cin hot loop, SW node clearance, FB divider
distances, non-pour B.Cu copper, zone summary). Cin hot loop area 2.51 mm²
(gate: < 15, passes) but 1 via found inside/touching the loop bbox (gate: 0,
does **not** pass) — this isn't something the brief asked me to evaluate pass/
fail on, just recording it since it's part of the baseline. Full output is in
the committed file.

### emc.py

- B.Cu GND pour: 1 island, 7179.8 mm².
- **F.Cu nets crossing a gap: 43 of 60**, not the 42 of 60 the brief states as
  expected. Off by one net. I did not see anything indicating a tooling
  problem (no traceback, the list of offending nets looks sane and
  measurement-shaped, e.g. `/PWM/DRAIN1` at 6.50 mm), so I'm treating this as
  a real, reproducible measurement rather than a broken invocation — but it
  doesn't match the brief's stated expectation and I'm flagging it as
  a discrepancy rather than silently "correcting" the brief's number or
  editing the output.
- 46 non-GND vias, 31 without a GND return via within 1.0 mm.
- B.Cu GND under the buck (U2 footprint + 5mm): 100% coverage, no foreign
  copper.

Full output for all three tools is in `hw/VDDimmer/analysis/rev-a-baseline.txt`
(committed).

## Step 4: Commit

`hw/VDDimmer/analysis/.gitignore` ignores everything in the `analysis/`
directory except `.gitignore` and `manifest.json` (comment: "Analysis output --
regenerable from source files"). `analysis/rev-a-baseline.txt` fell under that
`*` ignore rule, so `git add` would have silently done nothing — this would
have made Step 4 of the brief impossible to satisfy as written without a
change. This is a real conflict between the brief's committed-baseline goal
and the existing ignore rule (the baseline is explicitly *not* regenerable
disposable output — it's the fixed reference point every later task diffs
against), so I added one line to the `.gitignore`:

```
# Rev A -> Rev B rework: committed baseline every later task compares against
!rev-a-baseline.txt
```

Then staged and committed both files:

```
$ git add hw/VDDimmer/analysis/rev-a-baseline.txt hw/VDDimmer/analysis/.gitignore
$ git commit -m "Record the Rev A baseline before the Rev B rework ..."
[vddimmer-rev-b f013c46] Record the Rev A baseline before the Rev B rework
 2 files changed, 162 insertions(+)
 create mode 100644 hw/VDDimmer/analysis/rev-a-baseline.txt
```

Working tree is clean after the commit. Commit SHA: `f013c46`.

## Summary of concerns for the reviewer

1. `snapshot_project`'s PCB export path is claimed in the tool response but
   never written to disk (reproduced 3x). Schematic export works. No PCB PDF
   rollback checkpoint exists from this tool; git history of the `.kicad_pcb`
   remains the fallback.
2. `connect.py` found 12 split nets, not the 3 named in the brief. The 3 named
   ones are all present; the other 9 are new information, including a
   suspicious repeated pattern on DRAIN1–4/VIN_FUSED that looks like a
   duplicate-pad artifact and may be worth a second look before later tasks
   treat it as a real defect.
3. `emc.py` reports 43/60 nets crossing a pour gap, not 42/60 as expected —
   off by one, no apparent tooling fault, flagged rather than adjusted.
4. `analysis/.gitignore` needed a one-line exception to allow the baseline
   file to be committed at all; done as part of this task's commit rather than
   raised as a blocker, since it's a minimal, obviously-scoped fix directly
   required by the brief's own Step 4.
