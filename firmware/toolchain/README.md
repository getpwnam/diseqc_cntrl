# CUBLEY_F407_0_5 Toolchain

This folder contains firmware-local build and gate scripts for phased bring-up.

## Core scripts

- `clean.sh`: clears and recreates `firmware/cubley-f407/out/`.
- `build-native.sh`: canonical firmware-local native build script.
- `gate-wire-protocol.sh`: runs wire-protocol baseline checks using firmware-local toolchain scripts.
- `gate-fram-i2c1.sh`: phase gate wrapper for native FRAM validation.
- `gate-usb-cdc.sh`: phase gate wrapper for USB-CDC console + isolation checks.
- `gate-rmii-lan8742a.sh`: phase gate wrapper for native RMII/LAN8742A validation.

## Environment knobs

Common:
- `OUT_DIR` default: `firmware/cubley-f407/out`
- `LOG_ROOT` default: `firmware/cubley-f407/out/logs`

Wire protocol:
- `SERIAL_PORT` default: `/dev/ttyUSB0`
- `BAUD` default: `115200`
- `CYCLES` default: `5`

Phase commands:
- `FRAM_NATIVE_SMOKE_CMD` required by `gate-fram-i2c1.sh`
- `USB_CDC_SMOKE_CMD` required by `gate-usb-cdc.sh`
- `RMII_NATIVE_SMOKE_CMD` required by `gate-rmii-lan8742a.sh`

## Typical flow

1. `./firmware/cubley-f407/toolchain/clean.sh`
2. `./firmware/cubley-f407/toolchain/build-native.sh build`
3. `./firmware/cubley-f407/toolchain/gate-wire-protocol.sh`

## Notes

- `toolchain/build-native.sh` is the single build entrypoint for this firmware directory.
- It should operate within `firmware/cubley-f407/` without syncing into `software/nanoFramework/nf-native`.
