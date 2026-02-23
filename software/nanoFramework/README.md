# DiSEqC Satellite Dish Controller

**Automated satellite dish positioning and LNB control via MQTT**

## Quick Start

See [QUICK_START.md](QUICK_START.md) for build instructions.

## Build Components (Build Separately)

This project has two independent build components. Run them separately.

### 1) Managed Application Build (C# project)

Builds the managed application in `DiSEqC_Control/` and validates C# compile health.

Command:

- `./toolchain/compile-managed.sh`

Use this as the local pre-commit gate for managed code changes.

#### Managed Unit Tests (host-only)

Run host-only unit tests for pure managed logic (currently `RuntimeConfiguration` and `ParityHelper`):

- `cd tests/DiSEqC_Control.Tests && dotnet test -v minimal`

Recommended local sequence before commit:

1. `./toolchain/compile-managed.sh`
2. `cd tests/DiSEqC_Control.Tests && dotnet test -v minimal`

### 2) Firmware Build (nf-interpreter / nanoCLR)

Builds firmware artifacts by fetching/using `nf-interpreter` inside Docker and compiling the `M0DMF_DISEQC_F407` target.

Commands:

- Minimal profile: `docker compose run --rm nanoframework-build /work/toolchain/build.sh`
- W5500-native profile: `docker compose run --rm -e NF_BUILD_PROFILE=w5500-native nanoframework-build /work/toolchain/build.sh`
- Network profile (deprecated transitional path): `docker compose run --rm -e NF_BUILD_PROFILE=network nanoframework-build /work/toolchain/build.sh`

Expected firmware outputs in `build/`:

- `nanoCLR.bin`
- `nanoCLR.hex`
- `nanoCLR.elf`

## Optional Local Full Build-Chain Check

This path includes metadata/PE generation and is currently known to fail on this Linux toolchain.

Command:

- `./toolchain/build-chain.sh`

Known limitation:

- Fails in `NFProjectSystem.MDP.targets` when loading `System.Drawing.Common` during `MetaDataProcessorTask`.

Tracking:

- See [TODO.md](TODO.md), section **Current Focus: Build Chain Reliability**.

## Host Prerequisites (Linux)

Before running managed build scripts on Linux, ensure these host tools are available:

- Mono/MSBuild toolchain:
	- `sudo apt update && sudo apt install -y mono-complete`
- `nanoff` CLI:
	- `dotnet tool install -g nanoff` (or `dotnet tool update -g nanoff`)
- Compatibility shim for build flows that still invoke `xbuild`:
	- `sudo ln -sf "$(command -v msbuild)" /usr/local/bin/xbuild`

Sanity check:

- `msbuild --version`
- `xbuild --version`
- `nanoff --help`

Troubleshooting:

- If you see `xbuild: command not found`, recreate the shim:
	- `sudo ln -sf "$(command -v msbuild)" /usr/local/bin/xbuild`

## Scope

This folder contains:
- managed application code (`DiSEqC_Control/`),
- native integration code (`nf-native/`), and
- build orchestration/scripts for generating firmware artifacts.

The upstream `nf-interpreter` codebase is fetched during Docker build; it is not maintained as a first-class directory in this repository.

## Documentation

- [Docs Index](docs/README.md)
- [Docker Build Guide](docs/guides/DOCKER_BUILD_GUIDE.md)
- [Testing Guide](docs/guides/TESTING_GUIDE.md)
- [MQTT API](docs/reference/MQTT_API.md)
- [Architecture](docs/reference/ARCHITECTURE.md)
