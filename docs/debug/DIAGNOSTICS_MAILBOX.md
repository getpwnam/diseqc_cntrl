# Diagnostics Mailbox Reference

## Purpose

Define the runtime diagnostics mailbox system used for early bring-up breadcrumbs, sticky boot-probe results, and SWD-readable status/error words.

This design replaces the old W5500-specific mailbox naming with neutral `cubley_diag_*` symbols because the same channel is now used by CLR startup, managed startup probes, LED helpers, USB console paths, and network bring-up.

## Native Data Structure

Current mailbox words are implemented as global volatile `uint32_t` symbols in:

- `nf-native/cubley_interop.cpp`

### Slots

1. `g_cubley_diag_current_status`
   - Role: transient "latest status" word.
   - Write policy: may be overwritten by any producer using the current-status channel.

2. `g_cubley_diag_last_error`
   - Role: latest error-class word.
   - Write policy: updated by native error paths and CLR diagnostics injection.

3. `g_cubley_diag_boot_probe_status`
   - Role: sticky managed boot hardware probe aggregate.
   - Write policy: latch-once via `DiagnosticsMailbox.NativeTryLatchBootProbe(...)`.
   - Encoding: stage `0xF0`, result = aggregate (`PASS`/`WARN`/`FAIL`), detail = hardware bitmap (`bit0=W5500 bit1=LNBH26 bit2=FRAM`).

4. `g_cubley_diag_clr_status`
   - Role: sticky-ish CLR startup progress channel (written alongside CLR current status in startup helpers).
   - Write policy: written by CLR startup diagnostic emitters.

## Managed Interop Surface

Interop declarations are in:

- `Cubley.Interop/CubleyInteropNative.cs`

### Existing API (transient)

`BringupStatus` remains for compatibility and maps to current/transient slots:

- `NativeSet(uint statusWord)` -> current status
- `NativeGet()` -> current status
- `NativeGetLastNativeError()` -> last error

### New API (sticky boot probe)

`DiagnosticsMailbox` provides boot-probe latch semantics:

- `NativeTryLatchBootProbe(uint statusWord)`
  - Returns `true` on first successful latch.
  - Returns `false` if already latched.
- `NativeGetBootProbe()`
  - Returns latched boot-probe word (or `0` if not yet latched).

Managed startup now latches its bitmap via:

- `DiSEqC_Control/StartupProbe.cs`

## Word Encoding

Mailbox words are 32-bit packed values.

For Tier-0/Tier-1 diagnostics, this section is normative and must stay aligned
with `docs/software/INTEROP_CONTRACT_V1.md`.

### Status word format

`0xD5SSRRDD`

- `0xD5`: status magic
- `SS`: stage
- `RR`: result code (Phase A contract)
- `DD`: detail (Phase A contract: component selector for smoke checks)

Standard Phase A result decode:

- `0`: `ENTER` (entry/running marker)
- `1`: `PASS`
- `2`: `WARN` (aggregate/skipped)
- `14`: `FAIL`
- `15`: `EXCEPTION`

Any other `RR` value is invalid for Phase A check words and must be rejected by readers.

### Normative stage usage (Tier-0/Tier-1)

The stage byte is producer-owned. Readers must interpret stage values in
producer context, because stage ranges can overlap between producers.

- Managed smoke harness (`CubleySmokeTier0`) currently emits:
   - `0xC0` start, `0xC1` Tier-0 checks, `0xC2` Tier-1 checks, `0xCF` final
- Managed startup probe currently emits:
   - `0xE0` managed entry, `0xE1` W5500 probe, `0xE2` LNBH26 probe,
      `0xE3` FRAM probe, `0xEF` aggregate status
- CLR startup producer may emit additional stage values (including values in the
   `0xC*`/`0xD*` region); do not assume those regions are exclusive to managed
   harness producers.
- `0xF0` is reserved for sticky boot-probe aggregate latch stage.

Unlisted stage values are reserved for future producers and must be treated as
unknown stage (not unknown format) if magic/result decode is valid.

`DD` interpretation note:

- For stage `0xF0`, `DD` is a hardware bitmap (`bit0=W5500 bit1=LNBH26 bit2=FRAM`).
- For other documented Phase A check words, `DD` follows the component mapping below.

Phase A detail-byte mapping (`DD`) for smoke checks:

- `1`: UART wire protocol
- `2`: USB CDC serial
- `3`: LED / GPIO
- `4`: FRAM
- `5`: LNBH26
- `6`: W5500
- `7`: DiSEqC transmit path
- `8`: Diagnostics mailbox

