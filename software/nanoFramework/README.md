# DiSEqC Satellite Dish Controller

**Automated satellite dish positioning and LNB control via MQTT**

## Quick Start

See [QUICK_START.md](QUICK_START.md) for build instructions.

## Scope

This folder contains:
- managed application code (`DiseqC/`),
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

`build.sh` supports three profiles via `NF_BUILD_PROFILE`:

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

- Minimal: `docker compose run --rm nanoframework-build /work/build.sh`
- W5500-native: `docker compose run --rm -e NF_BUILD_PROFILE=w5500-native nanoframework-build /work/build.sh`
- Network: `docker compose run --rm -e NF_BUILD_PROFILE=network nanoframework-build /work/build.sh`

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
