// =============================================================================
// VANDIMMER-4CH+2A -- two-part printed enclosure, Rev A board
// =============================================================================
// Base tub carries the board and every wall; flat lid drops on top. Approach "A"
// of three considered 2026-09-20: it keeps all six cable openings, both edge
// connectors and the board mounting in one rigid part, at the cost of a bridge
// above the harness slot.
//
// EVERY board dimension comes from board_revA.scad, which extract_board.py
// generates from the KiCad board and its 3D models. Nothing here restates a
// board coordinate, so the case cannot drift out of step with the PCB.
//
// Coordinates: case space. X/Y from the outer corner of the shell, Z 0 at the
// underside of the floor. Board coordinates are converted with bx()/by().
//
// Print: PETG (van interior reaches 40 C+, PLA softens from ~60 C).
//   base  0.2 mm layers, 3 perimeters, no supports needed
//   lid   flat side down, no supports
// =============================================================================

include <board_revA.scad>

$fn = 48;

// ---------------------------------------------------------------- parameters
wall            = 3.0;    // side walls, as specified
floor_t         = 3.0;    // base floor
lid_t           = 3.0;    // lid plate
fit             = 0.6;    // clearance around the board, per side
standoff        = 4.0;    // board underside above the floor: clears TH tails
head            = 17.0;   // free space above the board's top surface

post_d          = 7.0;    // board mounting post outer diameter
post_pilot_d    = 2.5;    // pilot for an M3 self-tapping screw into the post
lid_boss_d      = 10.0;   // corner bosses the lid screws into
lid_pilot_d     = 2.5;    // pilot for M3 self-tapper
lid_screw_d     = 3.4;    // through hole in the lid
lid_screw_head  = 6.4;    // countersink diameter
lip_t           = 1.2;    // lid locating lip
lip_h           = 2.5;
lip_clear       = 0.35;   // lip to inner wall, each side

ear_len         = 12.0;   // van mounting ears, beyond the corner boss
ear_w           = 10.0;
ear_t           = 4.0;
ear_hole_d      = 4.5;    // M4 clearance

// Connector heights. The measured value is the modelled part; *_min covers what
// is actually fitted where that differs, and the larger always wins.
harness_conn_h_min = 9.5;   // Molex 22-05-3021 R/A body -- CONFIRM by measurement
power_conn_h_min   = 12.0;  // Phoenix MC 1,5/2-ST-5.08 mating plug
usb_plug_h         = 7.0;   // USB-C plug overmould
harness_plug_clear = 2.0;   // slack above the harness connectors
opening_margin     = 1.5;   // slop each side of a cable opening

letterbox_pillars  = true;  // ribs across the harness slot, in the gaps between
                            // connectors. They cut the longest bridge from 77 mm
                            // to about 22 mm. Set false for one clear opening.
pillar_clear       = 0.8;   // gap between a pillar and each neighbouring connector.
                            // The gaps themselves are only ~2.5 mm, so this is what
                            // stops a plug body wider than its header fouling a rib.

led_window_d       = 5.0;
vent_w             = 2.6;
vent_gap           = 2.8;

antenna_pad_l      = 42.0;  // marked flat area inside the left wall
antenna_pad_h      = 11.0;
antenna_pad_depth  = 0.4;

show_board         = false; // visual check only, never printed
explode            = 0;     // lift the lid this far in the assembly view

// ------------------------------------------------------------------ derived
inner_w   = pcb_w + 2 * fit;
inner_d   = pcb_h + 2 * fit;
outer_w   = inner_w + 2 * wall;
outer_d   = inner_d + 2 * wall;

z_floor   = floor_t;                    // top of the floor
z_board   = z_floor + standoff;         // board underside
z_top     = z_board + pcb_t;            // board top surface
z_rim     = z_top + head;               // where the lid lands
total_h   = z_rim + lid_t;

// board (x, y) -> case (x, y). Board y runs down the sheet; case Y runs up, so
// the harness edge of the board becomes the front wall at Y = 0.
function bx(x) = wall + fit + x;
function by(y) = wall + fit + (pcb_h - y);

harness_h = max(role_h("harness"), harness_conn_h_min);
power_h   = max(role_h("power_edge"), power_conn_h_min);
usb_h     = max(role_h("usb_edge"), usb_plug_h);

// Harness slot: spans every harness connector, from just below the board to
// clear of the tallest one.
slot_x0 = bx(role_x0("harness")) - opening_margin;
slot_x1 = bx(role_x1("harness")) + opening_margin;
slot_z0 = z_top - 0.8;
slot_z1 = z_top + harness_h + harness_plug_clear;
bridge_h = z_rim - slot_z1;             // material left above the slot

