# CUBLEY_F407_0_5 Firmware Rewrite (Kconfig / nf-interpreter workflow)

Date: 2026-07-15
Status: Phase 0 scaffold in progress — see progress table at the end.

Cubley v0.5 board (MCU: STM32F407VGT6, LQFP100).
The correct pin mappings and configurations for this board are in [BOARD_PIN_OWNERSHIP_AUDIT.md](../../docs/debug/BOARD_PIN_OWNERSHIP_AUDIT.md).

Do not use dynamic build profiles or copy files in from elsewhere during build.
Do not implement a "mailbox" bring-up protocol. With a reliable wire protocol and gdb/openocd we can replicate the functionality without interrupting control flow.

## 1) Goals and Non-Goals

### Goals
- Build directly from the upstream [`nanoframework/nf-interpreter`](https://github.com/nanoframework/nf-interpreter) repository, pinned as a git submodule at a Kconfig-capable commit on `main`.
- Keep the CUBLEY_F407_0_5 target as plain files inside this repository at `firmware/targets-local/CUBLEY_F407_0_5/`. No fork of `nanoframework/nf-Community-Targets`; that repo is a reference only.
- Drive all target configuration through Kconfig `defconfig` + optional `user-kconfig.conf` — no per-target CMakeLists that duplicate Kconfig state.
- Keep managed smoke tests in `software/nanoFramework/tests/` with explicit dependency on this native target.
- Implement features in the required order (Section 5). Each phase has an explicit gate.

### Non-Goals
- No fork of `nf-interpreter` itself. The build tree is used verbatim from upstream at a pinned commit.
- No fork of `nf-Community-Targets`.
- No mailbox/bring-up-status memory protocol.
- No dynamic build profiles or copy-in source swapping during build (bootstrap only creates a symlink into the nf-interpreter tree; the target files are never copied).
- No hidden dependency on the old `nf-native/` target directories (now parked at `firmware/_legacy-reference/`) at build time.

## 2) Board Baseline (From Audit)

- MCU: STM32F407VGT6 (LQFP100)
- Target name: `CUBLEY_F407_0_5`
- Wire protocol UART: USART3 on PD8 (TX) / PD9 (RX), AF7
- Required wire protocol baud: 921600
- Status LED: PB0, GPIO output, active-high
- FRAM bus: I2C1 on PB6 (SCL) / PB7 (SDA), AF4 open-drain + pull-up
- LNB I2C bus: I2C3 on PA8 (SCL) / PC9 (SDA), AF4 open-drain + pull-up
- LNB fault pin: PC8 input + pull-up
- Ethernet PHY: LAN8742A on RMII (see hardware schematic under `hardware/`)

## 3) Repository Layout

### Git submodules (all pinned)

| Path | Source | Pin | Purpose |
|---|---|---|---|
| `firmware/nf-interpreter` | `nanoframework/nf-interpreter` (upstream, unmodified) | `main` @ `51945c358` | nanoFramework native interpreter, Kconfig-capable |

The CUBLEY_F407_0_5 target itself lives at `firmware/targets-local/CUBLEY_F407_0_5/` as plain, git-tracked files in this repository. There is no second submodule for community targets.

### Bootstrap step (symlink integration)

`nf-interpreter` declares `targets-community` as its own nested submodule pointing at the upstream community-targets repo. Git does not allow the outer parent (`diseqc_cntrl`) to register a submodule inside another submodule's tree, and we do not want a copy of any community-targets repository at all, so:

1. The outer `diseqc_cntrl` repo owns the target at `firmware/targets-local/CUBLEY_F407_0_5/`.
2. Before every build, `firmware/bootstrap.sh` creates a relative symlink `firmware/nf-interpreter/targets-community/ChibiOS/CUBLEY_F407_0_5 -> ../../../targets-local/CUBLEY_F407_0_5`. This is exactly where the CMake logic in `targets/ChibiOS/CMakeLists.txt` searches for community boards.
3. `nf-interpreter`'s inner `targets-community` submodule stays deinitialised and never re-inits. Its git status will show `targets-community/` as untracked content because of our symlink; this is expected and harmless because we never push to `nf-interpreter`.

### Directory layout

```
firmware/
├── FIRMWARE_REWRITE.md            (this file)
├── bootstrap.sh                   (creates the symlink into nf-interpreter)
├── targets-local/
│   └── CUBLEY_F407_0_5/           (Cubley target — source of truth, in this repo)
│       ├── board.h  board.c
│       ├── defconfig  CMakePresets.json
│       ├── nanoBooter/  nanoCLR/  common/
│       └── target_*.c(pp)/.h(.in)
├── nf-interpreter/                (submodule → nanoframework/nf-interpreter @ 51945c358)
│   ├── build/                     (out-of-source CMake build root — git-ignored)
│   └── targets-community/ChibiOS/CUBLEY_F407_0_5 -> ../../../targets-local/CUBLEY_F407_0_5
│                                  (symlink created by bootstrap.sh)
└── toolchain/                     (bring-up helper scripts)

software/nanoFramework/tests/SmokeLed_CUBLEY_F407_0_5/       (Phase 8)
software/nanoFramework/tests/SmokeLnbh26_CUBLEY_F407_0_5/    (Phase 8)
```

### Build artifact rules

- CMake configures out-of-source into `firmware/nf-interpreter/build/CUBLEY_F407_0_5/` (the default when using `cmake --preset CUBLEY_F407_0_5`).
- Native artifacts (`nanoBooter.bin`, `nanoCLR.bin`, ELF, map, logs) live under that build directory.
- Managed build artifacts remain in `software/nanoFramework/…` test output folders.
- One-command clean: `rm -rf firmware/nf-interpreter/build/CUBLEY_F407_0_5/`.
- Wire protocol on USART3 is a protected path: any instability introduced in later phases is a regression and blocks progression.

## 4) Build Commands

Every fresh checkout of `diseqc_cntrl`:

```bash
git submodule update --init --recursive firmware/nf-interpreter
firmware/bootstrap.sh
```

Configure + build (from workspace root):

```bash
cd firmware/nf-interpreter
cmake --preset CUBLEY_F407_0_5
cmake --build --preset CUBLEY_F407_0_5
```

The Kconfig pipeline within nf-interpreter:
1. `cmake --preset` reads `NF_TARGET_DEFCONFIG=targets-community/ChibiOS/CUBLEY_F407_0_5/defconfig`.
2. `scripts/nf_merge_config.py` merges that defconfig with optional `config/user-kconfig.conf`.
3. `genconfig` produces `build/.config` and `build/nf_config.h`.
4. CMake picks up config knobs from `nf_config.h` and drives the ChibiOS build.

Reference: [Kconfig Target Configuration](https://docs.nanoframework.net/content/building/kconfig-options.html).

Updating the `defconfig` for the target after edits:

```bash
# Edit firmware/targets-local/CUBLEY_F407_0_5/defconfig
cd firmware/nf-interpreter
cmake --preset CUBLEY_F407_0_5             # re-run genconfig
cmake --build --preset CUBLEY_F407_0_5
```

The symlinked target is edited in place; no rebootstrap is needed after content changes.

## 5) Execution Phases (Required Order)

Proceed to the next phase only if the current phase gate passes.

Cross-phase regression rule:
- At the end of every phase, rerun wire-protocol stability checks.
- Any wire-protocol regression (enumeration, handshake, deploy path stability) is a hard fail for that phase.

### Phase 0 — Scaffold and Baseline

Deliverables:
- `nf-interpreter` submodule pinned; `firmware/targets-local/CUBLEY_F407_0_5/` contains a target seeded from `ST_STM32F4_DISCOVERY/` with Cubley-specific `board.h`/`board.c` and `CONFIG_TARGET_BOARD` renamed.
- `firmware/bootstrap.sh` executes idempotently (creates the symlink into nf-interpreter).
- `cmake --preset CUBLEY_F407_0_5` completes without error against upstream `nf-interpreter@51945c358`.
- `cmake --build --preset CUBLEY_F407_0_5` produces `nanoBooter.bin` and `nanoCLR.bin` for STM32F407VG.

Gate:
- Fresh clone reproduces the build using only the commands in Section 4 (no manual steps).
- Build artifacts appear under `firmware/nf-interpreter/build/CUBLEY_F407_0_5/`.

### Phase 1 — Wire Protocol on USART3 @ 921600

Implementation (in `firmware/targets-local/CUBLEY_F407_0_5/`):
- Edit `mcuconf.h` / `mcuconf_nf.h` under `nanoBooter/` and `nanoCLR/` to enable USART3.
- Configure debug/transport SD3 on PD8 (TX, AF7) / PD9 (RX, AF7) via board.c and `target_common.h.in` (via `SWO_UART` / `STDIN_STDOUT_UART` macros as required by nf-interpreter).
- Set default wire-protocol baud to 921600 (via `CONFIG_NF_WP_BAUD_RATE` if exposed, otherwise via the target's transport init).
- Ensure startup path initialises serial transport reliably after reset.

Validation:
- Flash `nanoBooter.bin` @ `0x08000000` and `nanoCLR.bin` @ `0x08004000` via `st-flash write`.
- Run `nanoff --nanodevice --serialport /dev/ttyUSB0 --baud 921600 --devicedetails` inside the discovery window.
- Confirm stable device discovery and wire-protocol handshake.

Gate (PASS criteria):
- `nanoff devicedetails` succeeds at 921600.
- Handshake survives at least 5 reset + query cycles.

### Phase 2 — Native Boot LED Pattern (Double Quick Flash)

Implementation:
- Drive PB0 as active-high output in early boot (board.c line-initialisation, `PAL_MODE_OUTPUT_PUSHPULL`).
- Emit exactly two quick visible pulses before normal runtime.
- Document target duration in this file and in `firmware/targets-local/CUBLEY_F407_0_5/README.md`.

Validation:
- Cold power cycle and SW reset verification.
- Confirm pattern appears on every boot and does not block runtime init.

Gate (PASS criteria):
- Two-pulse pattern is visually repeatable and observed on ≥10 consecutive boots.

### Phase 3 — FRAM I2C1 Native Proof

Implementation:
- Enable `I2CD1` in `mcuconf.h`; wire PB6 (SCL, AF4) / PB7 (SDA, AF4) in board.c.
- Implement native-only FRAM smoke routines in `target_common.c` (device ID / read / write / read-back).
- Interop port is deferred to Phase 5+.

Validation:
- Run deterministic FRAM test vector writes and read-backs after reset.
- Verify retention across reset and power cycle windows.

Gate (PASS criteria):
- FRAM read/write/read-back passes on repeated runs without bus lockups.
- Results logged with addresses, expected bytes, and observed bytes.

### Phase 4 — USB-CDC User Console Isolation

Implementation:
- Enable `USBD1` (OTG_FS) in `mcuconf.h`; add `usbcfg.c/h` USB-CDC profile in the target's `common/`.
- Keep command set minimal (help / status / echo) for deterministic validation.
- Explicitly isolate protocols: USB-CDC must not carry nanoFramework wire protocol traffic.

Validation:
- Open USB-CDC terminal and exercise user-console commands.
- In parallel, verify wire protocol remains only on USART3 using `nanoff` at 921600.
- Confirm `nanoff` operations fail or are rejected on the USB-CDC endpoint.

Gate (PASS criteria):
- USB-CDC console is functional and stable for user interaction.
- No nanoFramework wire protocol is accepted or emitted on USB-CDC.

Managed deployment rule:
- The first managed deployment is allowed only after Phase 4 passes.

### Phase 5 — LED Interop (Managed ↔ Native)

Implementation:
- Port minimal LED interop API from prior workspace code, preserving method order/signatures.
- Perform an explicit binding audit (managed declarations vs native lookup table).
- API surface: read state, write state, optional toggle.

Validation:
- Unit-level native call checks (breakpoints/probe if needed).
- Managed `SmokeLed_CUBLEY_F407_0_5` app exercises interop methods in a loop.

Gate (PASS criteria):
- Managed app reliably controls PB0 through interop calls across reset/deploy cycles.

### Phase 6 — LNBH26 Interop (Managed ↔ Native)

Implementation:
- Port LNBH26 interop API from prior code with strict method-order parity.
- Configure I2C3 (PA8 SCL / PC9 SDA, AF4) and FLT input (PC8) per audit.
- Implement only required primitives first (read/write regs, status/fault read).

Validation:
- Hardware-attached smoke checks for read/write paths.
- Confirm no bus lockups; include retry/timeouts with bounded behaviour.

Gate (PASS criteria):
- Managed calls complete without transport/runtime instability.
- Register read-back and fault/status sampling are deterministic.

### Phase 7 — RMII + LAN8742A Native Bring-up

Implementation:
- Bring up RMII MAC/PHY path for LAN8742A in native firmware.
- Validate clocking (`MCO1` or `HSE`), pinmux (`RMII_REF_CLK`, `RMII_MDIO`, `RMII_MDC`, `RMII_CRS_DV`, `RMII_RXD0/1`, `RMII_TX_EN`, `RMII_TXD0/1`), PHY address/config, link detect, and basic TX/RX in native tests.
- Phase 7 is native-only; interop/API surface can be added later.

Validation:
- Execute the workspace's existing RMII/Ethernet bring-up checklist (methodology in [W5500_LINK_BRINGUP_CHECKLIST.md](../../docs/debug/W5500_LINK_BRINGUP_CHECKLIST.md) is W5500-specific but the reset-pattern methodology applies) and capture artifacts under the CMake build tree.
- Verify link negotiation and repeatable frame-path checks across reset cycles.
- Re-run USART3 wire-protocol stability suite to prove no regressions.

Gate (PASS criteria):
- LAN8742A link-up and native RMII smoke checks pass reproducibly.
- USART3 wire protocol remains stable after RMII enablement.

### Phase 8 — Managed Smoke Applications

Implementation:
- Build two managed apps in `software/nanoFramework/tests/`:
  - `SmokeLed_CUBLEY_F407_0_5`: validates LED interop API and boot-state assumptions.
  - `SmokeLnbh26_CUBLEY_F407_0_5`: validates LNBH26 interop sequence and key telemetry.
- Keep app dependencies explicit and locked (NuGet lockfiles committed).

Validation:
- Deploy each smoke app via wire protocol at 921600.
- Run scripted smoke sequence and capture logs alongside the build tree.

Gate (PASS criteria):
- Both smoke apps deploy and execute successfully on target hardware.
- Results reproducible after clean rebuild + reflash.

## 6) Definition of Done

- `nf-interpreter` submodule pinned to an explicit commit; `bootstrap.sh` is the only glue.
- CUBLEY_F407_0_5 target lives at `firmware/targets-local/CUBLEY_F407_0_5/` in this repo; it is edited and reviewed like any other tracked file.
- CMake build produces `nanoBooter.bin` and `nanoCLR.bin` deterministically from a fresh clone using the commands in Section 4.
- FRAM on I2C1 is proven by native smoke tests before the first managed deployment.
- USB-CDC user console is proven and isolated from the nanoFramework wire protocol.
- RMII + LAN8742A path is proven in native firmware before managed Ethernet-facing work.
- Managed smoke projects exist under `software/nanoFramework/tests/` and build deterministically.
- UART wire protocol on USART3 at 921600 is stable for deploy/debug.
- Boot LED double-flash works consistently.
- LED and LNBH26 interop paths are functional and covered by smoke apps.
- Documentation in this file and in `firmware/targets-local/CUBLEY_F407_0_5/README.md` is current.

## 7) Test and Traceability Plan

For each phase, capture:
- Command(s) run
- Artifact path(s) from the CMake build tree
- Probe/measurement checkpoints used
- PASS/FAIL outcome
- One-line conclusion

Primary log target: [docs/debug/BRINGUP_TEST_LOG.md](../../docs/debug/BRINGUP_TEST_LOG.md) entries after each gated phase completion.

### Regression checklist per phase

Wire-protocol baseline (required every phase, once toolchain scripts are ported):

- `nanoff --nanodevice --serialport /dev/ttyUSB0 --baud 921600 --devicedetails`
- `nanoff --nanodevice --serialport /dev/ttyUSB0 --baud 921600 --listtargets` (round-trip check)
- 5-cycle reset + query loop

Phase-specific native checks (added when each phase is implemented):

- Phase 3: native FRAM test vectors — smoke script to be added under `firmware/toolchain/`.
- Phase 4: USB-CDC console smoke — verifies no wire-protocol echo on USB endpoint.
- Phase 7: RMII/LAN8742A link-up + frame smoke.

Pass/fail policy:
- Any failure in the wire-protocol baseline is a regression and blocks merge.
- Any phase-specific smoke failure blocks progression to the next phase.
- A phase gate is complete only when both baseline and phase-specific checks pass in the same build.

## 8) Risks and Controls

- Risk: `nf-interpreter` `main` moves in breaking ways.
  - Control: outer submodule is pinned to a specific commit; updating that pin is an explicit, reviewed change.
- Risk: nested `targets-community` submodule state drifts and confuses git inside nf-interpreter.
  - Control: that inner submodule stays deinit'd; `bootstrap.sh` only touches `targets-community/ChibiOS/CUBLEY_F407_0_5` (as a symlink). Any noise in nf-interpreter's git status inside `targets-community/` is ignored by policy — we never push to nf-interpreter.
- Risk: wire-protocol instability after peripheral changes.
  - Control: run wire-protocol stability checks at each phase gate; treat failures as regressions.
- Risk: hidden dependence on legacy source tree.
  - Control: the Kconfig build references only `firmware/targets-local/CUBLEY_F407_0_5/` and the pinned `firmware/nf-interpreter/` submodule. Files under `firmware/_legacy-reference/` are historical reference material only (not on the build path) and will be deleted once the phases that mine them for values (USART3 config, boot LED pattern, halconf tweaks) have landed.

Interop note:
- Interop surface stability is intentionally not a release gate in this rewrite. Firmware stability and deterministic bring-up behaviour are prioritised first.

## 9) Progress

| Phase | Status | Notes |
|---|---|---|
| Phase 0 — scaffold | in progress | nf-interpreter submodule pinned. Target lives at `firmware/targets-local/CUBLEY_F407_0_5/` with Cubley `board.h`/`board.c` authored. `bootstrap.sh` symlinks it into nf-interpreter's `targets-community/ChibiOS/`. Awaiting first `cmake --preset` run. |
| Phase 1 — USART3 wire proto | pending | |
| Phase 2 — LED double-flash | pending | |
| Phase 3 — FRAM I2C1 | pending | |
| Phase 4 — USB-CDC console | pending | |
| Phase 5 — LED interop | pending | |
| Phase 6 — LNBH26 interop | pending | |
| Phase 7 — RMII / LAN8742A | pending | |
| Phase 8 — managed smokes | pending | |

## 10) Immediate Next Actions

1. Run `cmake --preset CUBLEY_F407_0_5` from `firmware/nf-interpreter/` and capture the first-configure log. Fix whatever the first pass complains about (missing config values, path issues) without deviating from the seeded ST_STM32F4_DISCOVERY structure.
2. Once the preset configures, run `cmake --build --preset CUBLEY_F407_0_5` and confirm `nanoBooter.bin` + `nanoCLR.bin` are produced under the build tree.
3. Close Phase 0 gate by loading those images on the Cubley board and running a first-shot `nanoff devicedetails` (may fail — that's Phase 1's job, but a "silent" board with a valid boot is acceptable as Phase 0 exit).
4. Begin Phase 1: enable USART3 SD3 in `firmware/targets-local/CUBLEY_F407_0_5/nanoBooter|nanoCLR/mcuconf.h` and `firmware/targets-local/CUBLEY_F407_0_5/board.{h,c}`, rebuild, flash, retest `nanoff devicedetails` at 921600.
