# Architecture Reference

## Purpose

Define the software boundaries, control flow, and ownership for the `software/nanoFramework` domain.

## Current Build Profile

- Target: `M0DMF_DISEQC_F407`
- Build orchestrator: `toolchain/build.sh`
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
   - Location: `nf-native/diseqc_native.*`, `nf-native/lnb_control.*`, `nf-native/board_diseqc.*`
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

`docker compose run ... /work/toolchain/build.sh` → clone/update `nf-interpreter` → inject target/integration files → CMake/Ninja build → copy artifacts to `build/`

## Hardware Integration Points

- DiSEqC carrier/timing: TIM-based output path (board-configured)
- LNB control: I2C (`LNBH26PQR`)
- Optional network path (when enabled): W5500 over SPI

## Domain Boundaries

- This document describes `software/nanoFramework` only.
- PCB/schematic/fabrication ownership is in `hardware/`.
- `nf-interpreter` is an external dependency; this repo stores integration and build profile decisions, not a full fork layout.

## Related Documents

- `../README.md`
- `../../QUICK_START.md`
- `../guides/DOCKER_BUILD_GUIDE.md`
- `../guides/TESTING_GUIDE.md`
- `MQTT_API.md`
- `CONFIGURATION.md`
- `../hardware/W5500_ETHERNET.md`
- `../hardware/LNB_I2C_TESTING.md`
- `../hardware/MOTOR_ENABLE_NOTES.md`
