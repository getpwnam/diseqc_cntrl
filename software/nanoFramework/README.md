# Cubley nanoFramework Workspace

This directory contains the managed and native integration pieces used for Cubley firmware and managed deployment workflows.

## Primary Managed Application

Current primary app:

- CubleyControl

Project path:

- `CubleyControl/CubleyControl.nfproj`

Interop dependency:

- `CubleyNative.Interop/Cubley.Interop.nfproj`

## Managed Build Wrapper

Use:

- `./toolchain/build-CubleyControl.sh`

Default behavior:

- Targets `CubleyControl/CubleyControl.nfproj`
- Runs package bootstrap/restore
- Runs compile or build via nanoFramework MSBuild toolchain
- Runs interop guard/checksum preflight by default in this wrapper

Enable interop preflight checks when needed:

- `./toolchain/build-CubleyControl.sh compile --enable-interop-validation`

## Common Commands

List managed projects:

- `./toolchain/build-CubleyControl.sh list`

Compile only:

- `./toolchain/build-CubleyControl.sh compile --configuration Debug`

Full build:

- `./toolchain/build-CubleyControl.sh build --configuration Debug`

Build and deploy via serial:

- `./toolchain/build-CubleyControl.sh build --deploy --serialport /dev/ttyUSB0 --address 0x08060000`

Build and deploy via SWD:

- `./toolchain/build-CubleyControl.sh build --deploy --swd --address 0x08060000`

These addresses are for the Debug layout. Verify `firmware/nf-interpreter/build/nanoCLR.map` before using another firmware configuration.

## Build Outputs

For target `CubleyControl`, managed outputs are produced under:

- `build/CubleyControl/`

Typical artifacts:

- `CubleyControl.bin`
- `CubleyControl.pe`
- `latest.deploy.bin`

## Firmware Build

Firmware and managed build are separate workflows.

## Notes

- This repository may include historical docs and logs mentioning older managed wrappers.
- The active managed wrapper is now `toolchain/build-CubleyControl.sh`.
