# 8-Channel LED Dimmer v2.0 - Hardware Design Documentation

## Design Overview

The v2.0 design features a redesigned PCB layout with:
- **Dual MOSFET Banks**: 4 channels on left side, 4 channels on right side
- **Integrated Push Buttons**: Two tactile switches for reset/test operations
- **Dual Screw Terminal**: Robust 12-24V DC power input
- **Optimized Thermal Management**: MOSFETs positioned for better airflow

## Circuit Architecture

### Board Layout Philosophy

```
┌──────────────────────────────────────────────────┐
│  LEFT BANK (CH1-4)      CENTER      RIGHT BANK   │
│                                      (CH5-8)      │
│  Q1 ─ LED OUT 1         ESP32         Q5 ─ LED   │
│  Q2 ─ LED OUT 2                       Q6 ─ LED   │
│  Q3 ─ LED OUT 3       ┌──────┐        Q7 ─ LED   │
│  Q4 ─ LED OUT 4       │ U1   │        Q8 ─ LED   │
│                       └──────┘                    │
│  [SW1] [SW2]         POWER IN         STATUS LED │
│  RESET  TEST         (J1)             (D1)        │
└──────────────────────────────────────────────────┘
```

### Component Placement Strategy

**Left Bank (Channels 1-4)**
- GPIO25, 26, 27, 14
- MOSFETs Q1-Q4 aligned vertically
- Screw terminals J2-J5 at board edge
- Easy access for high-current wiring

**Right Bank (Channels 5-8)**
- GPIO4, 5, 18, 19
- MOSFETs Q5-Q8 aligned vertically (mirrored)
- Screw terminals J8-J11 at board edge
- Symmetric thermal distribution

**Central Section**
- ESP32-WROOM-32 module (U1)
- AMS1117-3.3 regulator (U2)
- Power input screw terminal (J1)
- Status WS2812B RGB LED (D1)
- Two tactile pushbuttons (SW1, SW2)

## Pin Assignments

| Function       | ESP32 GPIO | Bank  | Component | Notes                          |
|----------------|------------|-------|-----------|--------------------------------|
| PWM Channel 1  | GPIO25     | LEFT  | Q1        | LEDC Channel 0                 |
| PWM Channel 2  | GPIO26     | LEFT  | Q2        | LEDC Channel 1                 |
| PWM Channel 3  | GPIO27     | LEFT  | Q3        | LEDC Channel 2                 |
| PWM Channel 4  | GPIO14     | LEFT  | Q4        | LEDC Channel 3                 |
| PWM Channel 5  | GPIO4      | RIGHT | Q5        | LEDC Channel 4                 |
| PWM Channel 6  | GPIO5      | RIGHT | Q6        | LEDC Channel 5                 |
| PWM Channel 7  | GPIO18     | RIGHT | Q7        | LEDC Channel 6                 |
| PWM Channel 8  | GPIO19     | RIGHT | Q8        | LEDC Channel 7                 |
| Status LED     | GPIO16     | -     | D1        | WS2812B RGB (RMT peripheral)   |
| Reset Button   | GPIO32     | -     | SW1       | Active LOW, 10K pull-up        |
| Test Button    | GPIO33     | -     | SW2       | Active LOW, 10K pull-up        |

## Bill of Materials (BOM) - v2.0

### Core Components

| Reference | Qty | Value        | Package       | Description                    | Notes                    |
|-----------|-----|--------------|---------------|--------------------------------|--------------------------|
| U1        | 1   | ESP32-WROOM  | Module        | ESP32-WROOM-32 WiFi module     | 38-pin version           |
| U2        | 1   | AMS1117-3.3  | SOT-223       | 3.3V 1A LDO regulator          | Or equivalent            |
| Q1-Q8     | 8   | IRLZ44N      | TO-220        | 55V 47A N-MOSFET logic-level   | Heat sink optional       |
| D1        | 1   | WS2812B      | 5050 LED      | RGB addressable LED            | Status indicator         |

### Passive Components

