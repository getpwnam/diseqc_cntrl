# Interop Versioning Policy (v1.x)

## Purpose

Define compatibility and review rules for `Cubley.Interop` in the v1 major line.

## Policy Scope

- Managed declarations in `software/nanoFramework/Cubley.Interop/CubleyInteropNative.cs`
- Native lookup/export in `software/nanoFramework/nf-native/cubley_interop.cpp`
- Build guard scripts in `software/nanoFramework/toolchain/interop-guard.sh` and `software/nanoFramework/toolchain/interop-checksum.sh`

## Compatibility Rules (v1.x)

- Existing v1 slots are immutable and cannot be changed, removed, repurposed, or reordered.
- Non-append edits to existing slots are breaking and prohibited in v1.x.
- New methods are allowed only as append-only entries at the tail of the table.
- `AssemblyNativeVersion` checksum and native export checksum must remain aligned.

## What Is Compatible

- Adding a new InternalCall as a new trailing slot, with matching managed/native declarations.
- Non-interop internal implementation changes that do not change method ordering/signatures.

## What Is Breaking

- Reordering methods in `CubleyInteropNative.cs`.
- Inserting a method in the middle of existing slots.
- Deleting or renaming existing InternalCall methods.
- Changing signatures in a way that changes metadata order/checksum without coordinated major-version policy.

## Enforcement

- `interop-guard.sh` enforces managed/native slot alignment and immutable v1 baseline prefix.
- `interop-checksum.sh` enforces checksum alignment between managed metadata and native export.
- `build-managed.sh` and `build-native.sh` run these checks as hard preflight gates.

## Review Requirements

Every interop change PR must include:

1. Slot-impact statement (`no slot change` or `append-only slot addition`).
2. Guard output showing PASS from both scripts.
3. Contract update in `INTEROP_CONTRACT_V1.md` if any append occurred.

## Versioning Notes

- v1.x is append-only.
- Any intentional non-append change requires a future major policy decision and a new contract/version line.