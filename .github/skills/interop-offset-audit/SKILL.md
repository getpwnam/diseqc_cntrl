---
name: interop-offset-audit
description: "Use when checking nanoFramework InternalCall bindings, managed/native interop method order, method_lookup index drift, signature mismatches, checksum/version consistency, or after refactors in Cubley.Interop and native interop tables. Keywords: interop offset, method_lookup, InternalCall, BYREF mismatch, native binding audit, W5500 interop map."
---

# Interop Offset Audit

## Purpose

Validate that managed InternalCall declarations and native method tables are still aligned after refactors.

This skill is optimized for this repository's Cubley interop surfaces.

## Primary Targets

- software/nanoFramework/Cubley.Interop/CubleyInteropNative.cs
- software/nanoFramework/Cubley.Interop/Properties/AssemblyInfo.cs
- software/nanoFramework/nf-native/cubley_interop.cpp
- software/nanoFramework/nf-native/w5500_interop.cpp

## When To Use

- After renaming or adding interop methods.
- After changing out parameters, especially BYREF signatures.
- When managed code appears to run but native methods are never hit.
- When mailbox status does not progress as expected.

## Workflow

1. Read managed declarations in CubleyInteropNative.cs in source order.
2. Read native method_lookup order in cubley_interop.cpp.
3. Compare one by one:
   - method name intent
   - parameter shape (plain vs BYREF)
   - return shape
4. Confirm the native assembly identity and version/checksum alignment:
   - AssemblyNativeVersion in managed assembly metadata
   - native table export name/checksum in cubley_interop.cpp
5. Verify native function symbols exist with the expected names in the build ELF when available.
6. Produce a concise report:
   - PASS if all slots align
   - FAIL with exact slot index and symbol pair

## Output Format

- Summary: PASS or FAIL
- Findings list:
  - slot index
  - managed declaration
  - native handler
  - mismatch category (order, signature, missing symbol, assembly identity)
- Recommended fix steps (minimal, specific)

## Guardrails

- Do not reorder methods unless mismatch is proven.
- Prefer the smallest change that restores alignment.
- Do not change public managed APIs unless required.
- If build artifacts are missing, still perform static mapping audit and clearly mark runtime checks as pending.