| Reference | Qty | Value  | Package | Description              | Voltage Rating |
|-----------|-----|--------|---------|--------------------------|----------------|
| R1-R8     | 8   | 100Ω   | 0805    | MOSFET gate resistors    | 1/8W           |
| R9-R16    | 8   | 10KΩ   | 0805    | MOSFET gate pull-downs   | 1/8W           |
| R17-R18   | 2   | 10KΩ   | 0805    | Button pull-ups          | 1/8W           |
| C1        | 1   | 100nF  | 0805    | Decoupling capacitor     | 50V ceramic    |
| C2        | 1   | 10µF   | 0805    | Bulk capacitor (input)   | 35V electro    |
| C3        | 1   | 100nF  | 0805    | WS2812B decoupling       | 16V ceramic    |

### Connectors & Switches

| Reference | Qty | Type                | Pitch  | Description               | Current Rating |
|-----------|-----|---------------------|--------|---------------------------|----------------|
| J1        | 1   | Screw terminal 2pos | 5.08mm | Power input (12-24V DC)   | 10A            |
| J2-J5     | 4   | Screw terminal 2pos | 5.08mm | LED outputs CH1-4 (LEFT)  | 8A             |
| J8-J11    | 4   | Screw terminal 2pos | 5.08mm | LED outputs CH5-8 (RIGHT) | 8A             |
| SW1       | 1   | Tactile switch      | 6mm    | RESET button (GPIO32)     | 50mA           |
| SW2       | 1   | Tactile switch      | 6mm    | TEST button (GPIO33)      | 50mA           |

### Component Count Summary

| Component Type    | Total Quantity |
|-------------------|----------------|
| ICs               | 2              |
| MOSFETs (TO-220)  | 8              |
| Resistors (0805)  | 18             |
| Capacitors        | 3              |
| Screw Terminals   | 9              |
| Tactile Switches  | 2              |
| RGB LED           | 1              |

## Electrical Specifications

### Power Supply
- **Input Voltage**: 12-24V DC (nominal)
- **Absolute Maximum**: 28V DC
- **Recommended**: 12V for LED strips
- **Input Current**: Up to 12A total (8 channels × 1.5A)
- **Power Connector**: 5.08mm screw terminal, 10A rated

### MOSFET Channels (Each)
- **Switching Device**: IRLZ44N
- **Max Continuous Current**: 1.5A per channel (no heatsink)
- **Max Peak Current**: 3A per channel (with heatsink)
- **Rds(on)**: 22mΩ @ Vgs=5V
- **Power Dissipation**: 33mW @ 1.5A (P = I² × Rds)
- **Gate Drive**: 100Ω series, 10KΩ pull-down
- **PWM Frequency**: 5kHz (configurable up to 25kHz)

### Logic Level
- **Operating Voltage**: 3.3V (ESP32 native)
- **Logic High**: 2.4V min
- **Logic Low**: 0.4V max

## PCB Design Requirements

### Board Dimensions
- **Recommended Size**: 100mm × 80mm
- **Thickness**: 1.6mm (standard FR-4)
- **Copper Weight**: 2oz (70µm) for high current handling
- **Layer Count**: 2-layer (sufficient for this design)

### Trace Width Requirements

| Net Type           | Current | Min Width | Recommended Width |
|--------------------|---------|-----------|-------------------|
| Power input (VIN)  | 12A     | 3.0mm     | 4.0mm             |
| Ground plane       | 12A     | 3.0mm     | 4.0mm (pour)      |
| Channel outputs    | 1.5A    | 0.8mm     | 1.2mm             |
| 3.3V logic         | 500mA   | 0.3mm     | 0.5mm             |
| GPIO signals       | 20mA    | 0.2mm     | 0.25mm            |

### Component Spacing

**MOSFET Placement** (Critical for thermal management)
- Minimum spacing between MOSFETs: 15mm center-to-center
- Distance from board edge: 5mm minimum
- Orientation: TO-220 tabs facing outward for heatsink mounting
- Clearance above MOSFETs: 25mm for airflow

**Screw Terminal Placement**
- Spacing: 5.08mm pitch (standard)
- Board edge clearance: 3mm minimum
- Access clearance from top: 40mm for wire insertion