// Power connector opening, back wall.
pwr_x0 = bx(role_x0("power_edge")) - opening_margin - 0.5;
pwr_x1 = bx(role_x1("power_edge")) + opening_margin + 0.5;
pwr_z1 = z_top + power_h + 1.0;

// USB-C opening, left wall. Board y -> case Y, so y1 gives the lower edge.
usb_y0 = by(role_y1("usb_edge")) - opening_margin;
usb_y1 = by(role_y0("usb_edge")) + opening_margin;
usb_z1 = z_top + usb_h;

// Jumper hatch in the lid, over J9/J10.
jmp_x0 = bx(role_x0("jumper")) - 2;
jmp_x1 = bx(role_x1("jumper")) + 2;
jmp_y0 = by(role_y1("jumper")) - 2;
jmp_y1 = by(role_y0("jumper")) + 2;

led_row = part_row("D7");
led_x   = bx(led_row[6]);
led_y   = by(led_row[7]);

// Corner positions for the lid bosses and the van ears. The bosses straddle the
// outer corners: pulled inboard they would foul the board's corners, since the
// cavity is only 0.6 mm larger than the PCB.
corners = [[0, 0], [outer_w, 0], [0, outer_d], [outer_w, outer_d]];

// Ear geometry, derived once and used both by van_ears() and by the reported
// envelope, so the two cannot disagree.
ear_tip      = lid_boss_d / 2 + ear_len;                 // straight part's end
ear_hole_off = ear_tip - ear_hole_d / 2 - 2.0;           // hole centre from corner
ear_reach    = max(ear_tip, ear_hole_off + ear_w / 2);   // furthest material in X
boss_reach   = lid_boss_d / 2;                           // furthest material in Y

// Sanity checks on the derived geometry. These are cheap and they fire at render
// time, before anything is exported or printed.
assert(bridge_h >= 3.0,
       str("only ", bridge_h, " mm of wall left above the harness slot -- raise head"));
assert(slot_z1 > z_top + harness_conn_h_min,
       "harness slot does not clear the fitted connector height");
assert(z_rim > z_top + all_parts_h(),
       "lid would sit on the tallest part on the board");
assert(len(pcb_holes) == 4, "expected four board mounting holes");

// --------------------------------------------------------------------- parts
module board_mock() {
    color("green", 0.45)
        translate([bx(0), by(pcb_h), z_board]) cube([pcb_w, pcb_h, pcb_t]);
    for (p = pcb_parts)
        color("dimgray", 0.6)
            translate([bx(p[2]), by(p[5]), z_top])
                cube([p[3] - p[2], p[5] - p[4], p[8]]);
}

module rounded_slab(w, d, h, r) {
    hull()
        for (x = [r, w - r], y = [r, d - r])
            translate([x, y, 0]) cylinder(h = h, r = r);
}

module shell_profile(h) {
    union() {
        rounded_slab(outer_w, outer_d, h, 3);
        for (c = corners) translate([c[0], c[1], 0]) cylinder(h = h, d = lid_boss_d);
    }
}

module vent_slots(x0, x1, y0, y1, h) {
    n = floor((y1 - y0) / (vent_w + vent_gap));
    for (i = [0 : max(n - 1, 0)])
        translate([x0, y0 + i * (vent_w + vent_gap), -0.1])
            hull() {
                translate([vent_w / 2, vent_w / 2, 0]) cylinder(h = h + 0.2, d = vent_w);
                translate([x1 - x0 - vent_w / 2, vent_w / 2, 0]) cylinder(h = h + 0.2, d = vent_w);
            }
}

module board_posts() {
    for (hole = pcb_holes)
        translate([bx(hole[0]), by(hole[1]), z_floor - 0.01])
            difference() {
                cylinder(h = standoff + 0.01, d = post_d);
                translate([0, 0, -0.1]) cylinder(h = standoff + 1, d = post_pilot_d);
            }
}

module van_ears() {
    // One ear per corner, reaching outward in X from the corner boss, at floor
    // level so the lid never touches them. The screw head stays reachable with
    // the case closed, and no fastener enters the sealed volume.
    for (c = corners) {
        dir = (c[0] == 0) ? -1 : 1;
        x_in = c[0] + dir * (lid_boss_d / 2 - 1.0);   // start inside the boss
        x_out = c[0] + dir * ear_tip;
        hole_x = c[0] + dir * ear_hole_off;
        // Pull the ear inboard in Y so it never reaches past the shell: centred on
        // the corner it added ear_w to the case's depth.
        cy = (c[1] == 0) ? ear_w / 2 : c[1] - ear_w / 2;
        difference() {
            hull() {
                translate([min(x_in, x_out), cy - ear_w / 2, 0])
                    cube([abs(x_out - x_in), ear_w, ear_t]);
                translate([hole_x, cy, 0]) cylinder(h = ear_t, d = ear_w);
            }
            translate([hole_x, cy, -0.1]) cylinder(h = ear_t + 0.2, d = ear_hole_d);
        }
    }
}

