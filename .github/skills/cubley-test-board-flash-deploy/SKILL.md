---
name: cubley-test-board-flash-deploy
description: "Use when interacting with the CUBLEY_F407_0_5 test board: derive the active flash layout from build artifacts, flash nanoBooter and nanoCLR, deploy CubleyControl, and confirm runtime state. Keywords: Cubley test board, nanoBooter flash, nanoCLR flash, payload flash, managed deployment, st-flash, nanoff, flash layout."
---

# Cubley Test Board Flash/Deploy

## Purpose

Build, flash, deploy, and verify `CUBLEY_F407_0_5` using the layout encoded by
the artifacts being flashed.

## Source of Truth

Derive addresses from the generated files before every firmware flash:

- `firmware/nf-interpreter/build/nanoBooter.map`
- `firmware/nf-interpreter/build/nanoCLR.map`
- `firmware/nf-interpreter/build/nanoBooter.elf`
- `firmware/nf-interpreter/build/nanoCLR.elf`

Generated maps are the primary source. ELF program headers and symbols are the
independent fallback and cross-check because they travel with the linked image.
Checked-in linker scripts, build-script defaults, configuration names, and
historical layout tables are supporting evidence only.

Raw `.bin` files contain bytes but no load address. Use them only to determine
image size. Never infer an address from a filename or from Debug/Release labels.

## Layout Discovery

Run this procedure after every build and before every flash:

1. If maps are present, inspect each map's `Memory Configuration` and deployment
   symbols:
   - `grep -A20 '^Memory Configuration' firmware/nf-interpreter/build/nanoBooter.map`
   - `grep -A20 '^Memory Configuration' firmware/nf-interpreter/build/nanoCLR.map`
   - `grep -E '__deployment_(start|size)__' firmware/nf-interpreter/build/nanoCLR.map`
2. Read linked load addresses from the ELF program headers. Use these as the
   fallback when maps are absent and as a cross-check when maps are present:
   - `arm-none-eabi-readelf -lW firmware/nf-interpreter/build/nanoBooter.elf`
   - `arm-none-eabi-readelf -lW firmware/nf-interpreter/build/nanoCLR.elf`
   - Use the first flash-resident `LOAD` physical address for each image.
3. If map deployment symbols are unavailable, read them from the ELF symbol
   table:
   - `arm-none-eabi-nm -n firmware/nf-interpreter/build/nanoCLR.elf | grep -E '__deployment_(start|size)__'`
4. Read raw image sizes:
   - `stat -c '%n %s' firmware/nf-interpreter/build/nanoBooter.bin firmware/nf-interpreter/build/nanoCLR.bin`
5. Read the connected target's flash capacity with `st-info --probe`.
6. Calculate image ends as `load_address + bin_size` and verify:
   - nanoBooter does not overlap nanoCLR.
   - nanoCLR does not overlap deployment.
   - deployment start plus deployment size does not exceed target flash.
7. Refuse to flash if available map and ELF addresses disagree, neither source
   provides the required addresses and deployment symbols, an overlap exists,
   or the `.elf` adjacent to a `.bin` is absent.

## Primary Artifacts

- `firmware/nf-interpreter/build/nanoBooter.bin`
- `firmware/nf-interpreter/build/nanoCLR.bin`
- `firmware/nf-interpreter/build/nanoBooter.elf`
- `firmware/nf-interpreter/build/nanoCLR.elf`
- `software/nanoFramework/build/CubleyControl/latest.deploy.bin`

## Refresh Flow

From the repository root:

1. Build firmware:
   - `./firmware/build-flash-cubley.sh build`
2. Run the complete Layout Discovery procedure above and record the derived
   booter, CLR, deployment start, and deployment size values.
3. Flash using explicit values derived from the current artifacts:
   - `./firmware/build-flash-cubley.sh flash --bootaddr <booter-start> --clraddr <clr-start> --deployaddr <deployment-start> --deploysize <deployment-size> --reset`
4. Build the managed bundle:
   - `cd software/nanoFramework`
   - `./toolchain/build-CubleyControl.sh build --project CubleyControl/CubleyControl.nfproj --configuration Debug`
5. Deploy over the wire protocol:
   - `./toolchain/deploy-CubleyControl.sh --reset`
6. Verify identity:
   - `nanoff --nanodevice --serialport /dev/ttyUSB0 --baud 921600 --devicedetails`

For a direct SWD payload fallback, write `latest.deploy.bin` only at the
deployment start derived from the current map or ELF symbols. Confirm the bundle
size fits within the derived deployment size before writing.

## Expected Signals

- Device details reports `nanoCLR running @ CUBLEY_F407_0_5`.
- All assemblies required by the current managed deployment manifest are listed.
- Native `CubleyNative` version and checksum match the managed build.
- No assembly resolver or link failure is reported.

## Guardrails

- Do not erase deployment unless explicitly required.
- Do not trust hardcoded addresses or build-script defaults; pass derived values
   explicitly to flash commands.
- Do not mix firmware and managed artifacts from different interop checksums.
- Use `/dev/ttyUSB0` only for nanoFramework wire protocol operations.
- Validate user CLI commands on USB CDC separately; that console is not available from the container.
- Record significant flash/deploy/probe runs in `docs/debug/BRINGUP_TEST_LOG.md`.