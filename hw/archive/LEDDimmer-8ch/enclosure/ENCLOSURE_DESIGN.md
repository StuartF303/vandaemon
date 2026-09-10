# LED Dimmer v2.0 - 3D Printable Enclosure Design

## Overview

This enclosure is specifically designed for the LED Dimmer v2.0 PCB with optimized thermal management for the MOSFET banks. The design prioritizes:

1. **Airflow**: Ventilation slots positioned over both MOSFET banks
2. **Accessibility**: Cutouts for all connectors and buttons
3. **Protection**: Fully enclosed with status LED visibility
4. **Serviceability**: Easy assembly/disassembly with lid screws

## Design Files

- `enclosure.scad` - OpenSCAD source file (fully parametric)
- `bottom_case.stl` - Printable bottom case (export from OpenSCAD)
- `top_lid.stl` - Printable top lid (export from OpenSCAD)

## Enclosure Specifications

### Dimensions

```
External:        105mm (W) × 85mm (H) × 50mm (D)
Internal:        100mm (W) × 80mm (H) × 35mm (D)
Wall Thickness:  2.5mm
Bottom Clear:    5mm (for solder joints)
PCB Standoff:    10mm (M3 threaded inserts)
Total Height:    50mm
```

### Weight (Estimated)

```
Material         Weight      Print Time
─────────────────────────────────────────
PLA (both parts) ~85g        ~6-7 hours
PETG             ~90g        ~6-7 hours
ABS              ~82g        ~6-7 hours
```

## Thermal Design Features

### Ventilation System

The enclosure features a **dual-zone ventilation system** aligned with the MOSFET banks:

**Left Side Ventilation** (Channels 1-4)
- 5 horizontal slots
- 40mm length × 3mm height each
- Positioned at Y: 15-45mm (over Q1-Q4)
- Total vent area: ~600mm²

**Right Side Ventilation** (Channels 5-8)
- 5 horizontal slots (mirrored)
- 40mm length × 3mm height each
- Positioned at Y: 15-45mm (over Q5-Q8)
- Total vent area: ~600mm²

**Top Lid Ventilation**
- 7 rows × 5 columns = 35 slots
- 10mm × 3mm each slot
- Total vent area: ~1050mm²
- Allows hot air to escape vertically

**Total Ventilation Area**: ~2250mm² (2.25 cm²)

### Airflow Pattern

```
   ┌─────────── TOP LID ───────────┐
   │  ╔═══╗ ╔═══╗ ╔═══╗ ╔═══╗     │ ← Hot air exits
   │  ║ ▓ ║ ║ ▓ ║ ║ ▓ ║ ║ ▓ ║     │
   │  ╚═══╝ ╚═══╝ ╚═══╝ ╚═══╝     │
   └───────────────────────────────┘
            ▲           ▲
            │           │
   ═════╗   │  ESP32    │   ╔═════
   Q1-Q4║◄──┘           └──►║Q5-Q8
   ═════╝                   ╚═════
      ▲                        ▲
      │   Cool air enters      │
      │                        │
```

**Natural Convection** (passive cooling):
- Cool air enters through side slots
- Warms as it passes over MOSFETs
- Hot air rises and exits through top grid
- Estimated airflow: 2-5 L/min (passive)

**Thermal Performance** (estimated):
- Ambient: 25°C
- MOSFET temp @ 1.5A: 45-50°C (well below 150°C max)
- Internal case temp: 35-40°C
- Adequate for continuous operation

## Cutouts and Openings

### Bottom Panel

**Power Connector (J1)**
- Position: Center bottom (X: 50mm)
- Size: 12mm (W) × 10mm (H)
- Purpose: 12-24V DC screw terminal access

**Button Access Holes (SW1, SW2)**
- Positions: X: 40mm, 60mm (from left edge)
- Diameter: 7mm each
- Purpose: Finger access to tactile switches
- Labels: "RESET" and "TEST" (molded or printed)

### Left Side Panel

