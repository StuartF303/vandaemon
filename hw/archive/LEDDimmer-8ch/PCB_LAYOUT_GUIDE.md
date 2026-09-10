# LED Dimmer v2.0 - PCB Layout Guide

## Board Specifications

- **Dimensions**: 100mm (W) × 80mm (H)
- **Layers**: 2 (Top + Bottom)
- **Thickness**: 1.6mm
- **Material**: FR-4
- **Copper Weight**: 2oz (70µm)
- **Finish**: HASL or ENIG

## Coordinate System

Origin (0, 0) is at bottom-left corner of PCB.
All coordinates in millimeters from origin.

## Component Placement Map

### Power Section (Bottom Center)

```
Component    X-Position  Y-Position  Rotation  Layer  Notes
─────────────────────────────────────────────────────────────
J1 (Power)   50.0mm      5.0mm       0°        TOP    Centered on bottom edge
U2 (Reg)     50.0mm      20.0mm      0°        TOP    Above power connector
C2 (10µF)    65.0mm      20.0mm      0°        TOP    Regulator output
C1 (100nF)   70.0mm      20.0mm      0°        TOP    Decoupling
```

### ESP32 Module (Center)

```
Component    X-Position  Y-Position  Rotation  Layer  Notes
─────────────────────────────────────────────────────────────
U1 (ESP32)   50.0mm      40.0mm      0°        TOP    Board center
```

### Left Bank - Channels 1-4 (Left Side)

```
CHANNEL 1:
Q1 (MOSFET)  15.0mm      25.0mm      90°       TOP    Tab facing left
R1 (100R)    22.0mm      25.0mm      0°        TOP    Gate resistor
R9 (10K)     22.0mm      28.0mm      90°       TOP    Pull-down
J2 (Output)  5.0mm       25.0mm      0°        TOP    Board edge

CHANNEL 2:
Q2 (MOSFET)  15.0mm      40.0mm      90°       TOP    Tab facing left
R2 (100R)    22.0mm      40.0mm      0°        TOP    Gate resistor
R10 (10K)    22.0mm      43.0mm      90°       TOP    Pull-down
J3 (Output)  5.0mm       40.0mm      0°        TOP    Board edge

CHANNEL 3:
Q3 (MOSFET)  15.0mm      55.0mm      90°       TOP    Tab facing left
R3 (100R)    22.0mm      55.0mm      0°        TOP    Gate resistor
R11 (10K)    22.0mm      58.0mm      90°       TOP    Pull-down
J4 (Output)  5.0mm       55.0mm      0°        TOP    Board edge

CHANNEL 4:
Q4 (MOSFET)  15.0mm      70.0mm      90°       TOP    Tab facing left
R4 (100R)    22.0mm      70.0mm      0°        TOP    Gate resistor
R12 (10K)    22.0mm      73.0mm      90°       TOP    Pull-down
J5 (Output)  5.0mm       70.0mm      0°        TOP    Board edge
```

### Right Bank - Channels 5-8 (Right Side)

```
CHANNEL 5:
Q5 (MOSFET)  85.0mm      25.0mm      270°      TOP    Tab facing right
R5 (100R)    78.0mm      25.0mm      0°        TOP    Gate resistor
R13 (10K)    78.0mm      28.0mm      90°       TOP    Pull-down
J8 (Output)  95.0mm      25.0mm      180°      TOP    Board edge

CHANNEL 6:
Q6 (MOSFET)  85.0mm      40.0mm      270°      TOP    Tab facing right
R6 (100R)    78.0mm      40.0mm      0°        TOP    Gate resistor
R14 (10K)    78.0mm      43.0mm      90°       TOP    Pull-down
J9 (Output)  95.0mm      40.0mm      180°      TOP    Board edge

CHANNEL 7:
Q7 (MOSFET)  85.0mm      55.0mm      270°      TOP    Tab facing right
R7 (100R)    78.0mm      55.0mm      0°        TOP    Gate resistor
R15 (10K)    78.0mm      58.0mm      90°       TOP    Pull-down
J10 (Output) 95.0mm      55.0mm      180°      TOP    Board edge

CHANNEL 8:
Q8 (MOSFET)  85.0mm      70.0mm      270°      TOP    Tab facing right
R8 (100R)    78.0mm      70.0mm      0°        TOP    Gate resistor
R16 (10K)    78.0mm      73.0mm      90°       TOP    Pull-down
J11 (Output) 95.0mm      70.0mm      180°      TOP    Board edge
```

### Status LED & Buttons (Top Center)

