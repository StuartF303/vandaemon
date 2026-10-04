# ARCHIVED — VANDIMMER-8CH (the 8-channel LED dimmer)

Superseded by **`hw/VDDimmer`** (VANDIMMER-4CH+2A), which was ordered from JLCPCB on
2026-09-03 and is the board under active development. Nothing here is being worked on.

Moved to `hw/archive/` on 2026-09-10 and committed for the first time — it had sat
untracked in the working tree until then.

## Watch out: the paths in the older docs are wrong

This directory was called `hw/LEDDimmer` in documentation written before it was renamed to
`hw/LEDDimmer-8ch`, and it has now moved again. **`hw/LEDDimmer` has never existed as a
tracked path in this repo.** Anything referring to it means this directory. Known stale
references remain in `.claude/agents/documentation-writer.md`,
`.claude/agents/security-engineer.md` and `.claude/skills/kicad/references/workflows.md`.

## What is committed, and what is not

Committed: the KiCad schematic and PCB, the Arduino firmware, the design and layout guides,
the enclosure design, and the four `VanDaemon-8way_v*.mkc` CAM projects — so the board and
the CNC isolation-milling job can both be reproduced.

Not committed (see `.gitignore`, all still present on disk): `pcb.zip`, `gerber.zip`,
`.archive/archive.zip`, the 4 MB KiCad `fp-info-cache`, the 5 MB of `*-backups` auto-save
folders, and the generated gerber and `.nc` G-code output. That came to 22 MB of derived
files against 8 MB of sources, in a repo whose largest tracked file is otherwise 1.4 MB.

## If you ever build firmware here

The `platformio` skill's raw `pio run -t upload` is **unsafe on this machine**: `os.spawnve`
is broken OS-wide (Windows 11 build 26200), so a build can fail while reporting success and
a subsequent upload will flash a stale image without saying so. Port
`hw/VDDimmer/firmware/tools/scons_spawn_fix.py` across first, and read the
`vddimmer-firmware` skill.
