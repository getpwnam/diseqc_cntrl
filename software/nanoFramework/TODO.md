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
- [x] Add host-side interop contract tests for managed/native boundaries (starting with W5500 socket API status/parameter handling).
- [ ] Add hardware smoke-test checklist for W5500 RX/TX and USB wire protocol after native implementation lands.
- [ ] PAUSED: add further host-side test construction until Phase 3 native W5500 transport is implemented.

## Implementation Plan (Phased)

### Phase 1: Managed Test Foundation (completed)

- [x] Create host-only unit test project and run in Linux CI/local.
- [x] Add pure-logic tests for configuration and parity behaviors.
- [x] Document how to run unit tests in `software/nanoFramework/README.md`.

### Phase 2: Interop Contract Tests (paused)

- [x] Add host-side tests for managed/native contract behavior of W5500/DiSEqC/LNB interop API (host-safe reflection contract checks).
- [ ] Add runtime/on-device interop tests for parameter validation, lifecycle, and timeout/error paths (host CLR cannot invoke InternalCall methods).
- [ ] PAUSED pending Phase 3: defer additional test expansion until real native transport behavior exists.

### Phase 3: Real W5500 Native Transport (active focus)

- [x] Replace stub behavior in `nf-native/w5500_interop.cpp` with real RX/TX socket path.
- [x] Wire W5500 local IP/subnet/gateway/MAC defaults to runtime config + FRAM (`network.*`) via native `ConfigureNetwork` interop.
- [x] Reconcile board-level pin/config definitions required by W5500 runtime path.
- [x] Verify `w5500-native` firmware profile build remains green in Docker.

### Phase 3.5: M2Mqtt Adapter Integration (recommended next)

- [ ] Create in-repo managed adapter implementing `nanoFramework.M2Mqtt.IMqttNetworkChannel` backed by `DiSEqC_Control.Native.W5500Socket`.
- [ ] Add minimal M2Mqtt entry point to accept injected channel (small fork/overlay of package source in repo).
- [ ] Keep current host/port constructor path intact as fallback to reduce rollout risk.
- [ ] Update `Program.cs` to select transport mode (`system-net` fallback vs `w5500-native` adapter) via runtime config key.
- [ ] Add host contract tests for adapter behavior (connect/send/receive/close call routing + status mapping).
- [ ] Add on-device MQTT smoke test against broker using W5500 adapter path.
- [ ] Document adapter mode usage and rollback steps in `README.md` and testing guide.

### Phase 4: USB Wire Protocol Migration

- [ ] Define target USB behavior for deployment/debug wire protocol (native USB as primary interface).
- [ ] Implement required firmware startup/config changes for USB wire protocol.
- [ ] Update docs and test procedures for USB-first workflow.

### Phase 5: Hardware Validation

- [ ] Run board-level smoke tests for W5500 connectivity and stability.
- [ ] Validate USB wire protocol end-to-end (connect/deploy/debug).
- [ ] Capture known-good validation matrix and rollback notes in docs.

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

- [x] Replace W5500 interop stub behavior with real RX/TX socket path.
- [x] Implement real board pin configuration in `ConfigPins_I2C3()` (PA8/PC9 I2C3 open-drain AF4 setup).
- [ ] Validate runtime networking path on hardware once board is available.