`software/nanoFramework/tests/swd_read_bringup_status.sh` and
`software/nanoFramework/tests/swd_read_w5500_diag.sh` both use the shared
`software/nanoFramework/tests/phase_a_result_codec.sh` decoder so SWD/UART-side
readers apply the same rules and fail fast on invalid magic/result codes.

### Error word format

`0xE?OOCCDD`

- Top byte identifies producer family (`0xE0..0xEF`; `0xE1` and `0xE2` observed currently)
- `OO`: operation/opcode
- `CC`: code
- `DD`: detail

Interpretation of opcode/code/detail is subsystem-specific.

Normative reader rule for Tier-0/Tier-1:

- validate top-byte family range (`0xE0..0xEF`) and preserve raw word
- decode opcode/code/detail only when the opcode has a documented decoder path
- do not fail parsing solely because an opcode is unknown

## Ownership Rules

Use these rules to avoid clobber races and ambiguous traces:

1. Boot hardware probe writes only to `g_cubley_diag_boot_probe_status` via latch API.
2. CLR startup diagnostics write to `g_cubley_diag_clr_status` (and may mirror to current status).
3. Runtime breadcrumbs (LED/network/etc.) may write `g_cubley_diag_current_status`.
4. Error paths write `g_cubley_diag_last_error`.
5. Do not clear sticky slots from normal runtime code.

### Reset behavior

- `g_cubley_diag_boot_probe_status` is sticky within a boot session and is
   expected to reset to `0` on reboot/power-cycle before first managed latch.
- `g_cubley_diag_current_status` is transient and may change frequently.
- `g_cubley_diag_clr_status` reflects CLR/startup progression for the current
   boot session.
- `g_cubley_diag_last_error` reflects latest producer error for the current
   boot session.

## SWD Usage

Primary helper scripts:

- `tests/swd_read_bringup_status.sh`
- `tests/swd_read_w5500_diag.sh`
- `tests/tier0_mailbox_reliability_smoke.sh`

### Read all high-level slots

```bash
cd software/nanoFramework
./tests/swd_read_bringup_status.sh
```

Expected output sections:

- `Current status`
- `Boot probe`
- `CLR startup`

### Read network-focused diagnostics

```bash
cd software/nanoFramework
./tests/swd_read_w5500_diag.sh
```

This includes the generic mailbox/error slots plus W5500-specific latches and decode hints.

### Phase C Tier-0 Reliability Smoke

Run repeated reset/read cycles and enforce sticky boot-probe invariants:

```bash
cd software/nanoFramework
./tests/tier0_mailbox_reliability_smoke.sh --cycles 10 --read-count 4
```

Expected behavior:

- Each cycle reports `PASS` with stable `boot_probe` value across repeated reads.
- `boot_probe`, `clr_status`, and `current_status` words retain valid `0xD5` magic and known result-code encoding.
- Final summary reports `fails=0/<cycles>`.

Recommended managed payload for firmware-first smoke campaigns:

- `CubleySmokeTier0` (`software/nanoFramework/CubleySmokeTier0/CubleySmokeTier0.nfproj`)

Build/deploy example:

```bash
cd software/nanoFramework
./toolchain/build-managed.sh build \
   --project CubleySmokeTier0/CubleySmokeTier0.nfproj \
   --deploy --swd --address 0x080C0000 --reset
```

## Build and Compatibility Notes

Interop slot order and checksum must remain aligned between managed and native declarations.

After changing interop methods in `Cubley.Interop` and/or native method table:

1. Build managed assemblies:
   - `./toolchain/build-managed.sh compile`
2. Sync checksum if needed:
   - `./toolchain/interop-checksum.sh --fix --pe DiSEqC_Control/bin/Release/Cubley.Interop.pe`
3. Verify checksum:
   - `./toolchain/interop-checksum.sh --check --pe DiSEqC_Control/bin/Release/Cubley.Interop.pe`

If native interop method signatures changed, rebuild/flash nanoCLR before validating on hardware.

## Troubleshooting

1. `Boot probe` remains `0`
   - Managed startup may not have reached probe completion.
   - Verify `StartupProbe.Main()` executes and no load-time interop mismatch exists.

2. `Current status` appears unrelated to boot probe
   - Expected; this slot is transient and intentionally clobberable.
   - Use `Boot probe` slot for stable hardware-present bitmap.

3. Magic byte mismatch warnings
   - Indicates either uninitialized data, different producer encoding, or stale symbol assumptions in scripts.
   - Re-check symbol names against current ELF.