module harness_slot() {
    translate([slot_x0, -1, slot_z0])
        cube([slot_x1 - slot_x0, wall + 2, slot_z1 - slot_z0]);
}

module harness_pillars() {
    // One rib in each gap between neighbouring connectors, 0.6 mm narrower than
    // the gap so a connector body never fouls it.
    xs = [for (p = parts_with("harness")) p[2]];
    xe = [for (p = parts_with("harness")) p[3]];
    n = len(xs);
    for (i = [0 : n - 2]) {
        g0 = xe[i];
        g1 = xs[i + 1];
        gw = g1 - g0 - 2 * pillar_clear;
        if (gw >= 0.8)
            translate([bx(g0 + pillar_clear), -0.5, slot_z0])
                cube([gw, wall + 1, slot_z1 - slot_z0]);
    }
}

module base() {
    difference() {
        union() {
            shell_profile(z_rim);
            van_ears();
        }

        // cavity
        translate([wall, wall, z_floor]) cube([inner_w, inner_d, z_rim - z_floor + 1]);

        // harness slot, front wall
        harness_slot();

        // power connector, back wall
        translate([pwr_x0, outer_d - wall - 1, slot_z0])
            cube([pwr_x1 - pwr_x0, wall + 2, pwr_z1 - slot_z0]);

        // USB-C, left wall
        translate([-1, usb_y0, slot_z0])
            cube([wall + 2, usb_y1 - usb_y0, usb_z1 - slot_z0]);

        // lid screw pilots
        for (c = corners)
            translate([c[0], c[1], z_rim - 12]) cylinder(h = 13, d = lid_pilot_d);

        // antenna pad: a marked, flat recess inside the left wall, clear of the
        // USB opening and of the button headers
        translate([wall - antenna_pad_depth, outer_d - 6 - antenna_pad_l,
                   z_top + 3])
            cube([antenna_pad_depth + 0.01, antenna_pad_l, antenna_pad_h]);

        // back-wall vents, away from the power connector
        for (i = [0 : 5])
            translate([pwr_x1 + 8 + i * 8, outer_d - wall - 1, z_top + 4])
                cube([2.5, wall + 2, 8]);

        // Channel labels under the harness slot. The cut has to BREAK THE OUTER
        // SURFACE: starting it inside the wall leaves a skin over the glyphs and
        // engraves a sealed void instead, which renders identically and shows up
        // only as extra shells in the mesh. Hence y from +0.8 down past 0.
        for (p = parts_with("harness"))
            translate([bx(p[6]), 0.8, slot_z0 - 5.2])
                rotate([90, 0, 0])
                    linear_extrude(height = 1.0)
                        text(harness_label(p[0]), size = 3.6, halign = "center",
                             valign = "center", font = "Liberation Sans:style=Bold");
    }

    board_posts();
    if (letterbox_pillars) harness_pillars();
}

function harness_label(ref) =
    ref == "J3" ? "1" : ref == "J4" ? "2" : ref == "J5" ? "3" : ref == "J6" ? "4" :
    ref == "J7" ? "A1" : ref == "J8" ? "A2" : ref;

module lid() {
    difference() {
        union() {
            translate([0, 0, z_rim]) shell_profile(lid_t);
            // locating lip
            translate([wall + lip_clear, wall + lip_clear, z_rim - lip_h])
                difference() {
                    cube([inner_w - 2 * lip_clear, inner_d - 2 * lip_clear, lip_h]);
                    translate([lip_t, lip_t, -0.5])
                        cube([inner_w - 2 * lip_clear - 2 * lip_t,
                              inner_d - 2 * lip_clear - 2 * lip_t, lip_h + 1]);
                }
        }

        // jumper access hatch
        translate([jmp_x0, jmp_y0, z_rim - lip_h - 1])
            cube([jmp_x1 - jmp_x0, jmp_y1 - jmp_y0, lid_t + lip_h + 2]);

        // status LED window
        translate([led_x, led_y, z_rim - 1]) cylinder(h = lid_t + 2, d = led_window_d);

        // vents over the buck cluster, which is ~70% of the board's 2.5-3 W, and
        // over the MOSFET row
        translate([0, 0, z_rim])
            vent_slots(bx(role_x0("hot")) + 2, bx(role_x1("hot")) - 2,
                       by(role_y1("hot")) + 2, by(role_y0("hot")) - 2, lid_t);
        translate([0, 0, z_rim])
            vent_slots(bx(role_x0("fets")) + 2, bx(role_x1("fets")) - 2,
                       by(role_y1("fets")) + 2, by(role_y0("fets")) - 2, lid_t);

        // lid screws, countersunk
        for (c = corners) translate([c[0], c[1], z_rim - 1]) {
            cylinder(h = lid_t + 2, d = lid_screw_d);
            translate([0, 0, lid_t + 1 - (lid_screw_head - lid_screw_d) / 2])
                cylinder(h = (lid_screw_head - lid_screw_d) / 2 + 0.01,
                         d1 = lid_screw_d, d2 = lid_screw_head);
        }

        // legend
        translate([outer_w / 2, 12, z_rim + lid_t - 0.6])
            linear_extrude(height = 1)
                text("VANDIMMER 4CH+2A", size = 6, halign = "center",
                     font = "Liberation Sans:style=Bold");
        translate([outer_w / 2, 4, z_rim + lid_t - 0.6])
            linear_extrude(height = 1)
                text("REV A", size = 4, halign = "center",
                     font = "Liberation Sans:style=Bold");
    }
}

