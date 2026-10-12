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

- `./toolchain/build-CubleyControl.sh build --deploy --serialport /dev/ttyUSB0 --address 0x080C0000 --reset`

Build and deploy via SWD:

- `./toolchain/build-CubleyControl.sh build --deploy --swd --address 0x080C0000 --reset`

The current deployment region is `0x080C0000` through `0x080FFFFF`. Always
verify `__deployment_start__` and `__deployment_size__` in
`firmware/nf-interpreter/build/nanoCLR.map` before using an address explicitly.

For a reproducible command-line deployment, run the build wrapper and then
`./toolchain/deploy-CubleyControl.sh --reset`. A local VS Code nanoFramework
launch configuration may provide the same build-and-deploy flow. Choose one
deployment workflow and do not alternate between them during one bring-up
session.

For certificate provisioning and the first trusted API request, follow
[`docs/software/HTTPS_TRANSPORT.md`](../../docs/software/HTTPS_TRANSPORT.md).

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
