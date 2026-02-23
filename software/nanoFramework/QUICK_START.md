# Quick Start Guide

## Host Prerequisites (Linux)

Install required host tools once:

1. Mono/MSBuild:
	- `sudo apt update && sudo apt install -y mono-complete`
2. `nanoff` CLI:
	- `dotnet tool install -g nanoff` (or `dotnet tool update -g nanoff`)
3. `xbuild` compatibility shim (for tooling that still expects `xbuild`):
	- `sudo ln -sf "$(command -v msbuild)" /usr/local/bin/xbuild`

Verify:

- `msbuild --version`
- `xbuild --version`
- `nanoff --help`

Troubleshooting:

- If `xbuild` is missing, recreate the shim:
	- `sudo ln -sf "$(command -v msbuild)" /usr/local/bin/xbuild`

## Local Build Health Check (Linux)

From `software/nanoFramework/`:

1. Managed pre-commit compile (recommended current gate):
	- `./toolchain/compile-managed.sh`
2. Full build-chain check (includes metadata processor):
	- `./toolchain/build-chain.sh`

Current known limitation on this Linux toolchain:

- full `msbuild /t:Build` fails in `NFProjectSystem.MDP.targets` due to `System.Drawing.Common` load failure during metadata processing.
- managed compile (`/t:Compile`) remains healthy and is the current reliable gate.

## Build (Docker)

From `software/nanoFramework/`:

1. Build firmware (default `minimal` profile):
	- `docker compose run --rm nanoframework-build /work/toolchain/build.sh`
2. Build firmware (`w5500-native` scaffold profile):
	- `docker compose run --rm -e NF_BUILD_PROFILE=w5500-native nanoframework-build /work/toolchain/build.sh`
3. Build firmware (`network` profile, deprecated transitional path):
	- `docker compose run --rm -e NF_BUILD_PROFILE=network nanoframework-build /work/toolchain/build.sh`
4. Confirm output artifacts exist in `build/`:
	- `nanoCLR.bin`
	- `nanoCLR.hex`
	- `nanoCLR.elf`

## Flash

- `st-flash write build/nanoCLR.bin 0x08000000`

## Verify Basic Command Path

- Example command publish:
  - `mosquitto_pub -t diseqc/command/halt -m ''`

For complete procedures, see [docs/guides/TESTING_GUIDE.md](docs/guides/TESTING_GUIDE.md).