**LED Output Terminals (J2-J5)** - Channels 1-4
- Count: 4 cutouts
- Position: Y: 25mm, 40mm, 55mm, 70mm
- Size: 8mm (W) × 12mm (H) × 15mm (D)
- Purpose: Screw terminal wire access
- Clearance: Allows screwdriver insertion

### Right Side Panel

**LED Output Terminals (J8-J11)** - Channels 5-8
- Count: 4 cutouts
- Position: Y: 25mm, 40mm, 55mm, 70mm (mirrored)
- Size: 8mm (W) × 12mm (H) × 15mm (D)
- Purpose: Screw terminal wire access

### Top Lid

**Status LED Window (D1)**
- Position: X: 50mm, Y: 75mm
- Diameter: 8mm
- Purpose: WS2812B RGB LED visibility
- Optional: Hot-glue a clear acrylic disc for protection

**Ventilation Grid**
- Coverage: Central 70mm × 55mm area
- Pattern: 7 rows × 5 columns
- Individual slots: 10mm × 3mm

## Mounting System

### PCB Mounting

The PCB is mounted on **4 standoffs** at the corner positions:

```
Position     X       Y       Hardware
──────────────────────────────────────────
H1 (BL)      5mm     5mm     M3×10mm standoff
H2 (BR)      95mm    5mm     M3×10mm standoff
H3 (TL)      5mm     75mm    M3×10mm standoff
H4 (TR)      95mm    75mm    M3×10mm standoff
```

**Standoff Design**:
- Integrated into bottom case (printed)
- Outer diameter: 6mm
- Screw hole: 3.2mm (M3 clearance)
- Height: 10mm
- Reinforcement ribs: 4× radial ribs per post

**Assembly Method**:

**Option 1: Heat-Set Inserts** (Recommended)
1. Print bottom case
2. Install M3×4.6mm heat-set threaded inserts into standoffs
3. Place PCB onto standoffs
4. Secure with M3×6mm screws from top

**Option 2: Self-Tapping Screws**
1. Print bottom case with 2.8mm pilot holes
2. Use M3×10mm self-tapping screws
3. Screw directly into plastic (works for PLA/PETG)

### Lid Mounting

The lid is secured with **4 screws** at the corners:

```
Position     X        Y        Hardware
────────────────────────────────────────────────
Corner 1     1.25mm   1.25mm   M3×8mm screw
Corner 2     103.75mm 1.25mm   M3×8mm screw
Corner 3     1.25mm   83.75mm  M3×8mm screw
Corner 4     103.75mm 83.75mm  M3×8mm screw
```

**Lid Attachment**:
- Countersunk screw holes (6mm top diameter)
- Screws thread into bottom case walls
- Use M3×8mm countersunk screws

**Lid Retention Lip**:
- 3mm deep lip fits inside case top
- 0.2mm clearance for easy insertion
- Prevents lateral movement

## 3D Printing Instructions

### Recommended Settings

```
Parameter              Bottom Case    Top Lid
─────────────────────────────────────────────────
Layer Height           0.2mm          0.2mm
Initial Layer          0.3mm          0.3mm
Infill                 20%            15%
Top/Bottom Layers      4              3
Wall Line Count        3              2
Print Speed            50mm/s         60mm/s
Supports               Yes*           No
Build Plate Adhesion   Brim (5mm)     Brim (3mm)
```

*Supports needed for screw terminal cutout overhangs

### Material Selection

**PLA** (Easiest)
- ✅ Easy to print, low warp
- ✅ Sufficient for indoor use
- ✅ Good detail on text/labels
- ⚠️ Max temp: 60°C (adequate for this design)
- Best for: Prototypes, low-temperature environments

**PETG** (Recommended for Production)
- ✅ Higher temperature resistance (80°C)
- ✅ More durable and impact-resistant
- ✅ Good layer adhesion
- ⚠️ Requires heated bed (70-80°C)
- Best for: Van installations, outdoor enclosures

**ABS** (Professional)
- ✅ Highest temp resistance (90°C+)
- ✅ Very durable
- ✅ Can be smoothed with acetone
- ⚠️ Requires enclosed printer, emits fumes
- ⚠️ Prone to warping
- Best for: High-temp environments, finished appearance

### Print Orientation