**Push Button Placement**
- Location: Bottom center of board
- Spacing between buttons: 15mm center-to-center
- Accessibility: Top-side mounted for case cutouts

### Ground Plane Strategy
- **Bottom layer**: Solid ground pour
- **Top layer**: Ground pour in unused areas
- **Thermal reliefs**: For hand soldering (2-3 spokes)
- **Via stitching**: Every 10mm around power traces

### Thermal Management

**MOSFET Heat Dissipation** (per channel @ 1.5A):
```
P = I² × Rds(on)
P = 1.5² × 0.022Ω = 49mW
```

**Total Board Power Dissipation** (all 8 channels):
```
P_total = 8 × 49mW = 392mW
```

This is very low power dissipation. Heat sinks are **optional** unless:
- Operating at >2A per channel
- Ambient temperature >40°C
- Enclosed case with poor ventilation

**Recommended heatsink**: TO-220 clip-on, 15°C/W thermal resistance

## PCB Layout Guidelines

### Layer Stackup (2-layer)
```
TOP LAYER (Red):
- Component placement
- Signal traces (GPIO, PWM)
- VIN power distribution
- 3.3V power distribution

BOTTOM LAYER (Blue):
- Ground plane (solid pour)
- Return current paths
- Additional power routing if needed
```

### Critical Layout Rules

1. **MOSFET Driver Traces**
   - Keep gate drive traces short (<50mm)
   - Route gate resistor close to MOSFET gate pin
   - Place pull-down resistor near MOSFET

2. **Power Distribution**
   - Star topology from J1 to MOSFET drains
   - Wide traces for VIN distribution
   - Multiple vias for ground connections (at least 4 per MOSFET source)

3. **Decoupling Capacitors**
   - Place C1 (100nF) within 5mm of ESP32 VCC pin
   - Place C2 (10µF) at AMS1117 output
   - Place C3 (100nF) within 10mm of WS2812B VCC pin

4. **High-Current Paths**
   ```
   J1 (VIN) ──[wide trace]──► MOSFET Drains ──► LED+ outputs
   LED- outputs ──► MOSFET Sources ──[multiple vias]──► GND plane
   ```

5. **Signal Integrity**
   - Keep PWM traces away from power traces (3mm separation)
   - Avoid running GPIO under MOSFETs
   - Route WS2812B data line with ground return nearby

### Silkscreen Annotations

**Top Side**:
- Channel numbers (CH1-CH8) near each screw terminal
- Polarity markings (+/-) on all screw terminals
- Button functions ("RESET", "TEST")
- Component reference designators
- Board name and version (e.g., "LED Dimmer v2.0")

**Bottom Side**:
- Voltage warning: "12-24V DC ONLY"
- Current rating: "1.5A per channel max"
- Project name and website

## Testing and Validation

### Power-On Test Sequence

1. **Visual Inspection** (Before power)
   - Check for solder bridges
   - Verify component orientation (especially U2, D1)
   - Confirm no shorts between VIN and GND

2. **Power Supply Test**
   - Connect 12V power supply to J1 (observe polarity!)
   - Measure voltage at U2 input: Should be ~12V
   - Measure 3.3V at ESP32 VCC pin
   - Verify current draw: <100mA without LEDs connected

3. **Button Test**
   - Press SW1 (RESET): GPIO32 should read LOW
   - Press SW2 (TEST): GPIO33 should read LOW
   - Release buttons: GPIOs should read HIGH (pull-up)

4. **Status LED Test**
   - Flash test pattern to WS2812B
   - Verify all colors: Red, Green, Blue, White

5. **Channel Test** (One at a time)
   - Set PWM to 50% (value 128)
   - Connect LED strip to channel output
   - Verify LED brightness is 50%
   - Measure MOSFET source voltage: Should be near GND
   - Verify no excessive heat on MOSFET

6. **Full Load Test** (All channels)
   - Connect loads to all 8 channels
   - Set all channels to 100%
   - Monitor power supply voltage: Should remain stable
   - Check MOSFET temperatures: Should be <60°C ambient
   - Run for 30 minutes continuous

