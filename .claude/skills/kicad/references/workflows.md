# KiCad Workflows Reference

## Contents
- Project Setup
- Schematic Capture
- PCB Layout
- Design Rule Check
- Manufacturing Export
- Version Control

## Project Setup

### Create New KiCad Project

```bash
# Create project directory
mkdir -p hw/NewProject

# KiCad creates these files:
# - NewProject.kicad_pro (project settings)
# - NewProject.kicad_sch (schematic)
# - NewProject.kicad_pcb (PCB layout)
```

### Project File Structure

```
hw/LEDDimmer/
├── led_dimmer_8ch.kicad_pro    # Project config (JSON)
├── led_dimmer_8ch.kicad_sch    # Schematic (S-expression)
├── led_dimmer_8ch.kicad_pcb    # PCB layout (S-expression)
├── led_dimmer_8ch-backups/     # Auto-backups
├── fp-lib-table                # Footprint library paths
└── sym-lib-table               # Symbol library paths
```

## Schematic Capture Workflow

### Step-by-Step Process

Copy this checklist and track progress:
- [ ] Create schematic sheet with title block
- [ ] Place power symbols (+12V, +3.3V, GND)
- [ ] Add main IC (ESP32 module)
- [ ] Add peripheral circuits (MOSFETs, drivers)
- [ ] Wire all connections
- [ ] Add net labels for clarity
- [ ] Run ERC (Electrical Rules Check)
- [ ] Fix all ERC errors
- [ ] Assign footprints to all components
- [ ] Generate netlist

### Adding Components via CLI

```bash
# Search for component in library
grep -r "ESP32" /usr/share/kicad/symbols/

# Verify symbol exists
grep "symbol \"RF_Module:ESP32" hw/LEDDimmer/led_dimmer_8ch.kicad_sch
```

### Verify Schematic Integrity

```bash
# Check for common issues
grep -c "Footprint\" \"\"" hw/LEDDimmer/led_dimmer_8ch.kicad_sch  # Empty footprints
grep -c "(no_connect" hw/LEDDimmer/led_dimmer_8ch.kicad_sch        # NC flags
grep -c "(wire" hw/LEDDimmer/led_dimmer_8ch.kicad_sch              # Wire count
```

## PCB Layout Workflow

### Design Rule Configuration

```bash
# Design rules are in the PCB file header
grep -A 20 "(setup" hw/LEDDimmer/led_dimmer_8ch.kicad_pcb
```

### Standard Design Rules for LED Dimmer

| Rule | Value | Application |
|------|-------|-------------|
| Min track width | 0.25mm | Signal traces |
| Min clearance | 0.2mm | All copper |
| Min via drill | 0.4mm | Standard vias |
| Power track width | 0.5-1.0mm | 12V, GND |
| Thermal relief | 0.508mm | Ground planes |

### Layer Stack (2-layer)

```
Top (F.Cu)      - Signal, power distribution
Bottom (B.Cu)   - Ground plane, signal routing
F.SilkS         - Component labels
B.SilkS         - Board info
F.Mask          - Solder mask
B.Mask          - Solder mask
Edge.Cuts       - Board outline
```

## Design Rule Check (DRC)

### Run DRC from Command Line

```bash
# KiCad CLI (if available)
kicad-cli pcb drc \
  --output drc_report.txt \
  hw/LEDDimmer/led_dimmer_8ch.kicad_pcb
```

### Common DRC Errors

| Error | Cause | Fix |
|-------|-------|-----|
| Clearance violation | Traces too close | Increase spacing or reroute |
| Unconnected items | Missing copper | Add trace or via |
| Track width | Below minimum | Widen trace |
| Drill too small | Via undersized | Increase drill diameter |

### DRC Validation Loop

1. Run DRC check
2. Review error list
3. Fix highest-severity errors first
4. Re-run DRC
5. Repeat until zero errors
6. Only proceed to manufacturing export when DRC passes

## Manufacturing Export

### Generate Gerber Files

```bash
# Standard Gerber layers for 2-layer board
# F.Cu, B.Cu, F.SilkS, B.SilkS, F.Mask, B.Mask, Edge.Cuts

# Drill file
# PTH (Plated Through Hole) and NPTH (Non-Plated)
```

### Export Checklist

Copy this checklist and track progress:
- [ ] DRC passes with zero errors
- [ ] All footprints assigned
- [ ] Board outline closed (Edge.Cuts)
- [ ] Mounting holes present
- [ ] Fiducials added (if SMD assembly)
- [ ] Generate Gerber files (all layers)
- [ ] Generate drill files (Excellon format)
- [ ] Generate BOM (Bill of Materials)
- [ ] Generate position file (for assembly)
- [ ] Create ZIP for manufacturer upload
- [ ] Verify Gerber with viewer (gerbv or online)

### Gerber Verification

```bash
# Install gerbv for verification
sudo apt install gerbv

# View generated Gerbers
gerbv hw/LEDDimmer/gerbers/*.gbr
```

## Version Control

### Files to Track

```gitignore
# Track these KiCad files
*.kicad_pro
*.kicad_sch
*.kicad_pcb
*.kicad_sym
*.kicad_mod
fp-lib-table
sym-lib-table

# Ignore these
*-backups/
*.kicad_prl      # Local preferences
fp-info-cache
```

### Commit Message Pattern

```bash
git add hw/LEDDimmer/led_dimmer_8ch.kicad_sch
git commit -m "hw(LEDDimmer): Add gate resistors for MOSFET channels

- Added R1-R8 (100Ω) for gate drive limiting
- Prevents oscillation at PWM switching edges
- Updated footprints for 0603 SMD"
```

### Diff KiCad Files

```bash
# S-expression files diff reasonably well
git diff hw/LEDDimmer/led_dimmer_8ch.kicad_sch

# For visual diff, use KiDiff or plotgitsch
```

## Integration with PlatformIO

The LED dimmer schematic defines the hardware that the **platformio** skill firmware targets:

| Schematic Net | ESP32 GPIO | Firmware Reference |
|---------------|------------|-------------------|
| PWM_CH0-7 | GPIO 25,26,27,14,4,5,18,19 | `config.h` |
| STATUS_LED | GPIO 16 | WS2812 data pin |
| BTN1, BTN2 | GPIO 32, 33 | Button inputs |

See the **platformio** skill for firmware development workflows.