```
Component    X-Position  Y-Position  Rotation  Layer  Notes
─────────────────────────────────────────────────────────────
D1 (WS2812)  50.0mm      75.0mm      0°        TOP    Status indicator
C3 (100nF)   55.0mm      75.0mm      0°        TOP    LED decoupling
SW1 (Reset)  40.0mm      10.0mm      0°        TOP    Left button
R17 (10K)    38.0mm      13.0mm      0°        TOP    Button pull-up
SW2 (Test)   60.0mm      10.0mm      0°        TOP    Right button
R18 (10K)    62.0mm      13.0mm      0°        TOP    Button pull-up
```

## Mounting Holes

```
Hole  X-Position  Y-Position  Diameter  Notes
────────────────────────────────────────────────
H1    5.0mm       5.0mm       3.2mm     Bottom-left
H2    95.0mm      5.0mm       3.2mm     Bottom-right
H3    5.0mm       75.0mm      3.2mm     Top-left
H4    95.0mm      75.0mm      3.2mm     Top-right
```

Use M3 screws with 10mm standoffs for case mounting.

## PCB Zones and Pours

### Top Layer (Component Side)

**Zone 1: VIN Power Distribution**
- Net: VIN
- Start: (35, 5) → (65, 22)
- Clearance: 0.5mm
- Min Width: 2.0mm
- Priority: 2

**Zone 2: 3.3V Distribution**
- Net: 3V3
- Start: (35, 35) → (65, 45)
- Clearance: 0.3mm
- Min Width: 0.8mm
- Priority: 3

**Zone 3: Top Ground Pour**
- Net: GND
- Area: Entire top layer (except where blocked)
- Clearance: 0.3mm
- Thermal Relief: Yes (4 spokes, 0.4mm width)
- Priority: 1

### Bottom Layer (Ground Plane)

**Zone 4: Solid Ground Plane**
- Net: GND
- Area: Entire bottom layer
- Clearance: 0.3mm
- Thermal Relief: Yes (for hand soldering)
- Priority: 1

## Critical Trace Routing

### Power Traces (VIN)

```
Trace        Start Point         End Point           Width   Net
──────────────────────────────────────────────────────────────────
VIN Main     J1 Pin 1            U2 VIN              4.0mm   VIN
VIN to Q1    VIN zone            Q1 Drain            1.5mm   VIN
VIN to Q2    VIN zone            Q2 Drain            1.5mm   VIN
VIN to Q3    VIN zone            Q3 Drain            1.5mm   VIN
VIN to Q4    VIN zone            Q4 Drain            1.5mm   VIN
VIN to Q5    VIN zone            Q5 Drain            1.5mm   VIN
VIN to Q6    VIN zone            Q6 Drain            1.5mm   VIN
VIN to Q7    VIN zone            Q7 Drain            1.5mm   VIN
VIN to Q8    VIN zone            Q8 Drain            1.5mm   VIN
```

### Ground Return Paths

```
Connection   Start Point         End Point           Vias    Notes
────────────────────────────────────────────────────────────────────
Q1 Source    Q1 Pin 3            GND plane           4×      0.8mm dia
Q2 Source    Q2 Pin 3            GND plane           4×      0.8mm dia
Q3 Source    Q3 Pin 3            GND plane           4×      0.8mm dia
Q4 Source    Q4 Pin 3            GND plane           4×      0.8mm dia
Q5 Source    Q5 Pin 3            GND plane           4×      0.8mm dia
Q6 Source    Q6 Pin 3            GND plane           4×      0.8mm dia
Q7 Source    Q7 Pin 3            GND plane           4×      0.8mm dia
Q8 Source    Q8 Pin 3            GND plane           4×      0.8mm dia
J1 GND       J1 Pin 2            GND plane           6×      0.8mm dia
```

### Signal Traces (GPIO to MOSFET Gates)

```
Signal     Start (ESP32)       Via R1-R8           End (Gate)     Width
──────────────────────────────────────────────────────────────────────
CH1_PWM    GPIO25              R1                  Q1 Gate        0.25mm
CH2_PWM    GPIO26              R2                  Q2 Gate        0.25mm
CH3_PWM    GPIO27              R3                  Q3 Gate        0.25mm
CH4_PWM    GPIO14              R4                  Q4 Gate        0.25mm
CH5_PWM    GPIO4               R5                  Q5 Gate        0.25mm
CH6_PWM    GPIO5               R6                  Q6 Gate        0.25mm
CH7_PWM    GPIO18              R7                  Q7 Gate        0.25mm
CH8_PWM    GPIO19              R8                  Q8 Gate        0.25mm
WS_DATA    GPIO16              -                   D1 DIN         0.25mm
BTN1       GPIO32              SW1                 -              0.25mm
BTN2       GPIO33              SW2                 -              0.25mm
```

## Design Rules

### Clearances