### Troubleshooting Guide

| Symptom                  | Possible Cause                | Solution                          |
|--------------------------|-------------------------------|-----------------------------------|
| No 3.3V output           | U2 installed backwards        | Check regulator orientation       |
| ESP32 won't boot         | Low voltage, bad solder       | Check 3.3V, resolder U2           |
| Channel always on        | MOSFET gate shorted           | Check R1-R8 for shorts            |
| Channel always off       | MOSFET damaged, poor solder   | Replace MOSFET, check solder      |
| Dim LEDs at full power   | High Rds(on), wrong MOSFET    | Verify IRLZ44N (not IRF540)       |
| Status LED wrong colors  | D1 installed backwards        | Check WS2812B orientation         |
| Buttons don't work       | Missing pull-ups              | Check R17, R18 installation       |
| Overheating MOSFETs      | Excessive current, poor Rds   | Add heatsink, reduce load         |

## Design Files

### Generated Files

- `led_dimmer_8ch_v2.kicad_sch` - KiCad schematic (this file)
- `led_dimmer_8ch_v2.kicad_pcb` - KiCad PCB layout (to be created)
- `led_dimmer_8ch_v2_bom.csv` - Bill of materials export
- `led_dimmer_8ch_v2_gerbers.zip` - Gerber files for manufacturing

### Manufacturing Notes

**PCB Fabrication** (recommended specs):
- Minimum track/space: 0.2mm/0.2mm
- Minimum drill: 0.3mm
- Solder mask: Green (or matte black)
- Silkscreen: White
- Surface finish: HASL or ENIG
- Board thickness: 1.6mm
- Copper: 2oz (70µm)

**Assembly Notes**:
- SMD components first (0805 size, hand solder or reflow)
- Through-hole components second
- MOSFETs: Ensure thermal pad makes good contact with copper pour
- ESP32: Can use DevKit module or solder bare WROOM module
- Screw terminals: Ensure polarity matches silkscreen

## Comparison: v1.0 vs v2.0

| Feature                    | v1.0                          | v2.0                          |
|----------------------------|-------------------------------|-------------------------------|
| MOSFET layout              | All on one side               | 2 banks on opposite sides     |
| Board dimensions           | ~100mm × 60mm                 | 100mm × 80mm                  |
| Button inputs              | Header connectors (J6, J7)    | Integrated tactile switches   |
| Power connector            | Single screw terminal         | Dual screw terminal (robust)  |
| Thermal management         | Compact, potential hotspots   | Distributed, better airflow   |
| Serviceability             | Moderate                      | Excellent (symmetric design)  |
| Case mounting              | Basic                         | Optimized for 3D print case   |

## Next Steps

1. ✅ Circuit schematic complete
2. ⏳ PCB layout design
3. ⏳ 3D printed enclosure with ventilation
4. ⏳ Generate manufacturing files (Gerbers, BOM, assembly drawings)
5. ⏳ Prototype build and testing

## Safety Warnings

⚠️ **ELECTRICAL SAFETY**
- This board handles up to 24V DC and 12A total current
- Always disconnect power before handling
- Use properly rated wire for all connections (minimum 18 AWG for power)
- Ensure proper polarity on J1 power connector
- Do not exceed 1.5A per channel without heatsinks

⚠️ **THERMAL SAFETY**
- MOSFETs can reach 60°C+ under load
- Add heatsinks if operating above 1.5A per channel
- Ensure adequate ventilation in enclosure
- Do not cover MOSFETs with insulating materials

⚠️ **ESD PROTECTION**
- ESP32 is sensitive to static discharge
- Use ESD wrist strap during assembly
- Store boards in anti-static bags

## License

Part of the VanDaemon project. See main repository for license details.

## Revision History

| Version | Date       | Changes                                      |
|---------|------------|----------------------------------------------|
| 2.0     | 2025-01-29 | Redesigned with dual MOSFET banks, integrated buttons |
| 1.0     | 2024-11-26 | Initial design with single-side MOSFET layout |
