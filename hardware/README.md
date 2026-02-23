# Hardware Domain

This directory contains the board design and fabrication assets for the DiSEqC controller.

## Contents

- `kicad-project/`
  - Source-of-truth schematics and PCB layout.
- `gerber/`
  - Fabrication outputs for PCB manufacturing.
- `nonfree/`
  - Third-party footprints/models/symbols and their licenses.
- `LICENSE.txt`
  - Hardware licensing information.

## Working Rules

- Treat KiCad files in `kicad-project/` as canonical.
- Regenerate Gerbers from the matching KiCad revision before manufacturing.
- Keep hardware-specific docs/assets in `hardware/` to avoid cross-domain drift.
