# Interop Contract v1 (Cubley.Interop)

## Purpose

Define the immutable v1 InternalCall slot map and compatibility policy for `Cubley.Interop`.
This document is the source of truth for method slot governance in v1.x.

## Scope

- Managed assembly: `Cubley.Interop`
- Managed declarations: `software/nanoFramework/Cubley.Interop/CubleyInteropNative.cs`
- Native table: `software/nanoFramework/nf-native/cubley_interop.cpp` (`method_lookup`)
- Runtime export: `g_CLR_AssemblyNative_Cubley_Interop`

## Identity

- Assembly name: `Cubley.Interop`
- Native methods checksum (v1 baseline): `0xC5EF91C9`
- Native assembly version tuple: `{ 1, 0, 0, 0 }`

## Slot Policy (v1.x)

- Slots `0..33` are immutable in v1.x.
- Each slot is permanently owned by one fully-qualified API method and cannot be repurposed.
- Existing slots cannot be reordered, deleted, or reused.
- New APIs in v1.x are append-only and must be added at the end of `method_lookup`.
- Any signature change that would alter metadata ordering or checksum is a breaking change and is not allowed in v1.x.

## Canonical Slot Map (v1)

| Slot | API | Managed Signature |
|---:|---|---|
| 0 | `BringupStatus.NativeSet` | `void NativeSet(uint statusWord)` |
| 1 | `BringupStatus.NativeGet` | `uint NativeGet()` |
| 2 | `BringupStatus.NativeGetLastNativeError` | `uint NativeGetLastNativeError()` |
| 3 | `DiagnosticsMailbox.NativeTryLatchBootProbe` | `bool NativeTryLatchBootProbe(uint statusWord)` |
| 4 | `DiagnosticsMailbox.NativeGetBootProbe` | `uint NativeGetBootProbe()` |
| 5 | `W5500Socket.NativeOpen` | `int NativeOpen(out int socketHandle)` |
| 6 | `W5500Socket.NativeConfigureNetwork` | `int NativeConfigureNetwork(string localIp, string subnetMask, string gateway, string macAddress)` |
| 7 | `W5500Socket.NativeConnect` | `int NativeConnect(int socketHandle, string host, int port, int timeoutMs)` |
| 8 | `W5500Socket.NativeSend` | `int NativeSend(int socketHandle, byte[] buffer, int offset, int count, out int bytesSent)` |
| 9 | `W5500Socket.NativeReceive` | `int NativeReceive(int socketHandle, byte[] buffer, int offset, int count, int timeoutMs, out int bytesRead)` |
| 10 | `W5500Socket.NativeClose` | `int NativeClose(int socketHandle)` |
| 11 | `W5500Socket.NativeIsConnected` | `bool NativeIsConnected(int socketHandle)` |
| 12 | `W5500Socket.NativeGetPhyStatus` | `uint NativeGetPhyStatus()` |
| 13 | `W5500Socket.NativeGetVersion` | `uint NativeGetVersion()` |
| 14 | `W5500Socket.NativeGetVersionPhyStatus` | `uint NativeGetVersionPhyStatus()` |
| 15 | `W5500Socket.NativeSetPhyMode` | `uint NativeSetPhyMode(int modeCode)` |
| 16 | `LNBH26.NativeInit` | `int NativeInit()` |
| 17 | `LNBH26.NativeSetEnable` | `int NativeSetEnable(bool enable)` |
| 18 | `LNBH26.NativeReadStatus` | `int NativeReadStatus(out int statusRegister)` |
| 19 | `LNBH26.NativeSetVoltage` | `int NativeSetVoltage(int voltage)` |
| 20 | `LNBH26.NativeSetPolarization` | `int NativeSetPolarization(int polarization)` |
| 21 | `LNBH26.NativeSetTone` | `int NativeSetTone(bool enable)` |
| 22 | `LNBH26.NativeSetBand` | `int NativeSetBand(int band)` |
| 23 | `LNBH26.NativeGetVoltage` | `int NativeGetVoltage()` |
| 24 | `LNBH26.NativeGetTone` | `bool NativeGetTone()` |
| 25 | `LNBH26.NativeGetPolarization` | `int NativeGetPolarization()` |
| 26 | `LNBH26.NativeGetBand` | `int NativeGetBand()` |
| 27 | `StatusLed.NativeInit` | `void NativeInit()` |
| 28 | `StatusLed.NativeSetHigh` | `void NativeSetHigh()` |
| 29 | `StatusLed.NativeSetLow` | `void NativeSetLow()` |
| 30 | `StatusLed.NativePulse` | `void NativePulse(int count, int pulseMs)` |
| 31 | `UsbCdcConsole.NativeIsEnabled` | `bool NativeIsEnabled()` |
| 32 | `UsbCdcConsole.NativeReadByte` | `int NativeReadByte(int timeoutMs)` |
| 33 | `UsbCdcConsole.NativeWrite` | `int NativeWrite(string text)` |

## Ownership Rules

- `Cubley.Interop` maintainers own managed declaration order and signature stability.
- `nf-native` maintainers own native symbol implementation and one-to-one table alignment.
- Any change touching `CubleyInteropNative.cs` or `method_lookup` requires explicit interop review and contract check.

## Update Protocol

1. Propose change and classify it as compatible append or breaking.
2. Run static slot audit: managed declaration order vs native `method_lookup` order.
3. Recompute and verify native methods checksum from build output.
4. Update this document only after code change is validated.
5. For v1.x, append new rows at the end only. Do not modify rows `0..33`.

## Verification Pointers

- Validate managed declarations in `software/nanoFramework/Cubley.Interop/CubleyInteropNative.cs`.
- Validate native table order in `software/nanoFramework/nf-native/cubley_interop.cpp`.
- Validate runtime export checksum/version in `g_CLR_AssemblyNative_Cubley_Interop`.