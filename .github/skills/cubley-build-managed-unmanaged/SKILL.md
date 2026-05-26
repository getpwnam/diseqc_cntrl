---
name: cubley-build-managed-unmanaged
description: "Use when running end-to-end managed + native build/deploy for M0DMF_CUBLEY_F407, including interop checksum sync, firmware rebuild, payload deployment, and diagnostics mailbox verification. Keywords: compile-managed, build.sh cubley-stable, interop-checksum, st-flash, latest.deploy.bin, diagnostics mailbox, boot probe bitmap."
---

# Cubley Managed+Native Build/Deploy Workflow

## Purpose

Run a deterministic workflow for this repo when both managed and native code may have changed, and verify the board is actually running the expected mailbox-enabled image.

## Primary Targets

- software/nanoFramework/toolchain/compile-managed.sh
- software/nanoFramework/toolchain/build.sh
- software/nanoFramework/toolchain/interop-checksum.sh
- software/nanoFramework/tests/swd_read_bringup_status.sh
- software/nanoFramework/docs/BRINGUP_TEST_LOG.md

## When To Use

- Any change touching `Cubley.Interop` interop declarations.
- Any change touching `nf-native/*.cpp` or target override startup code.
- Any change touching managed startup/probe flow (`StartupProbe`, `Program`, config boot path).
- When board behavior does not match code expectations and stale firmware is suspected.

## Preconditions

1. ST-Link connected and visible.
2. Board power stable.
3. Build host has required tools (`msbuild`, `arm-none-eabi-*`, `st-flash`, `openocd`, `gdb-multiarch`).
4. From `software/nanoFramework/` unless noted otherwise.

## Canonical Full Flow

1. Build managed assemblies and deterministic deploy bundle:
   - `./toolchain/compile-managed.sh`

2. Validate interop checksum alignment:
   - `./toolchain/interop-checksum.sh --check --pe DiSEqC_Control/bin/Release/Cubley.Interop.pe`

3. If checksum mismatch is reported, fix then re-check:
   - `./toolchain/interop-checksum.sh --fix --pe DiSEqC_Control/bin/Release/Cubley.Interop.pe`
   - `./toolchain/interop-checksum.sh --check --pe DiSEqC_Control/bin/Release/Cubley.Interop.pe`

4. Rebuild firmware profile:
   - `./toolchain/build.sh cubley-stable`

5. Flash fixed firmware regions:
   - `st-flash write build/nanoBooter.bin 0x08000000`
   - `st-flash write build/nanoCLR.bin 0x08004000`
   - `st-flash reset`

6. Flash managed payload deployment region:
   - `st-flash write DiSEqC_Control/bin/Release/latest.deploy.bin 0x080C0000`
   - `st-flash reset`

7. Verify diagnostics mailbox:
   - `./tests/swd_read_bringup_status.sh`

8. Append factual run result:
   - `./toolchain/bringup_log_append.sh --result PASS|FAIL|INFO --conclusion "..." --commands "..." --artifact "..."`

## Quick Managed-Only Refresh (No Native Rebuild)

Use only when native binary compatibility is known unchanged.

1. `./toolchain/compile-managed.sh`
2. `st-flash write DiSEqC_Control/bin/Release/latest.deploy.bin 0x080C0000`
3. `st-flash reset`
4. `./tests/swd_read_bringup_status.sh`

If startup stalls or mailbox values are unexpected, switch to Canonical Full Flow.

## Verification Criteria

1. `compile-managed.sh` ends with `Managed compile succeeded.`
2. `build.sh cubley-stable` ends with `Build SUCCESS!`.
3. `st-flash` verifies booter/CLR/payload writes.
4. Mailbox script reports sane values with `0xD5` status magic for active diagnostic channels.
5. Boot probe slot eventually latches non-zero when startup reaches probe completion.

## Known Failure Signatures

1. Interop checksum mismatch:
   - Symptom: check step reports mismatch between AssemblyInfo and native table.
   - Action: run `interop-checksum.sh --fix` and re-check.

2. CLR resolve pointer values in mailbox (magic not `0xD5`):
   - Symptom: current/error words look like flash pointers (e.g., `0x080c....`).
   - Action: decode missing assembly and ensure bundle includes required `.pe` dependency.

3. Boot probe remains zero:
   - Symptom: `g_cubley_diag_boot_probe_status == 0` after reset/deploy.
   - Action: confirm CLR startup progresses, then inspect managed assembly dependency set and startup entry path.

## Guardrails

- Never flash payload into booter/CLR regions.
- Do not skip checksum validation after interop signature/table changes.
- Treat `latest.deploy.bin` as authoritative payload artifact for SWD deployment.
- Record every significant run in `docs/BRINGUP_TEST_LOG.md`.
