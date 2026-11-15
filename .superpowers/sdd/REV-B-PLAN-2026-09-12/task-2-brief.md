### Task 2: Swap U1 to the on-board-antenna module

**Files:**
- Modify: `MCU.kicad_sch` (U1 symbol), `bom-lcsc.csv`

**Interfaces:**
- Consumes: nothing.
- Produces: U1 with footprint `RF_Module:ESP32-S3-WROOM-1` and LCSC `C2913202`. Pin
  numbering is unchanged — the `-1` and `-1U` share a pad layout, so every net keeps its
  pin.

- [ ] **Step 1: Confirm the symbol and footprint exist**

```bash
ls "C:/Program Files/KiCad/10.0/share/kicad/symbols/RF_Module.kicad_sym"
grep -c "ESP32-S3-WROOM-1\b" "C:/Program Files/KiCad/10.0/share/kicad/symbols/RF_Module.kicad_sym"
ls "C:/Program Files/KiCad/10.0/share/kicad/footprints/RF_Module.pretty/" | grep "ESP32-S3-WROOM-1"
```

Expected: both `ESP32-S3-WROOM-1` and `ESP32-S3-WROOM-1U` present. If the `-1` footprint is
absent, stop and report — do not substitute.

- [ ] **Step 2: Change the symbol and footprint**

`load_toolset("sch_components")`, then `edit_schematic_component` on U1:
- value → `ESP32-S3-WROOM-1-N16R8`
- footprint → `RF_Module:ESP32-S3-WROOM-1`
- LCSC field → `C2913202`

- [ ] **Step 3: Read it back**

```bash
cd /c/Projects/vandaemon/hw/VDDimmer
grep -A6 '"U1"' MCU.kicad_sch | grep -i "value\|footprint\|C29\|C298"
```

Expected: value `ESP32-S3-WROOM-1-N16R8`, footprint `RF_Module:ESP32-S3-WROOM-1`,
no remaining `C2980298`.

- [ ] **Step 4: Update the BOM**

Edit `bom-lcsc.csv` — replace the `ESP32-S3-WROOM-1U-N16` / `C2980298` row with
`ESP32-S3-WROOM-1-N16R8` / `C2913202`. This is a plain CSV, safe to edit directly.

- [ ] **Step 5: Commit**

```bash
git add MCU.kicad_sch bom-lcsc.csv
git commit -m "Swap U1 to the on-board-antenna module; -1 and -1U share a pad layout"
```

