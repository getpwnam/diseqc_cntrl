---
name: cubley-flash-usage
description: "Generate current CUBLEY_F407_0_5 logical flash-layout and physical erase-sector usage tables from built nanoBooter, nanoCLR, managed deployment, and configuration estimates. Use when asked for flash blocks, capacities, fill level, free flash, memory map, sector usage, firmware size, or deployment occupancy. Keywords: flash usage, flash table, erase sectors, nanoCLR size, deployment size, configuration fill."
---

# Cubley Flash Usage

## Purpose

Produce the two canonical Markdown flash tables for the current build:

1. Logical regions: nanoBooter, configuration, nanoCLR, and managed deployment.
2. STM32F407 physical erase sectors 0 through 11.

## Command

From the repository root, run:

```bash
python3 tools/report_flash_usage.py
```

If an actual combined device certificate/private-key PEM is available, use its
size instead of the representative 1,067-byte P-256 credential:

```bash
python3 tools/report_flash_usage.py --device-certificate path/to/device.pem
```

For a measured configuration-sector occupancy, override the estimate:

```bash
python3 tools/report_flash_usage.py --config-used <bytes>
```

## Preconditions

Build both firmware and managed artifacts first. The script requires:

- `firmware/nf-interpreter/build/nanoBooter.bin`
- `firmware/nf-interpreter/build/nanoCLR.bin`
- `software/nanoFramework/build/CubleyControl/latest.deploy.bin`

## Interpretation

- Artifact sizes are occupied extents, not counts of non-erased bytes read from hardware.
- Configuration is estimated unless `--config-used` is supplied.
- The default estimate includes the Ethernet record, X.509 header,
  representative P-256 PEM bundle, and complete 512-byte Cubley reservation.
- The script validates the expected target linker and block-storage geometry and
  stops if the source layout no longer matches its STM32F407 assumptions.
- Report script errors rather than reconstructing tables manually.