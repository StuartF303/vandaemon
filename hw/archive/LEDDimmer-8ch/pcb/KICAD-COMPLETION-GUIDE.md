# VANDIMMER-8CH KiCad Project Completion Guide

## Project Contents

```
VANDIMMER-8CH.kicad_pro   - Project file
VANDIMMER-8CH.kicad_sch   - Schematic (components placed, needs wiring)
VANDIMMER-8CH.kicad_pcb   - PCB layout (footprints placed, needs routing)
```

---

## Step 1: Open Project in KiCad

1. Launch KiCad 7.x or 8.x
2. **File → Open Project**
3. Select `VANDIMMER-8CH.kicad_pro`

---

## Step 2: Complete the Schematic

The schematic has all components placed but needs wire connections.

### 2.1 Open Schematic Editor
- Double-click the `.kicad_sch` file in the project tree
- Or click the schematic icon in the main KiCad window

### 2.2 Add Wires
Use **W** key to start drawing wires. Connect:

**Power Section:**
```
J1 pin 1 (VIN) → C4 pin 1 → U2 pin 3 (VI)
J1 pin 2 (GND) → C4 pin 2 → U2 pin 2 (GND)
C1 between VIN and GND (near U2 input)
U2 pin 1 (VO) → +3V3 power symbol
C2 between +3V3 and GND (near U2 output)
```

**ESP32 Power:**
```
+3V3 → U1 pin 2 (3V3)
GND → U1 pins 1, 15, 38, 39
```

**PWM Channels (repeat for each):**
```
ESP32 GPIO → R(100Ω) → MOSFET Gate
MOSFET Gate → R(10K) → GND
MOSFET Source → GND
MOSFET Drain → Terminal pin 2 (LED-)
Terminal pin 1 → VIN (LED+)
```

**GPIO Assignments:**
| Channel | GPIO | ESP32 Pin |
|---------|------|-----------|
| CH1 | GPIO25 | Pin 10 |
| CH2 | GPIO26 | Pin 11 |
| CH3 | GPIO27 | Pin 12 |
| CH4 | GPIO14 | Pin 13 |
| CH5 | GPIO4 | Pin 26 |
| CH6 | GPIO5 | Pin 29 |
| CH7 | GPIO18 | Pin 30 |
| CH8 | GPIO19 | Pin 31 |
| WS2812 | GPIO16 | Pin 27 |
| BTN1 | GPIO32 | Pin 8 |
| BTN2 | GPIO33 | Pin 9 |

**Status LED:**
```
+3V3 → D1 VDD (pin 1)
GND → D1 VSS (pin 3)
GPIO16 → D1 DIN (pin 4)
C3 (100nF) between VDD and VSS
```

**Buttons:**
```
+3V3 → R17 (10K) → BTN1 signal → J10 pin 1
J10 pin 2 → GND
+3V3 → R18 (10K) → BTN2 signal → J11 pin 1
J11 pin 2 → GND
```

### 2.3 Add Power Symbols
- Press **P** to add power symbols
- Add `GND`, `+3V3`, `VIN` where needed
- Use power flags on unconnected power nets

### 2.4 Run ERC (Electrical Rules Check)
- **Inspect → Electrical Rules Checker**
- Fix any errors (unconnected pins, power conflicts)
- Common fix: Add "no connect" flags to unused ESP32 pins

### 2.5 Annotate Schematic
- **Tools → Annotate Schematic**
- Click "Annotate" to assign reference designators

### 2.6 Assign Footprints
- **Tools → Assign Footprints**
- Verify all components have footprints assigned:

| Component | Footprint |
|-----------|-----------|
| U1 ESP32 | RF_Module:ESP32-WROOM-32 |
| U2 AMS1117 | Package_TO_SOT_SMD:SOT-223-3_TabPin2 |
| Q1-Q8 MOSFET | Package_TO_SOT_THT:TO-220-3_Vertical |
| D1 WS2812B | LED_SMD:LED_WS2812B_PLCC4_5.0x5.0mm_P3.2mm |
| R1-R18 | Resistor_SMD:R_0805_2012Metric |
| C1-C3 | Capacitor_SMD:C_0805_2012Metric |
| C4 | Capacitor_THT:CP_Radial_D8.0mm_P3.50mm |
| J1, J2-J9 | TerminalBlock:TerminalBlock_bornier-2_P5.08mm |
| J10, J11 | Connector_PinHeader_2.54mm:PinHeader_1x02_P2.54mm_Vertical |

---

## Step 3: Update PCB from Schematic

1. In Schematic Editor: **Tools → Update PCB from Schematic** (F8)
2. Review changes and click "Update PCB"
3. New components will appear grouped - spread them out

---

## Step 4: PCB Layout & Routing

### 4.1 Board Setup
The PCB file already has:
- Board outline: 100mm × 80mm
- 4× M3 mounting holes in corners

To modify:
- **File → Board Setup**
- Set Design Rules:
  - Track width: 0.25mm (signals), 1.5mm (power)
  - Clearance: 0.2mm
  - Via size: 0.8mm drill 0.4mm

### 4.2 Component Placement
Components are pre-placed but you may want to adjust:

```
┌─────────────────────────────────────────────────────────────────┐
│ [M3]  J1    C4   U2     C1 C2                              [M3] │
│       PWR   100µ  REG                                           │
│                                                                 │
│       D1    C3           ┌──────────────┐                       │
│       LED                │    ESP32     │     Q1-Q4    Q5-Q8    │
│                          │     U1       │     CH1-4    CH5-8    │
│       J10   J11          └──────────────┘                       │
│       BTN1  BTN2                                                │
│       R17   R18                            J2-J5      J6-J9     │
│                                            OUT1-4     OUT5-8    │
│ [M3]                                                       [M3] │
└─────────────────────────────────────────────────────────────────┘
```

