# How to Import v2.0 Design into KiCad

I've created 3 simple files you can use to recreate the v2.0 schematic in KiCad:

## Files Created:

1. **`led_dimmer_v2.net`** - Netlist file (components + connections)
2. **`v2_component_list.csv`** - Component list for reference
3. **`v2_wiring_guide.txt`** - Complete wiring instructions

## Option 1: Import Netlist (Semi-Automatic)

### Step 1: Try Netlist Import
1. Open KiCad → **Schematic Editor**
2. Go to **Tools** → **Update PCB from Schematic** → **Import Netlist**
3. Browse to `led_dimmer_v2.net`
4. Click **Update**

⚠️ **Note**: This might not work perfectly because netlist import is designed for PCB updates, not schematic creation.

## Option 2: Manual Creation (Recommended)

This is more reliable and gives you full control.

### Step 1: Open Original Schematic
1. Open `hw/LEDDimmer/led_dimmer_8ch.kicad_sch` in KiCad

### Step 2: Save As New File
1. **File** → **Save As** → `led_dimmer_8ch_v2.kicad_sch`

### Step 3: Modify Components

**A. Delete Old Button Connectors:**
- Select and delete **J6** (button connector 1)
- Select and delete **J7** (button connector 2)

**B. Add Tactile Switches:**
1. Press **A** (Add Symbol)
2. Search for `SW_Push`
3. Place as **SW1** near bottom center
4. Add another as **SW2**
5. Connect:
   - SW1 → GPIO32 (keep existing R17 pull-up)
   - SW2 → GPIO33 (keep existing R18 pull-up)
   - Both switch other pins → GND

**C. Rearrange MOSFETs (Optional but recommended):**

**Left Bank (Q1-Q4):**
- Move Q1, Q2, Q3, Q4 to left side of schematic
- Arrange vertically with spacing
- Keep all associated components (R1-R4, R9-R12, J2-J5) with them

**Right Bank (Q5-Q8):**
- Move Q5, Q6, Q7, Q8 to right side of schematic
- Mirror the left bank arrangement
- Keep all associated components (R5-R8, R13-R16, J8-J11) with them

### Step 4: Update Labels

1. Add text labels:
   - "LEFT BANK - CHANNELS 1-4" (left side)
   - "RIGHT BANK - CHANNELS 5-8" (right side)

2. Update J1 value:
   - Change "PWR_IN" to "PWR_12-24V"

### Step 5: Run ERC (Electrical Rules Check)
1. **Inspect** → **Electrical Rules Checker**
2. Click **Run ERC**
3. Fix any errors (there shouldn't be any if you followed the wiring guide)

### Step 6: Save
1. **File** → **Save**
2. **File** → **Export** → **Netlist** (for PCB design later)

## Option 3: Quick Reference Method

Use the files as a **checklist** while manually creating the schematic:

1. Open `v2_component_list.csv` - lists all components
2. Open `v2_wiring_guide.txt` - shows all connections
3. Manually place and wire components in KiCad

This takes ~30-45 minutes but ensures everything is perfect.

## Verification Checklist

After creating the schematic, verify:

- [ ] All 8 MOSFETs present (Q1-Q8)
- [ ] All 18 resistors present (R1-R18)
- [ ] 3 capacitors (C1-C3)
- [ ] 2 tactile switches (SW1-SW2) - **NOT** connectors
- [ ] 9 screw terminals (J1-J5, J8-J11)
- [ ] ESP32 module (U1)
- [ ] Voltage regulator (U2)
- [ ] WS2812B LED (D1)
- [ ] VIN connected to all MOSFET drains and LED+ terminals
- [ ] GND properly distributed
- [ ] 3V3 connected to ESP32 VCC
- [ ] All 8 GPIO pins connected to gate resistors
- [ ] No unconnected pins (except ESP32 unused GPIOs)

## Tips

**For Left/Right Bank Layout:**
```
LEFT SIDE           CENTER              RIGHT SIDE
═════════           ══════              ══════════

Q1 ─ R1,R9         ESP32                R5,R13 ─ Q5
Q2 ─ R2,R10        U1                   R6,R14 ─ Q6
Q3 ─ R3,R11        +                    R7,R15 ─ Q7
Q4 ─ R4,R12        AMS1117              R8,R16 ─ Q8
                   U2
J2-J5                                   J8-J11
(outputs)          SW1  SW2             (outputs)
                   D1 LED
```

**Grid Alignment:**
- Use 50 mil grid for major components
- Use 25 mil grid for fine adjustments
- Align components on grid for clean look

**Wire Routing:**
- Use buses for VIN and GND where possible
- Label net names for clarity
- Avoid crossing wires when possible
- Use net labels instead of long wires

## Need Help?

All the design documentation is still valid:
- `HARDWARE_V2_DESIGN.md` - Full circuit specifications
- `PCB_LAYOUT_GUIDE.md` - PCB design after schematic is done
- `enclosure/ENCLOSURE_DESIGN.md` - 3D printable case
- `VISUAL_GUIDE.md` - Diagrams and illustrations

The netlist and wiring guide guarantee electrically correct connections!
