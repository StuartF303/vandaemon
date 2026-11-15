### Task 4: Swap the J3–J6 pin order

**Files:**
- Modify: `PWM.kicad_sch`

**Interfaces:**
- Consumes: nothing.
- Produces: for each of J3–J6, **pin 1 = `DRAINn`**, **pin 2 = `VIN_PROT`** — the reverse
  of Rev A.

- [ ] **Step 1: Record the current connections**

```bash
cd /c/Projects/vandaemon/hw/VDDimmer
"C:/Program Files/KiCad/10.0/bin/kicad-cli.exe" sch export netlist \
  --format kicadsexpr -o /tmp/before.net VANDIMMER-4CH2A.kicad_sch
grep -A4 -B4 '"J3"' /tmp/before.net | head -20
```

Expected: J3 pin 1 on `VIN_PROT`, pin 2 on `/PWM/DRAIN1`.

- [ ] **Step 2: Swap the wiring for all four connectors**

`load_toolset("sch_wiring")`. For each of J3/J4/J5/J6, disconnect the two pins and
reconnect so pin 1 carries `DRAINn` and pin 2 carries `VIN_PROT`.

- [ ] **Step 3: Verify by netlist, not by eye**

```bash
"C:/Program Files/KiCad/10.0/bin/kicad-cli.exe" sch export netlist \
  --format kicadsexpr -o /tmp/after.net VANDIMMER-4CH2A.kicad_sch
"C:/Program Files/KiCad/10.0/bin/python.exe" - <<'PY'
import io,re
s=io.open('/tmp/after.net',encoding='utf-8',errors='replace').read()
nets=s[s.index('(nets'):]
conn={}
for b in re.split(r'\n\t\t\(net\b',nets)[1:]:
    m=re.search(r'\(name "([^"]*)"\)',b)
    if not m: continue
    for r,p in re.findall(r'\(ref "(J[3-6])"\)\s*\n\s*\(pin "([^"]+)"\)',b):
        conn.setdefault(r,{})[p]=m.group(1)
for r in sorted(conn):
    print(r, {k:conn[r][k] for k in sorted(conn[r])})
PY
```

Expected, for every one of J3–J6: `{'1': '/PWM/DRAINn', '2': 'VIN_PROT'}`.
If any still shows `VIN_PROT` on pin 1, the swap did not land — fix before continuing.

- [ ] **Step 4: Commit**

```bash
git add PWM.kicad_sch
git commit -m "Swap J3-J6 pin order: pin 1 is now the LED- return"
```

