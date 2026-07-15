# Interop Contract (Cubley.Interop)

## Purpose

Define the current InternalCall slot map and compatibility policy for `Cubley.Interop`.
This document is the source of truth for method slot governance.

## Scope

- Managed assembly: `Cubley.Interop`
- Managed declarations: `software/nanoFramework/Cubley.Interop/CubleyInteropNative.cs`
- Native table: `software/nanoFramework/nf-native/cubley_interop.cpp` (`method_lookup`)
- Runtime export: `g_CLR_AssemblyNative_Cubley_Interop`

## Identity

- Assembly name: `Cubley.Interop`
- Native methods checksum (current baseline): `0xB5605EC4`
- Native assembly version tuple: `{ 1, 0, 0, 0 }`

## Normative Tier-0/Tier-1 Diagnostics Semantics (Phase C)

This section is normative for Tier-0/Tier-1 diagnostics behavior consumed by
bring-up tooling and smoke gates.

### Tier Ownership

- Tier-0 APIs: `BringupStatus.*`, `DiagnosticsMailbox.*`
- Tier-1 APIs: `StatusLed.*`, `UsbCdcConsole.*`

Tier-1 APIs must not overwrite Tier-0 sticky diagnostics slot
(`g_cubley_diag_boot_probe_status`).

### Status Word Encoding (Normative)

Status words are encoded as:

- `0xD5SSRRDD`

Where:

- `0xD5` = status magic (required)
- `SS` = stage byte (producer-defined stage)
- `RR` = result code
- `DD` = detail byte

Normative result codes for Tier-0/Tier-1 diagnostics readers:

- `0` = `ENTER`
- `1` = `PASS`
- `2` = `WARN`
- `14` = `FAIL`
- `15` = `EXCEPTION`

Any other result code is invalid for Tier-0/Tier-1 smoke interpretation.

### Error Word Encoding (Normative)

Error words are encoded as:

- `0xE?OOCCDD`

Where:

- top byte `0xE?` identifies producer family
- `OO` = operation/opcode
- `CC` = code
- `DD` = detail

Tier-0/Tier-1 tooling must treat `OO/CC/DD` as producer-specific unless a
decoder table explicitly defines an opcode.

### Reset and Stickiness Rules (Normative)

- `BringupStatus.NativeSet()` writes transient current status.
- `BringupStatus.NativeGet()` reads transient current status.
- `BringupStatus.NativeGetLastNativeError()` reads latest error slot.
- `DiagnosticsMailbox.NativeTryLatchBootProbe(word)` is latch-once per boot:
  first write succeeds (`true`), subsequent writes fail (`false`) and must not
  overwrite the latched value.
- `DiagnosticsMailbox.NativeGetBootProbe()` returns latched value or `0` before
  first latch.
- Sticky latch lifetime is one boot session; device reset/power cycle clears it
  by runtime reinitialization.

## Slot Policy

- Slots `0..22` are immutable in the current baseline.
- Each slot is permanently owned by one fully-qualified API method and cannot be repurposed.
- Existing slots cannot be reordered, deleted, or reused.
- New APIs are append-only and must be added at the end of `method_lookup`.

## Canonical Slot Map

| Slot | API | Managed Signature |
|---:|---|---|
| 0 | `BringupStatus.NativeSet` | `void NativeSet(uint statusWord)` |
| 1 | `BringupStatus.NativeGet` | `uint NativeGet()` |
| 2 | `BringupStatus.NativeGetLastNativeError` | `uint NativeGetLastNativeError()` |
| 3 | `DiagnosticsMailbox.NativeTryLatchBootProbe` | `bool NativeTryLatchBootProbe(uint statusWord)` |
| 4 | `DiagnosticsMailbox.NativeGetBootProbe` | `uint NativeGetBootProbe()` |
| 5 | `LNBH26.NativeInit` | `int NativeInit()` |
| 6 | `LNBH26.NativeSetEnable` | `int NativeSetEnable(bool enable)` |
| 7 | `LNBH26.NativeReadStatus` | `int NativeReadStatus(out int statusRegister)` |
| 8 | `LNBH26.NativeSetVoltage` | `int NativeSetVoltage(int voltage)` |
| 9 | `LNBH26.NativeSetPolarization` | `int NativeSetPolarization(int polarization)` |
| 10 | `LNBH26.NativeSetTone` | `int NativeSetTone(bool enable)` |
| 11 | `LNBH26.NativeSetBand` | `int NativeSetBand(int band)` |
| 12 | `LNBH26.NativeGetVoltage` | `int NativeGetVoltage()` |
| 13 | `LNBH26.NativeGetTone` | `bool NativeGetTone()` |
| 14 | `LNBH26.NativeGetPolarization` | `int NativeGetPolarization()` |
| 15 | `LNBH26.NativeGetBand` | `int NativeGetBand()` |
| 16 | `StatusLed.NativeInit` | `void NativeInit()` |
| 17 | `StatusLed.NativeSetHigh` | `void NativeSetHigh()` |
| 18 | `StatusLed.NativeSetLow` | `void NativeSetLow()` |
| 19 | `StatusLed.NativePulse` | `void NativePulse(int count, int pulseMs)` |
| 20 | `UsbCdcConsole.NativeIsEnabled` | `bool NativeIsEnabled()` |
| 21 | `UsbCdcConsole.NativeReadByte` | `int NativeReadByte(int timeoutMs)` |
| 22 | `UsbCdcConsole.NativeWrite` | `int NativeWrite(string text)` |

## Ownership Rules

- `Cubley.Interop` maintainers own managed declaration order and signature stability.
- `nf-native` maintainers own native symbol implementation and one-to-one table alignment.
- Any change touching `CubleyInteropNative.cs` or `method_lookup` requires explicit interop review and contract check.

## Update Protocol

1. Propose change and classify it as compatible append or breaking.
2. Run static slot audit: managed declaration order vs native `method_lookup` order.
3. Recompute and verify native methods checksum from build output.
4. Update this document only after code change is validated.
5. Append new rows at the end only. Do not modify rows `0..22`.

## Verification Pointers

- Validate managed declarations in `software/nanoFramework/Cubley.Interop/CubleyInteropNative.cs`.
- Validate native table order in `software/nanoFramework/nf-native/cubley_interop.cpp`.
- Validate runtime export checksum/version in `g_CLR_AssemblyNative_Cubley_Interop`.