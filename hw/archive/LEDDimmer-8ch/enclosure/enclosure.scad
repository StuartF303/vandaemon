// =====================================================
// VanDaemon LED Dimmer v2.0 - Enclosure Design
// =====================================================
// 3D Printable Case with Thermal Management
// Designed for 100mm × 80mm PCB
//
// Print Settings:
// - Layer Height: 0.2mm
// - Infill: 20%
// - Supports: Yes (for overhangs)
// - Material: PLA, PETG, or ABS
// =====================================================

// Global Parameters
$fn = 50; // Circle resolution

// =====================================================
// CONFIGURATION PARAMETERS - Adjust these as needed
// =====================================================

// PCB Dimensions
pcb_width = 100;
pcb_height = 80;
pcb_thickness = 1.6;

// Case Dimensions
wall_thickness = 2.5;
case_height = 35; // Internal height from PCB to lid
bottom_clearance = 5; // Space below PCB for solder joints

// Mounting
standoff_height = 10; // M3 standoff height
standoff_diameter = 6; // Outer diameter of mounting posts
screw_hole_diameter = 3.2; // M3 screw clearance

// Mounting hole positions (from PCB corner)
mounting_holes = [
    [5, 5],      // Bottom-left
    [95, 5],     // Bottom-right
    [5, 75],     // Top-left
    [95, 75]     // Top-right
];

// Ventilation
vent_slot_width = 3;
vent_slot_length = 40;
vent_slot_spacing = 6;
vent_count_per_side = 5;

// Cutouts
power_connector_width = 12;
power_connector_height = 10;
power_connector_x = 50; // Center bottom

screw_terminal_width = 8;
screw_terminal_height = 12;
screw_terminal_depth = 15; // How deep cutout extends

button_diameter = 7;
button_x_offset = [40, 60]; // Two buttons

status_led_diameter = 8;
status_led_x = 50;

// =====================================================
// MAIN ASSEMBLY
// =====================================================

module main_assembly() {
    // Uncomment one section at a time for printing

    // Bottom case (print this first)
    translate([0, 0, 0]) bottom_case();

    // Top lid (print separately)
    translate([0, pcb_height + 20, 0]) rotate([0, 0, 0]) top_lid();

    // Visualization: PCB outline (not for printing)
    //translate([wall_thickness, wall_thickness, bottom_clearance + standoff_height])
    //    %pcb_outline();
}

// =====================================================
// BOTTOM CASE
// =====================================================

module bottom_case() {
    difference() {
        // Main case body
        union() {
            // Outer shell
            cube([
                pcb_width + 2*wall_thickness,
                pcb_height + 2*wall_thickness,
                bottom_clearance + standoff_height + case_height
            ]);

            // Mounting standoffs
            translate([wall_thickness, wall_thickness, bottom_clearance]) {
                for (hole = mounting_holes) {
                    translate([hole[0], hole[1], 0])
                        mounting_post(standoff_height);
                }
            }
        }

        // Hollow interior
        translate([wall_thickness, wall_thickness, bottom_clearance]) {
            cube([pcb_width, pcb_height, case_height + 10]);
        }

        // Ventilation slots - LEFT SIDE (over MOSFETs Q1-Q4)
        translate([0, wall_thickness + 15, bottom_clearance + standoff_height + 5]) {
            for (i = [0:vent_count_per_side-1]) {
                translate([0, i * vent_slot_spacing, 0])
                    ventilation_slot(vent_slot_length, vent_slot_width, wall_thickness + 1);
            }
        }

        // Ventilation slots - RIGHT SIDE (over MOSFETs Q5-Q8)
        translate([pcb_width + 2*wall_thickness, wall_thickness + 15, bottom_clearance + standoff_height + 5]) {
            rotate([0, 0, 180])
            for (i = [0:vent_count_per_side-1]) {
                translate([0, i * vent_slot_spacing, 0])
                    ventilation_slot(vent_slot_length, vent_slot_width, wall_thickness + 1);
            }
        }

        // Power connector cutout - BOTTOM CENTER
        translate([power_connector_x + wall_thickness - power_connector_width/2,
                   0,
                   bottom_clearance + standoff_height - 2]) {
            cube([power_connector_width, wall_thickness + 1, power_connector_height]);
        }

        // LED Output screw terminals - LEFT SIDE (4 channels)
        for (i = [0:3]) {
            translate([0,
                       wall_thickness + 25 + (i * 15) - screw_terminal_width/2,
                       bottom_clearance + standoff_height - 2]) {
                cube([screw_terminal_depth, screw_terminal_width, screw_terminal_height]);
            }
        }

        // LED Output screw terminals - RIGHT SIDE (4 channels)
        for (i = [0:3]) {
            translate([pcb_width + 2*wall_thickness - screw_terminal_depth,
                       wall_thickness + 25 + (i * 15) - screw_terminal_width/2,
                       bottom_clearance + standoff_height - 2]) {
                cube([screw_terminal_depth, screw_terminal_width, screw_terminal_height]);
            }
        }

        // Button access holes - BOTTOM (through bottom panel)
        for (x_pos = button_x_offset) {
            translate([wall_thickness + x_pos,
                       wall_thickness + 10,
                       0]) {
                cylinder(h = bottom_clearance + 1, d = button_diameter);
            }
        }

        // Lid mounting screw holes (4 corners, countersunk)
        lid_screw_positions = [
            [wall_thickness/2, wall_thickness/2],
            [pcb_width + 1.5*wall_thickness, wall_thickness/2],
            [wall_thickness/2, pcb_height + 1.5*wall_thickness],
            [pcb_width + 1.5*wall_thickness, pcb_height + 1.5*wall_thickness]
        ];

        for (pos = lid_screw_positions) {
            translate([pos[0], pos[1], bottom_clearance + standoff_height + case_height - 5]) {
                // Screw hole
                cylinder(h = 6, d = 3.2);
                // Countersink
                translate([0, 0, 3])
                    cylinder(h = 3, d1 = 3.2, d2 = 6);
            }
        }
    }

