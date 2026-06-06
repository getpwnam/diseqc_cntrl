# nanoFramework TODO

## Program: Stable firmware + interop contract delivery (Phases A-F)

Program umbrella: [#11](https://github.com/getpwnam/diseqc_cntrl/issues/11)

### Child phase checklist (source of truth links)

- [ ] [Phase A: Deterministic firmware baseline and bring-up evidence runbook](https://github.com/getpwnam/diseqc_cntrl/issues/12)
- [ ] [Phase B: Interop contract governance (v1) and drift prevention](https://github.com/getpwnam/diseqc_cntrl/issues/13)
- [ ] [Phase C: Stabilize Tier-0/Tier-1 interop reliability](https://github.com/getpwnam/diseqc_cntrl/issues/14)
- [ ] [Phase D1: Freeze and validate LNBH26 interop contract](https://github.com/getpwnam/diseqc_cntrl/issues/15)
- [ ] [Phase D2: Freeze W5500 transport interop contract and constraints](https://github.com/getpwnam/diseqc_cntrl/issues/16)
- [ ] [Phase D3: Complete DiSEqC interop map and experimental gate](https://github.com/getpwnam/diseqc_cntrl/issues/17)
- [ ] [Phase E: Managed integration on frozen contracts](https://github.com/getpwnam/diseqc_cntrl/issues/18)
- [ ] [Phase F: Release discipline, compatibility matrix, and regression gate](https://github.com/getpwnam/diseqc_cntrl/issues/19)
- [ ] [Decision: Monorepo governance and split trigger criteria](https://github.com/getpwnam/diseqc_cntrl/issues/20)

### Dependency order and acceptance gates

- A -> B -> C -> (D1, D2, D3 in parallel) -> E -> F
- Decision #20 applies through Stage F and must stay linked from the umbrella issue.
- Do not roll out managed features from E/F before A-D gates have documented evidence.

### Gate evidence checklist

- [ ] Every completed phase includes links to gate evidence artifacts (logs/docs/tests) in its issue body or closing comment.
- [ ] No phase is marked complete until all declared dependencies are complete.
- [ ] Umbrella issue #11 reflects current phase status and links to evidence for each completed gate.

## Current Focus: Build Chain Reliability

### Current Decision (2026-02-23)

- Use `./toolchain/build-managed.sh compile` as the required local/pre-commit quality gate.
- Treat full `msbuild ... /t:Build` on this Linux host as blocked by nanoFramework metadata processor dependency/runtime mismatch.
- Generate deployable PE artifacts from a known-good packaging environment (e.g., Windows CI/dev box) until Linux metadata processor path is fixed.
- Do **not** rely on `libgdiplus` as the primary fix path for the current failure mode.

- [x] Make full `msbuild ... /t:Build` pass on Linux host (not only `/t:Compile`) via scripted Linux MDP override in `toolchain/build-chain.sh`.
- [x] Resolve `System.Drawing.Common` load failure in nanoFramework metadata processor (`NFProjectSystem.MDP.targets`) using `NF_MDP_MSBUILDTASK_PATH` + Mono `System.Drawing.dll` overlay.
- [x] Validate fix path for Linux-hosted metadata processor (current extension bundle includes a Windows-targeted `System.Drawing.Common` assembly).
- [x] Add a reproducible local build wrapper for full build (with required env vars/paths).
- [x] Document known-good build commands and prerequisites in `software/nanoFramework/README.md`.

## Managed Dependency Hygiene

- [x] Remove unused direct references/packages not required by current managed code.
- [x] Keep `System.Net` aligned to M2Mqtt expected version (`1.11.36`) to avoid CS1702 mismatch.
- [ ] Revisit remaining remap warnings now that full build chain is stable.
- [x] Keep `./toolchain/build-managed.sh compile` as the safe pre-commit gate until metadata processor issue is fixed.

## STM32-Only Cleanup

- [ ] Remove residual ESP32 references from tooling/docs/scripts and enforce STM32-only terminology in this repository.

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
- [x] Verify `cubley-uart` firmware profile build remains green in Docker.

### Phase 3.5: M2Mqtt Adapter Integration (in progress)

- [x] Create in-repo managed adapter implementing `nanoFramework.M2Mqtt.IMqttNetworkChannel` backed by `DiSEqC_Control.Native.W5500Socket`.
- [x] Add minimal M2Mqtt entry point to accept injected channel (in-repo overlay helper injecting `IMqttNetworkChannel`).
- [x] Keep current host/port constructor path intact as fallback to reduce rollout risk.
- [x] Update `Program.cs` to select transport mode (`system-net` fallback vs `w5500-native` adapter) via runtime config key.
- [x] Add host contract tests for adapter behavior (connect/send/receive/close call routing + status mapping).
- [x] Add on-device MQTT smoke test against broker using W5500 adapter path.
- [x] Document adapter mode usage and rollback steps in `README.md` and testing guide.

### Phase 4: USB Wire Protocol Migration

- [x] Define target USB behavior for deployment/debug wire protocol (native USB as primary interface).
- [ ] Implement required firmware startup/config changes for USB wire protocol. (Stage A implemented: `usb-first` profile + OTG/HAL USB flag wiring in `toolchain/build-native.sh`.)
- [ ] Update docs and test procedures for USB-first workflow.
- [ ] PAUSED: hardware-dependent USB validation is deferred until first working board is available.

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
- [x] Re-run `./toolchain/build-managed.sh compile` and `./toolchain/build-chain.sh` after rename (full build remains blocked by known `System.Drawing.Common` metadata processor issue).

## Managed Startup Preflight (idea)

- [ ] Add a minimal, always-completing managed hardware preflight at startup that probes core devices (W5500, LNBH26, FRAM, and other board-critical peripherals), records a per-device pass/fail bitmap + error details, and only then branches to main app behavior.
- [ ] Ensure preflight tolerates intentionally-missing hardware variants (for example, no W5500 fitted) without blocking remaining checks.
- [ ] Emit preflight summary to mailbox/diagnostic channel so failures are visible even when main app path is skipped.

## Temporary Diagnostics Cleanup

- [ ] Remove temporary deep CLR startup/resolve diagnostics in `toolchain/build-native.sh` (`CUBLEY_CLR_STARTUP_DIAG` and `CUBLEY_CLR_RESOLVE_PTR`) after startup regression risk is low and baseline tests are stable.

## Native/W5500 Follow-up

- [x] Replace W5500 interop stub behavior with real RX/TX socket path.
- [x] Implement real board pin configuration in `ConfigPins_I2C3()` (PA8/PC9 I2C3 open-drain AF4 setup).
- [ ] Validate runtime networking path on hardware once board is available.

## Deferred: Motor Control (after immediate hardware checks)

- [ ] Revisit native motor-enable interop/API surface after FRAM and board-level checks are completed.
- [ ] Define managed motor-control workflow (step, slew, tracking, timeout safety) and align native implementation.
- [ ] Add a dedicated motor bring-up test app with clear LED/telemetry pass-fail indicators.

## FRAM Hardware Checks (from bring-up)

- [ ] Verify FM24CL16B pin 7 (WP) is solidly tied low on assembled boards.
- [ ] Confirm FM24CL16B orientation/pin-1 alignment on PCB assemblies.
- [ ] Probe SDA/SCL bus levels during FRAM write transactions.
- [ ] Check continuity from MCU I2C3 lines (PA8/PC9) to FRAM pins.