```
Rule                          Value    Notes
───────────────────────────────────────────────────────────
Minimum trace width           0.2mm    Signal traces
Minimum trace spacing         0.2mm    All nets
Minimum via diameter          0.6mm    Signal vias
Minimum via drill             0.3mm    Standard drill
Power trace width (VIN)       4.0mm    Main power input
Power trace width (channels)  1.5mm    Per-channel distribution
3.3V trace width              0.8mm    Logic power
Minimum annular ring          0.15mm   Drill to copper
Edge clearance                3.0mm    Board edge to trace
```

### Via Specifications

```
Via Type       Diameter  Drill    Qty    Usage
──────────────────────────────────────────────────────────
Power via      1.2mm     0.8mm    30+    VIN, GND stitching
Signal via     0.8mm     0.4mm    50+    GPIO routing
Thermal via    0.6mm     0.3mm    32     MOSFET source to GND
```

## Silkscreen Layout

### Top Silkscreen (White on Green)

**Channel Labels** (near screw terminals):
```
Position     Text          Size    Justification
────────────────────────────────────────────────
J2           "CH1 LED+"    1.2mm   Left
J3           "CH2 LED+"    1.2mm   Left
J4           "CH3 LED+"    1.2mm   Left
J5           "CH4 LED+"    1.2mm   Left
J8           "CH5 LED+"    1.2mm   Right
J9           "CH6 LED+"    1.2mm   Right
J10          "CH7 LED+"    1.2mm   Right
J11          "CH8 LED+"    1.2mm   Right
```

**Power Connector**:
```
J1 (+):      "12-24V DC"   1.5mm   Bold
J1 (-):      "GND"         1.5mm   Bold
Nearby:      "⚠ POLARITY"  1.0mm   Warning triangle
```

**Button Labels**:
```
SW1:         "RESET"       1.2mm   Above button
SW2:         "TEST"        1.2mm   Above button
```

**Board Information**:
```
Position     Text                    Size    Location
──────────────────────────────────────────────────────────
Top-left     "VanDaemon LED Dimmer"  2.0mm   Above H3
Top-left     "v2.0"                  1.5mm   Below title
Top-right    "8× 1.5A PWM Channels"  1.2mm   Above H4
```

**Reference Designators**:
All component reference designators (Q1-Q8, R1-R18, etc.) placed near components, 0.8mm height.

### Bottom Silkscreen

**Safety Warnings**:
```
Center:      "⚠ 12-24V DC ONLY"           2.0mm   Bold
Center:      "MAX 1.5A PER CHANNEL"       1.2mm   Normal
Center:      "ESD SENSITIVE"              1.0mm   Normal
Bottom:      "VanDaemon Project 2025"     1.0mm   Copyright
```

## Solder Mask & Paste

### Solder Mask Clearance
- Standard: 0.1mm expansion from pads
- MOSFET thermal pads: No mask (exposed copper)
- Test points: No mask (if added)

### Solder Paste
- All SMD pads: Standard paste layer
- Through-hole: No paste (hand solder)
- ESP32 module: Paste on pads if using bare module

## 3D Preview Recommendations

When viewing 3D model in KiCad:
1. Verify MOSFET orientation (tabs outward)
2. Check screw terminal accessibility
3. Confirm button placement for case cutouts
4. Verify component clearances (min 2mm between parts)
5. Check for adequate spacing above MOSFETs (25mm)

## Manufacturing Checklist

Before sending to fabrication:

- [ ] Run DRC (Design Rule Check) - zero errors
- [ ] Run ERC (Electrical Rule Check) - zero errors
- [ ] Verify all footprints match BOM parts
- [ ] Check trace widths for current capacity
- [ ] Verify via count and placement
- [ ] Review ground plane connectivity (no islands)
- [ ] Confirm mounting hole positions
- [ ] Check silkscreen readability
- [ ] Export Gerbers (RS-274X format)
- [ ] Export drill files (Excellon format)
- [ ] Generate BOM CSV
- [ ] Generate assembly drawing PDF
- [ ] Archive project files

## Recommended PCB Manufacturers

For hobbyist quantities (1-10 boards):
- **JLCPCB**: Low cost, good quality, fast shipping
- **PCBWay**: Higher quality, more finish options
- **OSH Park**: Made in USA, excellent quality, higher cost

For production quantities (100+):
- **ALLPCB**: Good pricing for volume
- **Elecrow**: Assembly services available

Order specifications:
- 2-layer, 100×80mm
- 1.6mm thickness
- 2oz copper
- HASL or ENIG finish
- Green solder mask (or matte black)
- White silkscreen

Typical cost: $5-15 for 5 boards (JLCPCB), 5-7 day fabrication + shipping.

## Next Step: 3D Enclosure Design

After PCB layout is complete, proceed to design 3D-printable enclosure with:
- Mounting posts for M3×10mm standoffs
- Ventilation slots for MOSFET cooling
- Cutouts for screw terminals, buttons, status LED
- Cable strain relief for power input
- Access panel for ESP32 USB programming

See: `ENCLOSURE_DESIGN.md` (to be created)
