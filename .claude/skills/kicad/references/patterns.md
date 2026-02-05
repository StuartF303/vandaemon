# KiCad Patterns Reference

## Contents
- Comment Syntax
- S-Expression File Format
- Schematic Symbols
- Net and Wire Connections
- Footprint Assignment
- Design Rule Patterns
- Anti-Patterns

## Comment Syntax

**CRITICAL:** KiCad uses `#` for comments, NOT semicolons.

```kicad
# GOOD - This is a valid comment
# Section: Power regulation

; BAD - This is NOT a comment, KiCad will parse it as data
```

**Why This Matters:** Semicolons in KiCad files corrupt the schematic. The parser treats them as text content, causing load failures or corrupted component properties.

## S-Expression File Format

KiCad 7+ uses Lisp-style S-expressions for all files:

```kicad
(kicad_sch (version 20231120) (generator "eeschema")
  (uuid "a1b2c3d4-...")
  
  # Library symbols used in this schematic
  (lib_symbols
    (symbol "Device:R" ...)
  )
  
  # Actual component instances
  (symbol (lib_id "Device:R")
    (at 127 76.2 0)
    (property "Reference" "R1" (at 127 71.12 0))
    (property "Value" "4.7k" (at 127 81.28 0))
  )
)
```

### Property Structure

```kicad
(property "Name" "Value" (at X Y rotation)
  (effects (font (size 1.27 1.27)))
)
```

| Property | Purpose | Example |
|----------|---------|---------|
| Reference | Component designator | "R1", "U1", "C5" |
| Value | Component value | "10k", "ESP32-WROOM" |
| Footprint | PCB footprint path | "Package_QFP:LQFP-48..." |
| Datasheet | Documentation link | "https://..." |

## Schematic Symbols

### ESP32 Module (LED Dimmer)

```kicad
(symbol (lib_id "RF_Module:ESP32-WROOM-32")
  (at 101.6 88.9 0)
  (unit 1)
  (property "Reference" "U1" (at 101.6 45.72 0))
  (property "Value" "ESP32-WROOM-32" (at 101.6 48.26 0))
  (property "Footprint" "RF_Module:ESP32-WROOM-32" (at 101.6 88.9 0))
)
```

### MOSFET for PWM Output

```kicad
(symbol (lib_id "Transistor_FET:IRLZ44N")
  (at 165.1 101.6 0)
  (unit 1)
  (property "Reference" "Q1" (at 170.18 100.33 0))
  (property "Value" "IRLZ44N" (at 170.18 102.87 0))
  (property "Footprint" "Package_TO_SOT_THT:TO-220-3_Vertical" (at 165.1 101.6 0))
)
```

## Net and Wire Connections

### Basic Wire

```kicad
(wire (pts (xy 127 76.2) (xy 152.4 76.2)))
```

### Named Net Label

```kicad
(label "PWM_CH0" (at 140 76.2 0)
  (effects (font (size 1.27 1.27)))
  (uuid "...")
)
```

### Power Net

```kicad
(power_port (lib_id "power:+12V")
  (at 101.6 38.1 0)
  (property "Reference" "#PWR01" (at 101.6 42.164 0))
)
```

### Hierarchical Label (for multi-sheet)

```kicad
(hierarchical_label "LED_OUT[0..7]" (shape output)
  (at 203.2 88.9 0)
)
```

## Footprint Assignment

### SMD Resistor (0603)

```kicad
(property "Footprint" "Resistor_SMD:R_0603_1608Metric" ...)
```

### Through-Hole Connector

```kicad
(property "Footprint" "Connector_PinHeader_2.54mm:PinHeader_1x08_P2.54mm_Vertical" ...)
```

### LED Dimmer Specific Footprints

| Component | Footprint | Reason |
|-----------|-----------|--------|
| ESP32 | ESP32-WROOM-32 | Standard module |
| MOSFETs | TO-220-3 | Heat dissipation for 2A |
| Screw terminals | TerminalBlock_P5.08mm | 12V power handling |
| Status LED | LED_WS2812B | Addressable RGB |

## Design Rule Patterns

### Automotive-Grade Clearances

```
Track width (signal): 0.25mm minimum
Track width (power): 0.5mm minimum, 1.0mm preferred for 2A
Clearance: 0.2mm minimum
Via drill: 0.4mm
Via pad: 0.8mm
```

### Thermal Relief for Power Pads

```kicad
(zone (net "GND") (layer "B.Cu")
  (connect_pads (thermal_relief_gap 0.508) (thermal_relief_width 0.508))
)
```

## Anti-Patterns

### WARNING: Semicolon Comments

**The Problem:**

```kicad
; BAD - This corrupts the file
(symbol (lib_id "Device:R") ; inline comment breaks too
```

**Why This Breaks:** KiCad parser treats `;` as literal text, not comment delimiter. File becomes unreadable.

**The Fix:**

```kicad
# GOOD - Use hash comments on separate lines
(symbol (lib_id "Device:R")
```

### WARNING: Missing Footprint Assignment

**The Problem:**

```kicad
(property "Footprint" "" (at 0 0 0))  # Empty footprint
```

**Why This Breaks:** PCB export fails. DRC errors block manufacturing files.

**The Fix:**

```kicad
(property "Footprint" "Resistor_SMD:R_0603_1608Metric" (at 0 0 0))
```

### WARNING: Unconnected Pins Without No-Connect Flag

**The Problem:** Leaving pins floating without explicit `no_connect` markers.

**Why This Breaks:** ERC (Electrical Rules Check) generates false warnings, masking real errors.

**The Fix:**

```kicad
(no_connect (at 152.4 88.9) (uuid "..."))