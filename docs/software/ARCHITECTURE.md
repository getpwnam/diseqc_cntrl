# Architecture Reference

## Purpose

Define the software boundaries, control flow, and ownership for the `software/nanoFramework` domain.

## Current Build Profile

- Target: `M0DMF_CUBLEY_F407`
- Build orchestrator: `toolchain/build-native.sh`
- Networking (`System.Net`): **disabled** in the currently validated profile
- Firmware outputs: `build/nanoCLR.bin`, `build/nanoCLR.hex`, `build/nanoCLR.elf`

## Layered Model

1. **Application Layer (C#)**
  - Location: `DiSEqC_Control/`
   - Responsibilities:
     - command routing
     - high-level rotor/LNB workflows
     - state publication/integration glue

2. **Interop Layer (C++ bridge)**
   - Location: `nf-native/*_interop.cpp`
   - Responsibilities:
     - expose native functions to managed runtime
     - marshal arguments/results and status codes

3. **Native Driver Layer (C++/ChibiOS integration)**
  - Location: `nf-native/diseqc_native.*`, `nf-native/lnb_control.*`, `nf-native/board_cubley.*`
   - Responsibilities:
     - DiSEqC timing/control primitives
     - LNB I2C control (LNBH26PQR)
     - board-level pin/peripheral configuration

4. **Upstream Runtime Base (external)**
   - Location at build time: cloned `nf-interpreter`
   - Responsibilities:
     - nanoFramework runtime + ChibiOS target machinery
     - linker/toolchain integration

## Runtime Flows

### Control Flow

`Managed command` → `interop` → `native driver` → `board peripheral`

### Build Flow

`docker compose run ... /work/toolchain/build-native.sh` → clone/update `nf-interpreter` → copy static target files into `nf-interpreter/targets/ChibiOS/M0DMF_CUBLEY_F407/` → apply upstream compatibility patches → CMake/Ninja build → copy artifacts to `build/`

## Target File Ownership

All `M0DMF_CUBLEY_F407` target files are statically maintained in this repository under `nf-native/`:

| Directory | Contents |
|---|---|
| `nf-native/` | Board files: `board_cubley.h/cpp`, native drivers, interop sources |
| `nf-native/target-overrides/` | All ChibiOS/nf-interpreter integration files (halconf, chconf, linker scripts, CMakeLists.txt, mcuconf, target_*.c/cpp/h) |

The build script copies these files into the nf-interpreter target directory on each build.  No reference-board fallback files are required.  For the `cubley-stable` profile, `NF_STATIC_AUDIT=1` is enforced by default to verify this invariant at build time.

### Upstream Patches

The build script applies a small number of compatibility patches to nf-interpreter on each build:

- `CMake/Modules/FindChibiOS.cmake` — removes a duplicate include-path entry that causes double-compilation
- `CMake/binutils.common.cmake` — fixes `arm-none-eabi-size` invocation in generated ninja rules
- `targets/ChibiOS/_nanoCLR/CMakeLists.txt` — resets stale `INTERNAL` cache lists on re-configure
- `src/CLR/Startup/CLRStartup.cpp` — injects Cubley SWD-mailbox diagnostic hooks
- `src/CLR/Core/TypeSystem.cpp` — injects Cubley assembly-resolve diagnostic pointers

The first three are upstream bugs/limitations; the last two are board-specific diagnostics added during bring-up.  All patches are idempotent (guarded by `grep -Fq` before applying).

## Hardware Integration Points

- DiSEqC carrier/timing: TIM-based output path (board-configured)
- LNB control: I2C (`LNBH26PQR`)
- Optional network path (when enabled): W5500 over SPI

## Domain Boundaries

- This document describes `software/nanoFramework` only.
- PCB/schematic/fabrication ownership is in `hardware/`.
- `nf-interpreter` is an external dependency; this repo stores integration and build profile decisions, not a full fork layout.

## Related Documents (Software + Debug)

- `../README.md`
- `../debug/TESTING_GUIDE.md`
- `MQTT_API.md`
- `CONFIGURATION.md`
- `../debug/LNB_I2C_TESTING.md`
- `../debug/W5500_LINK_BRINGUP_CHECKLIST.md`
