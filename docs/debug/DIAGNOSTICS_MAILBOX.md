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
   - Role: sticky managed boot hardware probe result.
   - Write policy: latch-once via `DiagnosticsMailbox.NativeTryLatchBootProbe(...)`.

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

### Status word format

`0xD5SSRRDD`

- `0xD5`: status magic
- `SS`: stage
- `RR`: result code
- `DD`: detail

Typical result decode used by scripts:

- `0`: RUNNING
- `1`: PASS
- `2`: WARN
- `14`: FAIL
- `15`: EXCEPTION

### Error word format

`0xE?OOCCDD`

- Top byte identifies producer family (`0xE1` or `0xE2` seen currently)
- `OO`: operation/opcode
- `CC`: code
- `DD`: detail

Interpretation of opcode/code/detail is subsystem-specific.

## Ownership Rules

Use these rules to avoid clobber races and ambiguous traces:

1. Boot hardware probe writes only to `g_cubley_diag_boot_probe_status` via latch API.
2. CLR startup diagnostics write to `g_cubley_diag_clr_status` (and may mirror to current status).
3. Runtime breadcrumbs (LED/network/etc.) may write `g_cubley_diag_current_status`.
4. Error paths write `g_cubley_diag_last_error`.
5. Do not clear sticky slots from normal runtime code.

## SWD Usage

Primary helper scripts:

- `tests/swd_read_bringup_status.sh`
- `tests/swd_read_w5500_diag.sh`

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
