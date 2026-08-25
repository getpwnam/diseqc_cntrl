# Interop Versioning Policy (v1.x)

## Purpose

Define compatibility and review rules for the `CubleyNative` managed/native contract.

## Policy Scope

- Managed declarations in `software/nanoFramework/CubleyNative.Interop/CubleyInteropNative.cs`
- Canonical native lookup/export in `firmware/targets-local/CUBLEY_F407_0_5/nanoCLR/cubley_interop.cpp`
- Native CLR wrappers in `firmware/targets-local/CUBLEY_F407_0_5/nanoCLR/*_interop.cpp`
- Guards in `software/nanoFramework/toolchain/interop-guard.sh` and `interop-checksum.sh`

## Compatibility Rules

- The 26 slots in `INTEROP_CONTRACT_V1.md` are the immutable v1 prefix.
- Compatibility is determined from emitted PE order, not managed source order.
- A v1 addition is allowed only when all existing slots remain unchanged and new slots follow slot 25.
- Managed and native assembly names, versions, method signatures, and checksums must align.
- The target-local firmware tree is canonical; the community-target copy is generated build output.

Because MetadataProcessor groups methods by alphabetically ordered declaring type,
adding a method to an existing type can shift every later type. Such a change is
breaking even when the declaration is appended within its source file.

## Compatible Changes

- Internal implementation changes that preserve the complete emitted slot map.
- A new InternalCall whose emitted PE slot follows the frozen 26-slot prefix.
- Documentation and guard improvements that do not change assembly identity or slots.

## Breaking Changes

- Renaming, deleting, repurposing, or changing the signature of a frozen method.
- Any source change that inserts or reorders a PE `MethodDef` within slots `0..25`.
- Changing managed or native assembly version on only one side.
- Bypassing checksum validation or deploying managed/native artifacts from different contracts.

Breaking changes require a coordinated contract version, regenerated checksum,
matching managed and native builds, and deployment of both artifacts.

## Enforcement

- `interop-guard.sh` validates native-only declarations, PE dispatch order, and the frozen prefix.
- `interop-checksum.sh` validates checksum and source assembly-version alignment.
- `interop-negative-drift-test.sh` proves a mutated frozen slot is rejected.
- Managed and native build wrappers run the guards as preflight gates.

## Review Requirements

Every interop change PR must include:

1. Slot impact: `no slot change`, `PE-prefix-preserving addition`, or `breaking contract change`.
2. Output from all three enforcement scripts.
3. An updated `INTEROP_CONTRACT_V1.md` when identity or slots intentionally change.
4. For a breaking change, a coordinated firmware and managed deployment plan.

Commit messages for interop changes must be timestamped and include a brief rationale,
as required by the repository interop policy.