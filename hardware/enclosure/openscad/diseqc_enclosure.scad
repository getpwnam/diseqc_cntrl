// DiSEqC controller printable enclosure (base + lid)
// Units: mm

$fn = 64;

// -----------------------------------------------------------------------------
// Board and case envelope
// -----------------------------------------------------------------------------
pcb_w = 90.0;   // X (left-right)
pcb_l = 100.0;  // Y (front-back)
pcb_t = 1.6;

xy_clearance = 1.0;   // free space around PCB edges
wall = 2.4;
floor_t = 2.4;
top_clearance = 18.0; // free height above PCB top

// PCB support ledges (no mounting-hole assumptions)
ledge_depth = 1.6;
ledge_drop = 0.8;     // ledge is slightly below PCB bottom
standoff_h = 5.0;     // PCB bottom Z above base floor

// Lid behavior
lid_t = 2.4;
lid_lip_h = 5.0;
lid_gap = 0.3;        // fit clearance between lid lip and base

// Output mode: "base", "lid", or "preview"
mode = "preview";

// -----------------------------------------------------------------------------
// Derived dimensions
// -----------------------------------------------------------------------------
inner_w = pcb_w + 2 * xy_clearance;
inner_l = pcb_l + 2 * xy_clearance;
inner_h = standoff_h + pcb_t + top_clearance;

outer_w = inner_w + 2 * wall;
outer_l = inner_l + 2 * wall;
base_h = floor_t + inner_h;

// Useful references in case coordinates
board_x0 = wall + xy_clearance;
board_y0 = wall + xy_clearance;
pcb_bottom_z = floor_t + standoff_h;

// -----------------------------------------------------------------------------
// Connector cutouts
// Coordinates are PCB-space values translated from the KiCad board:
// board outline 50..140 (X), 50..150 (Y), so y_from_board_bottom = y - 50.
// Tune these after a quick fit print.
// -----------------------------------------------------------------------------

// J1 Barrel Jack (left wall)
J1_enable = true;
J1_y = 25.125;
J1_z = pcb_bottom_z + 2.8;
J1_h = 8.2;
J1_w = 12.0;

// J4 USB-C (left wall)
J4_enable = true;
J4_y = 79.665;
J4_z = pcb_bottom_z + 1.6;
J4_h = 4.2;
J4_w = 11.0;

// J8 RJ45 (left wall)
J8_enable = true;
J8_y = 61.19;
J8_z = pcb_bottom_z + 6.4;
J8_h = 14.5;
J8_w = 16.8;

// J6/J7 SMA pair (right wall) as one slot for tolerance
J67_enable = true;
J67_y = 76.26;
J67_z = pcb_bottom_z + 5.8;
J67_h = 18.0;
J67_w = 30.0;

// -----------------------------------------------------------------------------
// Geometry helpers
// -----------------------------------------------------------------------------
module rounded_box(size_xyz, r) {
    x = size_xyz[0];
    y = size_xyz[1];
    z = size_xyz[2];
    hull() {
        for (sx = [r, x - r]) {
            for (sy = [r, y - r]) {
                translate([sx, sy, 0]) cylinder(h = z, r = r);
            }
        }
    }
}

module left_cutout(y_center, z_center, w_y, h_z) {
    translate([-0.1, wall + y_center - w_y / 2, z_center - h_z / 2])
        cube([wall + 0.2, w_y, h_z]);
}

module right_cutout(y_center, z_center, w_y, h_z) {
    translate([outer_w - wall - 0.1, wall + y_center - w_y / 2, z_center - h_z / 2])
        cube([wall + 0.2, w_y, h_z]);
}

module pcb_ledges() {
    z0 = pcb_bottom_z - ledge_drop;

    // Long ledges, split around side cutout zones.
    translate([wall, wall, z0]) cube([ledge_depth, inner_l, ledge_drop + pcb_t + 0.6]);
    translate([outer_w - wall - ledge_depth, wall, z0]) cube([ledge_depth, inner_l, ledge_drop + pcb_t + 0.6]);

    // End stops to keep board from sliding in Y.
    translate([wall + ledge_depth, wall, z0]) cube([inner_w - 2 * ledge_depth, ledge_depth, ledge_drop + pcb_t + 0.6]);
    translate([wall + ledge_depth, outer_l - wall - ledge_depth, z0]) cube([inner_w - 2 * ledge_depth, ledge_depth, ledge_drop + pcb_t + 0.6]);
}

module base_shell() {
    difference() {
        rounded_box([outer_w, outer_l, base_h], 3.0);
        translate([wall, wall, floor_t]) cube([inner_w, inner_l, inner_h + 0.2]);

        if (J1_enable) left_cutout(J1_y, J1_z, J1_w, J1_h);
        if (J4_enable) left_cutout(J4_y, J4_z, J4_w, J4_h);
        if (J8_enable) left_cutout(J8_y, J8_z, J8_w, J8_h);
        if (J67_enable) right_cutout(J67_y, J67_z, J67_w, J67_h);
    }

    pcb_ledges();
}

module lid_shell() {
    // Plate
    difference() {
        rounded_box([outer_w, outer_l, lid_t], 3.0);

        // Optional lightweighting pocket.
        translate([wall + 8, wall + 8, -0.1])
            rounded_box([inner_w - 16, inner_l - 16, lid_t - 0.9], 2.0);
    }

    // Locating lip
    translate([0, 0, -lid_lip_h])
    difference() {
        rounded_box([outer_w, outer_l, lid_lip_h], 2.2);
        translate([wall - lid_gap, wall - lid_gap, -0.1])
            rounded_box([inner_w + 2 * lid_gap, inner_l + 2 * lid_gap, lid_lip_h + 0.2], 1.8);
    }
}

module assembly_preview() {
    color([0.86, 0.86, 0.9]) base_shell();

    // Simple PCB proxy for visual checks.
    color([0.0, 0.4, 0.1, 0.5])
        translate([board_x0, board_y0, pcb_bottom_z])
            cube([pcb_w, pcb_l, pcb_t]);

    color([0.8, 0.82, 0.84, 0.6])
        translate([0, 0, base_h + 8])
            lid_shell();
}

if (mode == "base") {
    base_shell();
} else if (mode == "lid") {
    lid_shell();
} else {
    assembly_preview();
}
