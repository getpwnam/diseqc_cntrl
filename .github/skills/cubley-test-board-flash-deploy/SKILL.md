---
name: cubley-test-board-flash-deploy
description: "Use when interacting with the CUBLEY_F407_0_5 test board: verify the active linker-map flash layout, flash nanoBooter and nanoCLR, deploy CubleyControl, and confirm runtime state. Keywords: Cubley test board, nanoBooter flash, nanoCLR flash, payload flash, managed deployment, st-flash, nanoff, flash layout."
---

# Cubley Test Board Flash/Deploy

## Purpose

Build, flash, deploy, and verify `CUBLEY_F407_0_5` without mixing Debug and Release flash layouts.

## Source of Truth

Derive addresses from the generated files before every firmware flash:

- `firmware/nf-interpreter/build/nanoBooter.map`
- `firmware/nf-interpreter/build/nanoCLR.map`

The checked-in linker and block-storage definitions are supporting evidence. Generated maps are authoritative for the artifacts being flashed.

## Known Layouts

| Configuration | nanoBooter | nanoCLR | Deployment |
|---|---|---|---|
| Debug | `0x08000000..0x08007FFF` | `0x08008000..0x0805FFFF` | `0x08060000..0x080FFFFF` |
| Release | `0x08000000..0x08003FFF` | `0x08004000..0x0803FFFF` | `0x08040000..0x080FFFFF` |

Never flash nanoCLR at `0x08004000` when using a Debug nanoBooter; that address overlaps the second Debug booter sector.

## Primary Artifacts

- `firmware/nf-interpreter/build/nanoBooter.bin`
- `firmware/nf-interpreter/build/nanoCLR.bin`
- `software/nanoFramework/build/CubleyControl/latest.deploy.bin`

## Debug Refresh Flow

From the repository root:

1. Build and inspect maps:
   - `./firmware/build-flash-cubley.sh build`
2. Flash firmware using Debug defaults:
   - `./firmware/build-flash-cubley.sh flash --reset`
3. Build the managed bundle:
   - `cd software/nanoFramework`
   - `./toolchain/build-CubleyControl.sh build --project CubleyControl/CubleyControl.nfproj --configuration Debug`
4. Deploy over the wire protocol at the Debug deployment start:
   - `./toolchain/deploy-CubleyControl.sh --reset`
5. Verify identity:
   - `nanoff --nanodevice --serialport /dev/ttyUSB0 --baud 921600 --devicedetails`

For a direct SWD payload fallback, write `latest.deploy.bin` at `0x08060000` only after confirming the Debug map.

## Release Firmware Flash

Use `./firmware/build-flash-cubley.sh flash --release-layout --reset` only with artifacts whose generated maps show the Release addresses. Do not select `--release-layout` merely to move existing Debug binaries.

## Expected Signals

- Device details reports `nanoCLR running @ CUBLEY_F407_0_5`.
- All 16 CubleyControl managed assemblies are listed.
- Native `CubleyNative` version and checksum match the managed build.
- No assembly resolver or link failure is reported.

## Guardrails

- Do not erase deployment unless explicitly required.
- Do not mix firmware and managed artifacts from different interop checksums.
- Use `/dev/ttyUSB0` only for nanoFramework wire protocol operations.
- Validate user CLI commands on USB CDC separately; that console is not available from the container.
- Record significant flash/deploy/probe runs in `docs/debug/BRINGUP_TEST_LOG.md`.