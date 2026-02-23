# nanoFramework TODO

## Current Focus: Build Chain Reliability

### Current Decision (2026-02-23)

- Use `./toolchain/compile-managed.sh` as the required local/pre-commit quality gate.
- Treat full `msbuild ... /t:Build` on this Linux host as blocked by nanoFramework metadata processor dependency/runtime mismatch.
- Generate deployable PE artifacts from a known-good packaging environment (e.g., Windows CI/dev box) until Linux metadata processor path is fixed.
- Do **not** rely on `libgdiplus` as the primary fix path for the current failure mode.

- [ ] Make full `msbuild ... /t:Build` pass on Linux host (not only `/t:Compile`).
- [ ] Resolve `System.Drawing.Common` load failure in nanoFramework metadata processor (`NFProjectSystem.MDP.targets`).
- [ ] Validate fix path for Linux-hosted metadata processor (current extension bundle includes a Windows-targeted `System.Drawing.Common` assembly).
- [x] Add a reproducible local build wrapper for full build (with required env vars/paths).
- [x] Document known-good build commands and prerequisites in `software/nanoFramework/README.md`.

## Managed Dependency Hygiene

- [x] Remove unused direct references/packages not required by current managed code.
- [x] Keep `System.Net` aligned to M2Mqtt expected version (`1.11.36`) to avoid CS1702 mismatch.
- [ ] Revisit remaining remap warnings only after full build chain is stable.
- [x] Keep `./toolchain/compile-managed.sh` as the safe pre-commit gate until metadata processor issue is fixed.

## Managed/Interop Test Coverage

- [x] Add host-only unit test project for pure managed logic (`tests/DiSEqC_Control.Tests`) and run it on Linux.
- [x] Cover `RuntimeConfiguration` parsing/validation and `ParityHelper` behavior with unit tests.
- [ ] Add host-side interop contract tests for managed/native boundaries (starting with W5500 socket API status/parameter handling).
- [ ] Add hardware smoke-test checklist for W5500 RX/TX and USB wire protocol after native implementation lands.

## Deferred: Align to Latest Stable Everywhere

- [ ] Inventory latest stable versions for all nanoFramework packages in use.
- [ ] Upgrade in small batches with compile + build verification after each batch.
- [ ] Record rollback points per batch.
- [ ] Update docs once a fully verified baseline is reached.

## Deferred: Managed Project Rename

- [x] Rename managed project from `DiseqC` to `DiSEqC_Control`.
- [x] Rename managed project files/folders (`*.nfproj`, `*.sln`, and related paths) to match new project name.
- [x] Update C# namespaces from `DiseqC` to `DiSEqC_Control` across managed code.
- [x] Update native interop registration names/assembly identifiers and build scripts required by the rename.
- [x] Update docs and command examples to reference the new project name.
- [x] Re-run `./toolchain/compile-managed.sh` and `./toolchain/build-chain.sh` after rename (full build remains blocked by known `System.Drawing.Common` metadata processor issue).

## Native/W5500 Follow-up

- [ ] Replace W5500 interop stub behavior with real RX/TX socket path.
- [ ] Implement real board pin configuration in `ConfigPins_I2C3()` (current symbol is a link-fix stub only).
- [ ] Validate runtime networking path on hardware once board is available.
