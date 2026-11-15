### Task 3: Re-rate PF1/PF2 from 2 A to 3 A

**Files:**
- Modify: `Addressable.kicad_sch`, `bom-lcsc.csv`

**Interfaces:**
- Consumes: nothing.
- Produces: PF1/PF2 at 3 A hold, 30 V, 1812.

- [ ] **Step 1: Find a 3 A / 30 V 1812 polyfuse in stock**

Use `mcp__pcbparts__jlc_search` with query `polyfuse PPTC 3A 30V 1812`. Record the LCSC
code and confirm stock ≥ 100. If nothing at 30 V exists in 1812, report the closest and
stop — do **not** drop the voltage rating, these sit downstream of the jumper and may see
`VIN_PROT`.

- [ ] **Step 2: Edit both parts**

`load_toolset("sch_components")`, `edit_schematic_component` on PF1 and PF2: value → `3A`,
LCSC field → the code from Step 1. Footprint stays 1812.

- [ ] **Step 3: Read it back**

```bash
grep -B2 -A8 '"PF1"' Addressable.kicad_sch | grep -i "value\|lcsc\|footprint"
grep -B2 -A8 '"PF2"' Addressable.kicad_sch | grep -i "value\|lcsc\|footprint"
```

Expected: both show `3A` and the new LCSC code.

- [ ] **Step 4: Commit**

```bash
git add Addressable.kicad_sch bom-lcsc.csv
git commit -m "Re-rate PF1/PF2 to 3 A; 1.7 A against a 2 A hold nuisance-trips warm"
```