// ------------------------------------------------------------------- reporting
// verify_case.py measures the rendered mesh and compares it against these, so the
// numbers below are the only place the case's intent is stated.
function join(l, sep) = len(l) == 0 ? "" :
    len(l) == 1 ? l[0] : str(l[0], sep, join([for (i = [1:len(l) - 1]) l[i]], sep));

function q(k, v) = str("\"", k, "\":", v);
function qs(k, v) = str("\"", k, "\":\"", v, "\"");

harness_xs = [for (p = parts_with("harness")) p[2]];
harness_xe = [for (p = parts_with("harness")) p[3]];

function part_json(p) = str("{",
    join([qs("ref", p[0]), qs("role", p[1]),
          q("cx0", bx(p[2])), q("cx1", bx(p[3])),
          q("cy0", by(p[5])), q("cy1", by(p[4])),
          q("h", p[8])], ","), "}");

params_json = str("{", join([
    q("wall", wall), q("floor_t", floor_t), q("lid_t", lid_t),
    q("inner_w", inner_w), q("inner_d", inner_d),
    q("outer_w", outer_w), q("outer_d", outer_d), q("total_h", total_h),
    q("z_floor", z_floor), q("z_board", z_board), q("z_top", z_top), q("z_rim", z_rim),
    q("bridge_h", bridge_h), q("slot_x0", slot_x0), q("slot_x1", slot_x1),
    q("slot_z0", slot_z0), q("slot_z1", slot_z1),
    q("harness_h", harness_h), q("power_h", power_h),
    q("post_d", post_d), q("lip_clear", lip_clear), q("lip_h", lip_h),
    q("ear_reach", ear_reach), q("ear_t", ear_t), q("boss_reach", boss_reach),
    q("bx0", bx(0)), q("by0", by(pcb_h)),
    q("letterbox_pillars", letterbox_pillars ? "true" : "false"),
    q("parts", str("[", join([for (p = pcb_parts) part_json(p)], ","), "]")),
    q("holes_case", str("[", join([for (h = pcb_holes)
        str("[", bx(h[0]), ",", by(h[1]), ",", h[2], "]")], ","), "]")),
    q("harness_gaps", str("[", join([for (i = [0:len(harness_xs) - 2])
        str("[", bx(harness_xe[i]), ",", bx(harness_xs[i + 1]), "]")], ","), "]"))
], ","), "}");

// ----------------------------------------------------------------- selection
// Set on the command line: -D part="base" | "lid" | "lid_asm" | "assembly" | "params"
part = "assembly";

if (part == "params") {
    echo(str("PARAMS ", params_json));
    cube(0.01);                       // OpenSCAD wants geometry to render
} else if (part == "base") base();
else if (part == "lid") {
    // print orientation: flipped, lip upward, engraved legend on the bed
    translate([0, 0, z_rim + lid_t]) rotate([180, 0, 0]) lid();
} else if (part == "lid_asm") lid();
else if (part == "gauge") {
    // Fit gauge: the front strip of the base -- both front mounting posts, the
    // whole harness slot with its pillars, the corner bosses and the front ears.
    // A couple of hours on the printer settles the hole pitch, the slot height and
    // whether a fitted plug clears the pillars, before the full print commits.
    intersection() {
        base();
        translate([-ear_reach - 1, -boss_reach - 1, -1])
            cube([outer_w + 2 * ear_reach + 2, 19 + boss_reach, z_rim + 2]);
    }
} else if (part == "section") {
    // Cutaway for review: front half of the assembly with the board in place.
    difference() {
        union() {
            base();
            lid();
            board_mock();
        }
        translate([-40, outer_d / 2, -5]) cube([220, outer_d, 60]);
    }
} else {
    base();
    translate([0, 0, explode]) lid();
    if (show_board) board_mock();
}