**Placement Tips:**
- Keep decoupling capacitors close to IC power pins
- MOSFETs in a row for easy heatsinking if needed
- Output terminals along board edge
- ESP32 antenna area (top of module) clear of ground plane

### 4.3 Create Ground Plane
1. Select bottom copper layer (B.Cu)
2. **Place → Zone** (or press **Ctrl+Shift+Z**)
3. Draw rectangle around entire board
4. Set net to "GND"
5. Click OK

### 4.4 Route Traces

**Recommended routing order:**

1. **Power traces first (VIN, GND)**
   - Use 1.5mm width for VIN
   - VIN runs from J1 to all LED terminals (J2-J9 pin 1)
   - Connect all MOSFET sources to GND plane with vias

2. **3.3V traces**
   - 0.5mm width
   - From U2 output to ESP32, WS2812, pull-up resistors

3. **Signal traces**
   - 0.25mm width
   - PWM signals: ESP32 GPIO → 100Ω gate resistor → MOSFET gate
   - Keep short and direct

**Routing shortcuts:**
- **X** - Start routing
- **V** - Place via (change layer)
- **/** - Switch layer
- **D** - Drag trace
- **U** - Unroute

### 4.5 Design Rule Check (DRC)
- **Inspect → Design Rules Checker**
- Fix all errors before generating Gerbers
- Common issues: clearance violations, unconnected nets

---

## Step 5: Generate Gerber Files

### 5.1 Plot Gerbers
1. **File → Plot** (or **Ctrl+P** in PCB editor)
2. Set output directory: `./gerbers/`
3. Select layers:
   - [x] F.Cu (Front Copper)
   - [x] B.Cu (Back Copper)
   - [x] F.SilkS (Front Silkscreen)
   - [x] B.SilkS (Back Silkscreen)
   - [x] F.Mask (Front Solder Mask)
   - [x] B.Mask (Back Solder Mask)
   - [x] Edge.Cuts (Board Outline)
   - [x] F.Paste (Front Paste - for stencil)

4. Options:
   - [x] Plot footprint values
   - [x] Plot footprint references
   - [x] Use Protel filename extensions
   - Format: Gerber

5. Click **Plot**

### 5.2 Generate Drill Files
1. In Plot dialog, click **Generate Drill Files**
2. Settings:
   - Drill Units: Millimeters
   - Zeros Format: Decimal format
   - Drill Map: None (or Gerber if your fab wants it)
   - [x] PTH and NPTH in single file (or separate, check fab requirements)
3. Click **Generate Drill File**

### 5.3 Create ZIP for Fab House
```bash
cd /path/to/project/gerbers/
zip VANDIMMER-8CH-gerbers.zip *.gbr *.drl
```

---

## Step 6: Order PCBs

### JLCPCB Settings
Upload ZIP, then set:
- Layers: 2
- Dimensions: 100 × 80 mm
- PCB Thickness: 1.6mm
- Surface Finish: HASL (cheapest) or ENIG (better for SMD)
- Copper Weight: 1oz (2oz for high current)
- Solder Mask: Any colour
- Remove Order Number: Yes (or specify location)

### PCBWay / OSH Park
Similar settings, upload the same ZIP file.

---

## Step 7: Generate BOM and Position Files (Optional for Assembly)

### BOM Export
1. In Schematic: **Tools → Generate BOM**
2. Select a BOM plugin (e.g., bom2csv)
3. Generate CSV file

### Component Position File (for pick-and-place)
1. In PCB Editor: **File → Fabrication Outputs → Component Placement**
2. Generate .pos files for SMD assembly

---

## Quick Reference: KiCad Shortcuts

| Action | Shortcut |
|--------|----------|
| Add wire | W |
| Add power symbol | P |
| Add component | A |
| Route track | X |
| Place via | V |
| Select layer | Page Up/Down |
| Delete | Del |
| Move | M |
| Rotate | R |
| Edit properties | E |
| Run DRC | Shift+D |
| Update PCB | F8 |

---

## Troubleshooting

**"Missing footprint" errors:**
- Ensure KiCad libraries are installed
- **Preferences → Manage Footprint Libraries**
- Add standard libraries if missing

**"Unconnected net" warnings:**
- May be intentional (unused GPIO)
- Add no-connect flags in schematic

**DRC clearance errors:**
- Increase trace/pad spacing
- Move components apart
- Use smaller trace widths

**Gerbers look wrong in viewer:**
- Use online Gerber viewer (e.g., tracespace.io, JLCPCB's viewer)
- Ensure all layers are included
- Check drill file alignment

---

## Files Generated

After completion, you should have:
```
VANDIMMER-8CH/
├── VANDIMMER-8CH.kicad_pro
├── VANDIMMER-8CH.kicad_sch
├── VANDIMMER-8CH.kicad_pcb
└── gerbers/
    ├── VANDIMMER-8CH-F_Cu.gbr
    ├── VANDIMMER-8CH-B_Cu.gbr
    ├── VANDIMMER-8CH-F_SilkS.gbr
    ├── VANDIMMER-8CH-B_SilkS.gbr
    ├── VANDIMMER-8CH-F_Mask.gbr
    ├── VANDIMMER-8CH-B_Mask.gbr
    ├── VANDIMMER-8CH-Edge_Cuts.gbr
    ├── VANDIMMER-8CH-F_Paste.gbr
    ├── VANDIMMER-8CH.drl
    └── VANDIMMER-8CH-gerbers.zip
```
