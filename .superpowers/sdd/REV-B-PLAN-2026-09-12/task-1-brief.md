### Task 1: Branch, snapshot, and record the baseline

**Files:**
- Create: `analysis/rev-a-baseline.txt`

**Interfaces:**
- Produces: a committed baseline every later task compares against.

- [ ] **Step 1: Create the branch**

```bash
cd /c/Projects/vandaemon
git checkout -b vddimmer-rev-b
```

- [ ] **Step 2: Snapshot the KiCad project through Konnect**

Call `load_toolset("project")` then `snapshot_project` on
`C:\Projects\vandaemon\hw\VDDimmer\VANDIMMER-4CH2A.kicad_pro`.
This is the rollback point if a Konnect write goes wrong.

- [ ] **Step 3: Record the Rev A baseline measurements**

With KiCad **closed**:

```bash
cd /c/Projects/vandaemon/hw/VDDimmer
PY="C:/Program Files/KiCad/10.0/bin/python.exe"
{ echo "=== connect.py ==="; "$PY" tools/connect.py
  echo "=== measure.py ==="; "$PY" tools/measure.py
  echo "=== emc.py ===";     "$PY" tools/emc.py
} > analysis/rev-a-baseline.txt 2>&1
tail -40 analysis/rev-a-baseline.txt
```

Expected: `connect.py` reports the three known split nets (`J11.1`, `J9.1`/`J10.1`,
`D9.1`); `emc.py` reports 42 of 60 nets crossing a pour gap.

- [ ] **Step 4: Commit**

```bash
git add analysis/rev-a-baseline.txt
git commit -m "Record the Rev A baseline before the Rev B rework"
```

---

# PHASE 2 — Schematic

All of Phase 2 runs with **KiCad closed** — the schematic engine is file-based.

