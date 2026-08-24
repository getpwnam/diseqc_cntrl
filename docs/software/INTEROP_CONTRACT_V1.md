# Interop Contract (CubleyNative v1)

## Purpose

Define the frozen nanoFramework InternalCall dispatch map for `CubleyNative`.
The runtime dispatch index is the PE `MethodDef` index, not managed source order.

## Scope

- Managed assembly: `CubleyNative`
- Managed declarations: `software/nanoFramework/CubleyNative.Interop/CubleyInteropNative.cs`
- Canonical native table: `firmware/targets-local/CUBLEY_F407_0_5/nanoCLR/cubley_interop.cpp`
- Native CLR wrappers: `firmware/targets-local/CUBLEY_F407_0_5/nanoCLR/*_interop.cpp`
- Runtime export: `g_CLR_AssemblyNative_CubleyNative`

The corresponding community-target tree is generated during the firmware build and is not an edit source.

## Identity

- Assembly name: `CubleyNative`
- Managed and native assembly version: `1.0.0.0`
- Native methods checksum: `0x55A991DA`
- Frozen slot count: 26 (`0..25`)

## PE Ordering

The nanoFramework MetadataProcessor orders methods by declaring type name
(ordinal alphabetical order), then by declaration order within each type.
The native `method_lookup[]` table must follow that emitted order exactly.

The current type order is:

1. `DiagMailbox`
2. `Fram24C128`
3. `LNBH26`
4. `LNBH26Registers`
5. `LNBH26Tweaks`
6. `UsbCdcConsole`

## Canonical Slot Map

| Slot | API | Managed Signature |
|---:|---|---|
| 0 | `DiagMailbox.NativeSet` | `void NativeSet(uint statusWord)` |
| 1 | `DiagMailbox.NativeGet` | `uint NativeGet()` |
| 2 | `DiagMailbox.NativeGetLastNativeError` | `uint NativeGetLastNativeError()` |
| 3 | `Fram24C128.NativeInit` | `int NativeInit()` |
| 4 | `Fram24C128.NativeWrite` | `int NativeWrite(int address, byte[] buffer, int offset, int count)` |
| 5 | `Fram24C128.NativeRead` | `int NativeRead(int address, byte[] buffer, int offset, int count)` |
| 6 | `LNBH26.NativeInit` | `int NativeInit()` |
| 7 | `LNBH26.NativeSetEnable` | `int NativeSetEnable(bool enable)` |
| 8 | `LNBH26.NativeReadStatus` | `int NativeReadStatus(out int statusRegister)` |
| 9 | `LNBH26.NativeReadStatusPair` | `int NativeReadStatusPair(out int status1, out int status2)` |
| 10 | `LNBH26.NativeSetPolarizationForChannel` | `int NativeSetPolarizationForChannel(int channel, int polarization)` |
| 11 | `LNBH26.NativeSetBandForChannel` | `int NativeSetBandForChannel(int channel, int band)` |
| 12 | `LNBH26.NativeSetLowPowerForChannel` | `int NativeSetLowPowerForChannel(int channel, bool lowPower)` |
| 13 | `LNBH26.NativeSetDiseqcInputModeForChannel` | `int NativeSetDiseqcInputModeForChannel(int channel, int mode)` |
| 14 | `LNBH26.NativeGetPolarizationForChannel` | `int NativeGetPolarizationForChannel(int channel)` |
| 15 | `LNBH26.NativeGetBandForChannel` | `int NativeGetBandForChannel(int channel)` |
| 16 | `LNBH26.NativeGetLastError` | `int NativeGetLastError()` |
| 17 | `LNBH26.NativeGetLastErrorDetail` | `int NativeGetLastErrorDetail()` |
| 18 | `LNBH26Registers.NativeReadRegister` | `int NativeReadRegister(int registerAddress, out int registerValue)` |
| 19 | `LNBH26Tweaks.NativeSetIsetLowForChannel` | `int NativeSetIsetLowForChannel(int channel, bool lowRange)` |
| 20 | `LNBH26Tweaks.NativeSetIswLowForChannel` | `int NativeSetIswLowForChannel(int channel, bool lowLimit)` |
| 21 | `LNBH26Tweaks.NativeGetIsetLowForChannel` | `int NativeGetIsetLowForChannel(int channel)` |
| 22 | `LNBH26Tweaks.NativeGetIswLowForChannel` | `int NativeGetIswLowForChannel(int channel)` |
| 23 | `UsbCdcConsole.NativeIsEnabled` | `int NativeIsEnabled()` |
| 24 | `UsbCdcConsole.NativeReadByte` | `int NativeReadByte(int timeoutMs)` |
| 25 | `UsbCdcConsole.NativeWrite` | `int NativeWrite(string text)` |

`UsbCdcConsole.NativeIsEnabled` intentionally returns `int` because a Boolean
InternalCall return can trigger a nanoFramework CLR eval-stack assertion.

## Diagnostics Semantics

- `DiagMailbox.NativeSet()` writes the transient current status word.
- The first `DiagMailbox.NativeGet()` after boot returns the reset-cause word.
- Later `DiagMailbox.NativeGet()` calls return the transient current status word.
- `DiagMailbox.NativeGetLastNativeError()` returns the latest native subsystem detail word.
- Status words conventionally use `0xD5SSRRDD`; native error words use producer-specific `0xE?OOCCDD` encodings.

The reset-cause overload is part of the frozen v1 behavior. A future contract
should expose reset cause through a dedicated API rather than extending this ambiguity.

## Compatibility Rule

Slots `0..25` are immutable. A v1 addition is compatible only when the emitted
PE order preserves all 26 existing slots as an exact prefix and places new slots
after slot 25. Generic source-level "append-only" edits are not sufficient:
adding a method to an earlier alphabetically ordered type shifts later slots.

Any change that renames, reorders, removes, repurposes, or shifts a frozen slot
requires a coordinated new contract version and matching managed/native deployment.

## Verification

From `software/nanoFramework/` run:

```sh
./toolchain/interop-guard.sh
./toolchain/interop-checksum.sh --check --assembly CubleyNative
./toolchain/interop-negative-drift-test.sh
```

The guard models PE type ordering and rejects changes to the frozen prefix. The
checksum check validates source checksum and assembly-version alignment, and the
negative test proves an intentional baseline mutation is blocked.