---
name: w5500-reset-flash-deploy-verify
description: "Use when running the standard W5500 bring-up cycle on M0DMF_CUBLEY_F407: reset, flash nanoCLR, deploy W5500Bringup, verify interop checksum, verify managed assemblies, and confirm mailbox/native-error stage progression. Keywords: w5500 bringup cycle, st-flash deploy, interop checksum check, nanoff devicedetails, swd_read_w5500_diag, version verify, stage progression."
---

# W5500 Reset/Flash/Deploy/Verify

## Purpose

Run a reproducible, minimal-risk workflow for W5500 bring-up validation after small code or wiring changes.

## Primary Targets

- software/nanoFramework/build/nanoCLR.bin
- software/nanoFramework/tests/W5500Bringup/bin/Release/latest.deploy.bin
- software/nanoFramework/tests/swd_read_w5500_diag.sh
- software/nanoFramework/tests/swd_read_bringup_status.sh
- software/nanoFramework/toolchain/interop-checksum.sh

## Preconditions

1. Board connected by ST-Link and UART.
2. Firmware already built (`build/nanoCLR.bin` exists).
3. Managed app already compiled (`latest.deploy.bin` exists).

## Canonical Flow

1. Verify interop checksum consistency:
   - `./toolchain/interop-checksum.sh --check`
   - `./toolchain/interop-checksum.sh --check --pe tests/W5500Bringup/bin/Release/Cubley.Interop.pe`
2. Flash CLR and reset:
   - `st-flash write build/nanoCLR.bin 0x08004000`
   - `st-flash reset`
3. Deploy managed bundle via SWD fallback (preferred when UART deploy is flaky):
   - `st-flash write tests/W5500Bringup/bin/Release/latest.deploy.bin 0x080C0000`
   - `st-flash reset`
4. Verify managed assemblies visible:
   - `nanoff --nanodevice --serialport /dev/ttyUSB0 --baud 115200 --devicedetails`
5. Verify runtime state:
   - `./tests/swd_read_w5500_diag.sh`
   - `./tests/swd_read_bringup_status.sh`

## Expected Signals

- `interop-checksum.sh`: `Checksum OK` for global + PE checks.
- `devicedetails`: `W5500Bringup` and `Cubley.Interop` listed under managed assemblies.
- `swd_read_w5500_diag.sh`:
  - `VERSIONR=0x04` for W5500.
  - For PHY config validation: opcodes `0x44` (pre-soft-reset snapshot) and `0x43` (post-write verification).
- `swd_read_bringup_status.sh`: stage/result progression matches current test objective.

## Decision Rules

- If checksum fails: stop and fix checksum before flashing.
- If managed assemblies missing in `devicedetails`: rebuild managed app and redeploy bundle.
- If mailbox stays at firmware-only stage while managed app should run: treat as deployment/runtime state issue before changing native code.
- If PHY diagnostics regress: capture repeated SWD samples before editing logic.

## Logging

After each coherent run, append one factual entry:
- `./toolchain/bringup_log_append.sh --result PASS|FAIL|INFO --conclusion "..." --commands "..." --artifact "..." --breakpoints "..." --note "..."`

## Guardrails

- Prefer smallest change first (rebuild/redeploy/re-verify) before code edits.
- Do not infer PMODE/PHY conclusions from a single sample; use repeated reads.
- Keep transport failures (UART/SWD) separated from firmware conclusions.
