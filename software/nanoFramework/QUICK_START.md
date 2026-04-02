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

Managed full-build note:

- `toolchain/build-chain.sh`, `toolchain/build-managed-cli.sh`, and `toolchain/compile-blink-test.sh` include Linux metadata-processor workarounds.
- If you still hit metadata processor errors, use the dedicated script outputs and post the failing command/output.

## Build (Firmware Profiles)

From `software/nanoFramework/`:

1. Build firmware (default `minimal` profile):
	- `./toolchain/build.sh minimal`
2. Build firmware (`w5500-native` scaffold profile):
	- `./toolchain/build.sh w5500-native`
3. Build firmware (`bringup-smoke` hardware proof profile):
	- `./toolchain/build.sh bringup-smoke`
4. Build firmware (`bringup-hardalive` bare-metal proof profile):
	- `./toolchain/build.sh bringup-hardalive`
5. Build firmware (`network` profile, deprecated transitional path):
	- `./toolchain/build.sh network`
6. Confirm output artifacts exist in `build/`:
	- `nanoCLR.bin`
	- `nanoCLR.hex`
	- `nanoCLR.elf`

## Build Managed Test App (BlinkBringup)

From `software/nanoFramework/`:

1. Build managed test app:
	- `./toolchain/compile-blink-test.sh`
2. Confirm artifact exists:
	- `tests/BlinkBringup/bin/Release/BlinkBringup.pe`

## Flash

- `st-flash write build/nanoBooter.bin 0x08000000`
- `st-flash write build/nanoCLR.bin 0x08004000`

## Verify Basic Command Path

- Example command publish:
  - `mosquitto_pub -t diseqc/command/halt -m ''`

For complete procedures, see [docs/guides/TESTING_GUIDE.md](docs/guides/TESTING_GUIDE.md).
