# Quick Start Guide

## Build (Docker)

From `software/nanoFramework/`:

1. Build firmware (default `minimal` profile):
	- `docker compose run --rm nanoframework-build /work/build.sh`
2. Build firmware (`w5500-native` scaffold profile):
	- `docker compose run --rm -e NF_BUILD_PROFILE=w5500-native nanoframework-build /work/build.sh`
3. Build firmware (`network` profile, deprecated transitional path):
	- `docker compose run --rm -e NF_BUILD_PROFILE=network nanoframework-build /work/build.sh`
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
