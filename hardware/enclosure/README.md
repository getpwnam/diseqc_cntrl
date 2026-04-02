# 3D Printed Enclosure (OpenSCAD)

This folder contains a first-pass printable enclosure for the DiSEqC controller PCB.

## Files

- `openscad/diseqc_enclosure.scad`
  - Parametric base + lid model.
  - Includes side-wall connector apertures for:
    - J1 barrel jack
    - J4 USB-C
    - J8 RJ45
    - J6/J7 SMA pair

## Known PCB Geometry Source

The enclosure defaults are based on:

- Board outline rectangle in KiCad: 90 x 100 mm
- Connector centroid positions from the PCB layout and production placement data

## Build Steps

1. Open `openscad/diseqc_enclosure.scad` in OpenSCAD.
2. Set `mode = "base"` and export STL.
3. Set `mode = "lid"` and export STL.
4. Slice with your preferred settings.

## Suggested Print Settings

- Material: PETG or ASA for outdoor/temperature resilience
- Layer height: 0.2 mm
- Perimeters: 4
- Top/Bottom layers: 5
- Infill: 20-30% gyroid
- Supports: none for base, optional for lid pocket depending on slicer

## Fit Calibration Workflow

1. Print base only.
2. Test PCB seating on ledges.
3. Check connector alignment in side apertures.
4. Tune these variables in the SCAD file:
   - `J1_y`, `J1_z`, `J1_w`, `J1_h`
   - `J4_y`, `J4_z`, `J4_w`, `J4_h`
   - `J8_y`, `J8_z`, `J8_w`, `J8_h`
   - `J67_y`, `J67_z`, `J67_w`, `J67_h`
5. Re-export and reprint base.
6. Once cutouts align, print lid.

## Notes

- This model intentionally uses PCB edge ledges instead of screw standoffs, because mounting-hole coordinates are not yet defined in this design package.
- If you want screw-down mounting, add hole coordinates from KiCad and convert ledges to standoffs in a follow-up revision.
