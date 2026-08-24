---
name: cubley-build-managed-unmanaged
description: "Use when running end-to-end managed + native build/deploy for CUBLEY_F407_0_5, including interop validation, firmware rebuild, payload deployment, and diagnostics verification. Keywords: build-CubleyControl.sh, build-flash-cubley.sh, interop-checksum, st-flash, latest.deploy.bin, diagnostics mailbox."
---

# Cubley Managed+Native Build/Deploy Workflow

## Purpose

Run a deterministic workflow for this repo when both managed and native code may have changed, and verify the board is actually running the expected mailbox-enabled image.

## Primary Targets

- software/nanoFramework/toolchain/build-CubleyControl.sh
- firmware/build-flash-cubley.sh
- software/nanoFramework/toolchain/interop-checksum.sh
- software/nanoFramework/tests/swd_read_bringup_status.sh
- docs/debug/BRINGUP_TEST_LOG.md

## When To Use

- Any change touching `Cubley.Interop` interop declarations.
- Any change touching `firmware/targets-local/CUBLEY_F407_0_5/` native or startup code.
- Any change touching managed startup/probe flow (`StartupProbe`, `Program`, config boot path).
- When board behavior does not match code expectations and stale firmware is suspected.

## Preconditions

1. ST-Link connected and visible.
2. Board power stable.
3. Build host has required tools (`msbuild`, `arm-none-eabi-*`, `st-flash`, `openocd`, `gdb-multiarch`).
4. Run commands from the repository root unless a step says otherwise.

## Canonical Full Flow

1. Build managed assemblies and deterministic deploy bundle:
   - `cd software/nanoFramework`
   - `./toolchain/build-CubleyControl.sh build --project CubleyControl/CubleyControl.nfproj --configuration Debug`

2. Validate interop checksum alignment:
   - `./toolchain/interop-checksum.sh --check --assembly CubleyNative --pe build/CubleyControl/CubleyNative.pe`

3. If checksum mismatch is reported, fix then re-check:
   - `./toolchain/interop-checksum.sh --fix --assembly CubleyNative --pe build/CubleyControl/CubleyNative.pe`
   - `./toolchain/interop-checksum.sh --check --assembly CubleyNative --pe build/CubleyControl/CubleyNative.pe`

4. Rebuild the canonical firmware target from the repository root:
   - `cd ../..`
   - `./firmware/build-flash-cubley.sh build`

5. Flash the Debug firmware layout after verifying addresses in the generated maps:
   - `./firmware/build-flash-cubley.sh flash --reset`

6. Deploy the managed payload over the wire protocol:
   - `cd software/nanoFramework`
   - `./toolchain/deploy-CubleyControl.sh --reset`

7. Verify diagnostics mailbox:
   - `./tests/swd_read_bringup_status.sh`

8. Append factual run result:
   - `./toolchain/bringup_log_append.sh --result PASS|FAIL|INFO --conclusion "..." --commands "..." --artifact "..."`

## Quick Managed-Only Refresh (No Native Rebuild)

Use only when native binary compatibility is known unchanged.

1. `./toolchain/build-CubleyControl.sh build --project CubleyControl/CubleyControl.nfproj --configuration Debug`
2. `./toolchain/deploy-CubleyControl.sh --reset`
3. `./tests/swd_read_bringup_status.sh`

If startup stalls or mailbox values are unexpected, switch to Canonical Full Flow.

## Verification Criteria

1. `build-CubleyControl.sh build` creates a validated deterministic bundle.
2. `firmware/build-flash-cubley.sh build` produces `nanoBooter.bin` and `nanoCLR.bin` successfully.
3. `st-flash` verifies booter/CLR/payload writes.
4. Mailbox script reports sane values with `0xD5` status magic for active diagnostic channels.

## Known Failure Signatures

1. Interop checksum mismatch:
   - Symptom: check step reports mismatch between AssemblyInfo and native table.
   - Action: run `interop-checksum.sh --fix` and re-check.

2. CLR resolve pointer values in mailbox (magic not `0xD5`):
   - Symptom: current/error words look like flash pointers (e.g., `0x080c....`).
   - Action: decode missing assembly and ensure bundle includes required `.pe` dependency.

## Guardrails

- Never flash payload into booter/CLR regions.
- Do not skip checksum validation after interop signature/table changes.
- Treat `latest.deploy.bin` as authoritative payload artifact for SWD deployment.
- Record every significant run in `docs/debug/BRINGUP_TEST_LOG.md`.