    // Text label on front
    translate([wall_thickness + 20, wall_thickness - 0.5, bottom_clearance + standoff_height + case_height/2]) {
        rotate([90, 0, 0])
            linear_extrude(height = 0.8)
                text("LED DIMMER", size = 5, font = "Liberation Sans:style=Bold", halign = "left");
    }
}

// =====================================================
// TOP LID
// =====================================================

module top_lid() {
    difference() {
        union() {
            // Main lid plate
            cube([
                pcb_width + 2*wall_thickness,
                pcb_height + 2*wall_thickness,
                2.5
            ]);

            // Lip for fitting into case
            translate([wall_thickness - 0.2, wall_thickness - 0.2, -3]) {
                difference() {
                    cube([pcb_width + 0.4, pcb_height + 0.4, 3]);
                    translate([0.5, 0.5, 0])
                        cube([pcb_width - 0.6, pcb_height - 0.6, 3.5]);
                }
            }
        }

        // Status LED window
        translate([wall_thickness + status_led_x,
                   wall_thickness + 75,
                   0]) {
            cylinder(h = 3, d = status_led_diameter);
        }

        // Ventilation grid - TOP (multiple rows of slots)
        for (row = [0:6]) {
            translate([wall_thickness + 15,
                       wall_thickness + 15 + (row * 8),
                       0]) {
                for (col = [0:4]) {
                    translate([col * 14, 0, 0])
                        cube([10, 3, 3]);
                }
            }
        }

        // Mounting screw holes (same positions as bottom case)
        lid_screw_positions = [
            [wall_thickness/2, wall_thickness/2],
            [pcb_width + 1.5*wall_thickness, wall_thickness/2],
            [wall_thickness/2, pcb_height + 1.5*wall_thickness],
            [pcb_width + 1.5*wall_thickness, pcb_height + 1.5*wall_thickness]
        ];

        for (pos = lid_screw_positions) {
            translate([pos[0], pos[1], 0]) {
                cylinder(h = 3, d = 3.5);
            }
        }
    }

    // Version label
    translate([wall_thickness + 5, pcb_height + wall_thickness + 1, 2.5]) {
        rotate([0, 0, 0])
            linear_extrude(height = 0.6)
                text("v2.0", size = 4, font = "Liberation Sans:style=Bold", halign = "left");
    }
}

// =====================================================
// COMPONENT MODULES
// =====================================================

module mounting_post(height) {
    difference() {
        cylinder(h = height, d = standoff_diameter);
        // Screw hole (through hole for M3 screw)
        translate([0, 0, -0.5])
            cylinder(h = height + 1, d = screw_hole_diameter);
    }

    // Reinforcement ribs
    for (angle = [0, 90, 180, 270]) {
        rotate([0, 0, angle])
            translate([-0.5, 0, 0])
                cube([1, standoff_diameter/2 + 1, height]);
    }
}

module ventilation_slot(length, width, depth) {
    rotate([0, 90, 0])
        linear_extrude(height = depth)
            hull() {
                translate([0, 0, 0]) circle(d = width);
                translate([length, 0, 0]) circle(d = width);
            }
}

module pcb_outline() {
    // Visualization only - not for printing
    color("green", 0.7)
        cube([pcb_width, pcb_height, pcb_thickness]);
}

// =====================================================
// RENDER
// =====================================================

main_assembly();
