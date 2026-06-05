---
name: cubley-test-board-flash-deploy
description: "Use when interacting with the M0DMF_CUBLEY_F407 test board: verify the fixed flash map, flash nanoBooter and nanoCLR, deploy a managed payload by SWD or nanoff, and confirm the board reports the expected runtime state. Keywords: Cubley test board, M0DMF_CUBLEY_F407, nanoBooter flash, nanoCLR flash, payload flash, managed deployment, st-flash, nanoff, flash layout."
---

# Cubley Test Board Flash/Deploy

## Purpose

Run the standard board-level workflow for the M0DMF_CUBLEY_F407 test board when you need to rebuild, reflash, deploy, or sanity-check managed payloads.

## Fixed Flash Layout

- `nanoBooter`: `0x08000000` to `0x08003FFF` (16 KB)
- `nanoCLR`: `0x08004000` to `0x080BFFFF` (752 KB total runtime region)
- `payload` / managed deployment region: `0x080C0000` to `0x080FFFFF` (256 KB)

Treat these addresses as fixed for this board unless the firmware linker layout is intentionally changed and the docs are updated with it.

## Primary Targets

- `software/nanoFramework/build/nanoBooter.bin`
- `software/nanoFramework/build/nanoCLR.bin`
- Managed payload image, for example:
  - `software/nanoFramework/DiSEqC_Control/bin/Release/latest.deploy.bin`
- `software/nanoFramework/toolchain/build-managed.sh build`
- `software/nanoFramework/toolchain/interop-checksum.sh`

## Preconditions

1. Board connected over ST-Link.
2. UART connected if using `nanoff` deploy or `--devicedetails` verification.
3. Firmware artifacts already built when flashing:
   - `build/nanoBooter.bin`
   - `build/nanoCLR.bin`
4. Managed payload already compiled when deploying:
   - board app `.bin`, `.pe`, or `latest.deploy.bin` as appropriate.

## Exact Flash Commands

From `software/nanoFramework/`:

1. Flash `nanoBooter` to its fixed address:
   - `st-flash write build/nanoBooter.bin 0x08000000`

2. Flash `nanoCLR` to its fixed address:
   - `st-flash write build/nanoCLR.bin 0x08004000`

3. Reset after firmware flashing:
   - `st-flash reset`

4. Flash a managed payload directly into the deployment region by SWD when you want a transport-independent write:
   - `st-flash write DiSEqC_Control/bin/Release/latest.deploy.bin 0x080C0000`

5. Reset after payload flashing:
   - `st-flash reset`

## Managed Payload Deploy Alternatives

Use `nanoff` when the wire protocol is healthy and you want the runtime to own deployment handling:

- `nanoff --nanodevice --serialport /dev/ttyUSB0 --baud 115200 --deploy --image DiSEqC_Control/bin/Release/latest.deploy.bin --address 0x080C0000 --reset`

Use the repo helper when you want build + optional deploy from one entry point:

- `./toolchain/build-managed.sh build --deploy --serialport /dev/ttyUSB0 --address 0x080C0000`

Decision rule:

- Flash `nanoBooter` and `nanoCLR` with `st-flash`.
- Deploy payload with `nanoff` when UART transport is working.
- Deploy payload with `st-flash` to `0x080C0000` when UART deploy is flaky or you want a known-good SWD fallback.

## Canonical Board Refresh Flow

1. Verify interop checksum before mixing native and managed changes:
   - `./toolchain/interop-checksum.sh --check`

2. Flash firmware:
   - `st-flash write build/nanoBooter.bin 0x08000000`
   - `st-flash write build/nanoCLR.bin 0x08004000`
   - `st-flash reset`

3. Deploy payload to the fixed deployment region:
   - `nanoff --nanodevice --serialport /dev/ttyUSB0 --baud 115200 --deploy --image DiSEqC_Control/bin/Release/latest.deploy.bin --address 0x080C0000 --reset`
   - or `st-flash write <payload-image> 0x080C0000`

4. Verify board state:
   - `nanoff --nanodevice --serialport /dev/ttyUSB0 --baud 115200 --devicedetails`

## Expected Signals

- `nanoff --devicedetails` reports `nanoCLR running @ M0DMF_CUBLEY_F407`.
- Managed assemblies appear after payload deployment instead of showing only native assemblies.
- Payload address used for deploy is always `0x080C0000`.

## Guardrails

- Do not flash a managed payload over `0x08000000` or `0x08004000`; those are firmware regions.
- Do not change the deployment address away from `0x080C0000` unless the board flash layout changes first.
- If deployment verification fails, separate transport issues from firmware issues before editing code.
- Prefer rebuild, reflash, and redeploy before assuming a board-level hardware regression.