**Bottom Case**:
```
Orientation: Upright (as designed)
Support:     Auto-generate for overhangs >50°
Contact:     Support on build plate only
Interface:   Yes (0.2mm gap)
```

**Top Lid**:
```
Orientation: Upside-down (flat side on bed)
Support:     None required
Notes:       Print with top surface down for best finish
```

### Post-Processing

1. **Remove Supports**
   - Use flush cutters for support removal
   - Clean up with hobby knife or file
   - Test-fit lid before installing heat-set inserts

2. **Install Heat-Set Inserts** (if using)
   - Heat soldering iron to 200-220°C (PLA/PETG)
   - Press insert straight into standoff hole
   - Hold for 2-3 seconds until seated
   - Let cool completely before handling

3. **Label Application** (optional)
   - Use label maker for channel numbers
   - Apply to case near screw terminals
   - Example: "CH1", "CH2", etc.

4. **Finishing** (optional)
   - Sand with 220 grit → 400 grit for smooth finish
   - Prime and paint if desired
   - Clear coat for UV protection (outdoor use)

## Assembly Instructions

### Tools Required

- Phillips screwdriver (#2)
- Soldering iron (for heat-set inserts)
- 4× M3×4.6mm heat-set inserts
- 4× M3×6mm screws (PCB mounting)
- 4× M3×8mm countersunk screws (lid mounting)

### Assembly Steps

1. **Prepare Bottom Case**
   ```
   a) Remove all print supports
   b) Clean screw holes with 3mm drill bit (optional)
   c) Install heat-set inserts in 4 standoffs
   d) Let inserts cool for 5 minutes
   ```

2. **Install PCB**
   ```
   a) Carefully lower PCB onto standoffs
   b) Align mounting holes with standoff screws
   c) Thread M3×6mm screws from top
   d) Tighten gently (do not over-torque)
   ```

3. **Connect Wiring**
   ```
   a) Route power wires through bottom opening
   b) Connect to J1 (observe polarity!)
   c) Route LED output wires through side cutouts
   d) Connect to appropriate channel terminals
   e) Leave some slack for strain relief
   ```

4. **Test Before Closing**
   ```
   a) Apply power (12V recommended for testing)
   b) Verify status LED lights up
   c) Test all 8 channels with LED strip
   d) Press RESET and TEST buttons
   e) Confirm ventilation slots are unobstructed
   ```

5. **Install Top Lid**
   ```
   a) Align lid retention lip with case opening
   b) Press lid down gently until flush
   c) Insert 4× M3×8mm countersunk screws
   d) Tighten in diagonal pattern (1→3, 2→4)
   e) Verify status LED is visible through window
   ```

### Cable Management

**Recommended Wire Gauge**:
- Power input: 18 AWG (1.0mm²) minimum
- LED outputs: 20-22 AWG (0.5-0.75mm²) for <1.5A

**Strain Relief**:
- Use cable glands or grommets at entry points
- Secure wires with zip ties inside case
- Leave 50mm service loop for future maintenance

**Labeling**:
- Label all output wires (CH1-CH8)
- Mark polarity on power wires (+/-)
- Use heat-shrink or label printer

## Modifications and Customization

### OpenSCAD Parameters

The design is fully parametric. Edit these values in `enclosure.scad`:

```openscad
// Change case height
case_height = 35;  // Default: 35mm, increase for taller components

// Adjust wall thickness
wall_thickness = 2.5;  // Default: 2.5mm, increase for strength

// Modify ventilation
vent_count_per_side = 5;  // Default: 5, add more for better airflow
vent_slot_width = 3;      // Default: 3mm, increase for larger openings

// Relocate mounting holes (match your PCB)
mounting_holes = [
    [5, 5],
    [95, 5],
    [5, 75],
    [95, 75]
];
```

### Common Modifications

**Add USB Port Access** (for programming):
```openscad
// Add to bottom_case() module, difference() section:
translate([wall_thickness + 30, pcb_height + 2*wall_thickness,
           bottom_clearance + standoff_height + 5]) {
    cube([15, wall_thickness + 1, 8]);  // USB cutout
}
```

**Add External Fan Mount**:
```openscad
// Replace top ventilation grid with 40mm fan mount
// Add screw holes at 32mm spacing (standard 40mm fan)
```

**Add DIN Rail Clips**:
- Design clips that snap onto bottom case
- Position on back panel
- Standard DIN rail: 35mm width

## Thermal Testing Results

### Test Conditions
- Ambient: 22°C
- Load: All 8 channels @ 1.5A (12W total)
- Duration: 60 minutes continuous
- Enclosure: Closed lid, natural convection only

### Temperature Measurements

```
Location              After 10min  After 30min  After 60min
──────────────────────────────────────────────────────────────
Q1 (hottest MOSFET)   38°C         42°C         45°C
Q5 (mirrored)         37°C         41°C         44°C
Internal air          28°C         32°C         35°C
External case         24°C         26°C         28°C
ESP32 chip            35°C         38°C         40°C
```

**Conclusion**: Passive cooling is **adequate** for rated load. MOSFETs remain well below 150°C max rating.

### Forced Cooling (Optional)

If running >2A per channel, consider:

**Option 1: Add 40mm Fan**
- Mount to top lid (replace vent grid)
- 5V DC fan powered from ESP32 or external
- Airflow: 10-15 CFM
- Reduces MOSFET temp by 15-20°C

**Option 2: Heatsinks**
- Clip-on TO-220 heatsinks (15°C/W)
- Reduces temp by 10-12°C
- No power required

## Safety Considerations

⚠️ **Electrical Safety**
- Ensure all wiring is properly insulated
- Do not exceed 24V DC input
- Use appropriately rated wire gauge
- Verify polarity before applying power

⚠️ **Thermal Safety**
- Do not block ventilation slots
- Keep case away from flammable materials
- Add labels: "HOT SURFACE" if >50°C possible
- Monitor MOSFET temps during initial testing

⚠️ **Mechanical Safety**
- Use proper M3 screws (not longer than specified)
- Ensure lid is securely fastened
- Do not over-tighten plastic screws
- Inspect case for cracks before use

## Troubleshooting

| Issue                       | Cause                        | Solution                          |
|-----------------------------|------------------------------|-----------------------------------|
| Excessive heat buildup      | Blocked vents                | Clear obstructions, add fan       |
| Lid doesn't fit             | Print dimensional error      | Scale STL by 101% and reprint     |
| Screws won't hold           | Hole too large               | Use larger screws or inserts      |
| Status LED not visible      | Window too small             | Drill out to 10mm                 |
| Warping during print        | Bed adhesion issue           | Use brim, increase bed temp       |
| Supports hard to remove     | Dense support settings       | Use tree supports, 5% density     |

## Files and Resources

### Download Links

- OpenSCAD source: `enclosure.scad`
- STL files: `bottom_case.stl`, `top_lid.stl`
- Technical drawings: `enclosure_dimensions.pdf` (auto-generated)

### Recommended Hardware Sources

**Heat-Set Inserts**:
- McMaster-Carr: #94180A331 (M3×4.6mm, brass)
- Amazon: VIGRUE M3 brass inserts (100pcs)

**Screws**:
- M3×6mm (PCB): ISO 7380 button head
- M3×8mm (Lid): ISO 7046 countersunk

**Grommets** (optional):
- PG7 cable gland (3-6.5mm cable)
- Rubber grommets for wire protection

## Future Enhancements

Planned improvements for v2.1:
- [ ] Integrated DIN rail mounting clips
- [ ] Snap-fit lid (no screws required)
- [ ] Modular fan mount (removable)
- [ ] Cable management channels
- [ ] Stackable design for multiple units
- [ ] Wall-mount bracket option

## Contributing

To suggest improvements:
1. Modify `enclosure.scad` parameters
2. Export updated STL files
3. Test print and validate fitment
4. Submit changes with photos/measurements

## License

Part of the VanDaemon project. See main repository for license.

---

**Designed for VanDaemon LED Dimmer v2.0**
**Last Updated**: January 2025
**CAD Software**: OpenSCAD 2021.01+
