# DiSEqC Satellite Dish Controller

**Automated satellite dish positioning and LNB control via MQTT**

## Quick Start

See [QUICK_START.md](QUICK_START.md) for build instructions.

## Build Status (Managed vs Full Build)

Current local status on Linux:

- Managed C# compile is stable and should be used as the pre-commit gate:
	- `./toolchain/compile-managed.sh`
- Full nanoFramework build (PE generation) currently fails in metadata processing:
	- `./toolchain/build-chain.sh`
	- failure point: `NFProjectSystem.MDP.targets` cannot load `System.Drawing.Common` during `MetaDataProcessorTask`

### Known Limitation

On this Linux host/toolchain combination, `msbuild /t:Build` reaches metadata processing and then fails with:

- `Could not load file or assembly 'System.Drawing.Common ...'`

This is a build-chain/runtime dependency issue in the nanoFramework metadata processor path, not a managed code compile error in this project.

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

## Build Notes (M0DMF_DISEQC_F407 target)

`toolchain/build.sh` supports three profiles via `NF_BUILD_PROFILE`:

- `minimal` (default)
	- `System.Net`: OFF
	- `NF_FEATURE_HAS_CONFIG_BLOCK`: OFF
	- current low-risk profile for non-network firmware work

- `network`
	- `System.Net`: ON
	- `NF_FEATURE_HAS_CONFIG_BLOCK`: ON
	- `NF_SECURITY_MBEDTLS`: OFF (size/stability tradeoff)
	- currently compiles and links, but still uses interim compatibility settings while full W5500 runtime validation is in progress
	- **deprecated**: transitional profile kept only until `w5500-native` is validated

- `w5500-native`
	- `System.Net`: OFF
	- `NF_FEATURE_HAS_CONFIG_BLOCK`: OFF
	- intended target profile for direct/native W5500 transport work (no lwIP/internal-MAC path)
	- currently scaffolded for migration

Build commands:

- Minimal: `docker compose run --rm nanoframework-build /work/toolchain/build.sh`
- W5500-native: `docker compose run --rm -e NF_BUILD_PROFILE=w5500-native nanoframework-build /work/toolchain/build.sh`
- Network: `docker compose run --rm -e NF_BUILD_PROFILE=network nanoframework-build /work/toolchain/build.sh`

Common target notes:

- nanoCLR uses a custom serial-only entry point (`build/nanoCLR_main.c`) and does not rely on USB CDC startup.
- Debugger support is enabled.
- Target block storage definitions include `common/Device_BlockStorage.c`.
- nanoCLR linker split is adjusted to prioritize code space:
	- `flash0`: 496 KB
	- `deployment`: 512 KB

### Last validated result

- Docker build completes successfully.
- Artifacts are copied to `build/`:
	- `nanoCLR.bin`
	- `nanoCLR.hex`
	- `nanoCLR.elf`
