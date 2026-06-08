# DiSEqC Satellite Dish Controller

**Automated satellite dish positioning and LNB control via MQTT**

## Getting Started

Use the debug bring-up guides as the canonical starting path:

- [Phase A Baseline](../../docs/debug/PHASE_A_BASELINE.md) — pinned build profile, flash map, tooling, and wiring for all Phase A runs
- [Managed Deployment](../../docs/debug/MANAGED_DEPLOYMENT.md)
- [Testing Guide](../../docs/debug/TESTING_GUIDE.md)

## Build Components (Build Separately)

This project has two independent build components. Run them separately.

Interop governance for these build paths:

- Contract map: `../../docs/software/INTEROP_CONTRACT_V1.md`
- Versioning policy: `../../docs/software/INTEROP_VERSIONING_POLICY.md`
- Enforced guards: `./toolchain/interop-guard.sh`, `./toolchain/interop-checksum.sh`
- Negative drift fixture test: `./toolchain/interop-negative-drift-test.sh`

### 1) Managed Application Build (C# project)

Builds the managed application in `DiSEqC_Control/` and validates C# compile health.

Command:

- `./toolchain/build-managed.sh compile`

Use this as the local pre-commit gate for managed code changes.

#### Managed Unit Tests (host-only)

Run host-only unit tests for pure managed logic (currently `RuntimeConfiguration` and `ParityHelper`):

- `cd tests/DiSEqC_Control.Tests && dotnet test -v minimal`

Recommended local sequence before commit:

1. `./toolchain/build-managed.sh compile`
2. `cd tests/DiSEqC_Control.Tests && dotnet test -v minimal`

#### Managed Build + Optional Deploy (CLI helper)

Use the helper script when you want a single command path for full managed build (`/t:Build`) and optional deploy via `nanoff`:

- Build only:
	- `./toolchain/build-managed.sh build`
- Build and deploy:
	- `./toolchain/build-managed.sh build --deploy --serialport /dev/ttyUSB0 --address 0x080C0000`

Notes:

- Deployment requires a valid target deployment address for your firmware layout.
- Use `./toolchain/build-managed.sh --help` for all options.

Windows users should run the shell scripts through WSL/Linux.

### 2) Firmware Build (nf-interpreter / nanoCLR)

Builds firmware artifacts by fetching/using `nf-interpreter` inside Docker and compiling the `M0DMF_CUBLEY_F407` target.

Commands (recommended wrapper):

- Cubley base profile (default/canonical): `./toolchain/build-native.sh build --profile cubley-base`
- Cubley W5500-native profile: `./toolchain/build-native.sh build --profile cubley-uart`
- Cubley USB bring-up profile (no-VBUS-sense default): `./toolchain/build-native.sh build --profile cubley-usb`
- Cubley hardalive profile (PA2 + PB10 hard toggle, no RTOS/CLR): `./toolchain/build-native.sh build --profile cubley-hardalive`
- Bring-up smoke diagnostic profile (PA2 blink + USART3 heartbeat): `./toolchain/build-native.sh build --profile bringup-smoke`
- Core-only diagnostic profile: `./toolchain/build-native.sh build --profile core-only`

Reference-only profile safety gate:

- Non-base profiles are blocked by default.
- To run intentionally: `NF_ALLOW_REFERENCE_PROFILE=1 ./toolchain/build-native.sh build --profile <profile>`

Deprecated profile quarantine:

- `network` now maps to `legacy-network` and is blocked by default.
- To run it intentionally: `NF_ALLOW_DEPRECATED_PROFILE=1 ./toolchain/build-native.sh build --profile legacy-network`

### Firmware Profile Matrix

| Profile | Status | Primary Purpose | Key Traits |
|---|---|---|---|
| `cubley-base` | canonical | Default baseline firmware | Minimal UART3 wire-protocol baseline on `M0DMF_CUBLEY_V0.4` |
| `cubley-stable` | reference-only | Historical stable comparison profile | Non-network, config block on, RTC on, UART wire protocol |
| `cubley-uart` | reference-only | Native W5500 bring-up scaffold | Non-network, config block off, SPI/GPIO/I2C on |
| `cubley-usb` | reference-only | USB-first transport bring-up | OTG1 + USB serial enabled; VBUS-sense mode selectable |
| `cubley-hardalive` | reference-only | Bare-metal liveness check | No RTOS/CLR startup; hard pin toggles |
| `bringup-smoke` | reference-only | Fast smoke diagnostics | PA2 blink + USART3 heartbeat |
| `core-only` | reference-only | Fast firmware iteration | Smallest managed/API footprint |
| `legacy-network` | deprecated | Transitional compatibility only | lwIP/System.Net path; gated by env var |

The wrapper auto-runs inside Docker when invoked from host Linux/WSL, and also works when already inside the container.

Expected firmware outputs in `build/`:

- `nanoCLR.bin`
- `nanoCLR.hex`
- `nanoCLR.elf`

### Managed Deployment Target

The only managed deployment app kept in this repo is `DiSEqC_Control/`:

- Build: `./toolchain/build-managed.sh compile`
- Output: `build/DiSEqC_Control/DiSEqC_Control.bin` when produced; otherwise deploy `build/DiSEqC_Control/DiSEqC_Control.pe`
- Deploy over USART3 wire protocol with `nanoff` to `0x080C0000`

### Bring-up Session Logging

To keep debugging history stable across long sessions and context compaction, append test outcomes to:

- `../../docs/debug/BRINGUP_TEST_LOG.md`

Helper command:

- `./toolchain/bringup_log_append.sh --result PASS|FAIL|INFO --commands "..." --artifact "..." --conclusion "one-line conclusion"`

Use `--help` for optional fields (`--breakpoints`, `--note`, `--baseline`, `--logfile`).

### Deterministic Cycle Campaign Helper

For Phase A issue #26 style repeatability runs, use:

- `./toolchain/run-deterministic-cycles.sh --cycles 20 --serial /dev/ttyUSB0 --baud 115200 --settle-ms 2000`

The helper performs booter flash, CLR flash, explicit `st-flash reset`, then `nanoff --listdevices` and `--devicedetails` per cycle, writing per-cycle logs under `.debug/`.

## MQTT Transport Mode (Phase 3.5)

Runtime config key:

- `mqtt.transport_mode=system-net|w5500-native`

Current behavior:

- `system-net` (default): use standard `MqttClient` network channel path.
- `w5500-native`: request injected W5500-backed `IMqttNetworkChannel` path.
- If channel injection fails at runtime, code logs the reason and continues on the `system-net` fallback path.

Set mode via MQTT config command:

- `mosquitto_pub -h <broker-ip> -t 'diseqc/command/config/set' -m 'mqtt.transport_mode=w5500-native'`
- `mosquitto_pub -h <broker-ip> -t 'diseqc/command/config/save' -m ''`

Rollback to default:

- `mosquitto_pub -h <broker-ip> -t 'diseqc/command/config/set' -m 'mqtt.transport_mode=system-net'`
- `mosquitto_pub -h <broker-ip> -t 'diseqc/command/config/save' -m ''`

Verification:

- Request effective config and check `diseqc/status/config/effective/mqtt/transport_mode`.
- For full on-device smoke steps, see `../../docs/debug/TESTING_GUIDE.md` (Step 2.5).

## Optional Managed Build Modes

Use `./toolchain/build-managed.sh` with an explicit mode:

- Full managed build (`/t:Build`): `./toolchain/build-managed.sh build`
- Compile-only mode (`/t:Compile`): `./toolchain/build-managed.sh compile`

Known limitation:

- May still emit assembly remap warnings (`MSB3276`) that are non-blocking for current local build workflow.

Linux metadata processor workaround:

- `toolchain/build-managed.sh` creates a temporary metadata processor override folder and injects `System.Drawing` dependencies when available.
- This avoids editing the VS Code extension install and keeps the workaround local/scripted.

Tracking:

- See [TODO.md](../../docs/software/TODO.md), section **Current Focus: Build Chain Reliability**.

## Host Prerequisites

All build, flash, and deploy work is performed **inside the devcontainer**
(`.devcontainer/`).  Every required tool — `gcc-arm-none-eabi` (15.2.rel1),
cmake (3.31.6), kconfiglib, stlink-tools, nanoff, dotnet/MSBuild and the
`xbuild` shim — is pre-installed in the devcontainer image.  No host-side
tool installs are required.

USB device passthrough (ST-Link, UART adapter) is configured in
`.devcontainer/devcontainer.json`.  On WSL2/Windows, attach USB devices to
WSL first (e.g. with `usbipd`) before rebuilding/reopening the container.

## Scope

This folder contains:
- managed application code (`DiSEqC_Control/`),
- native integration code (`nf-native/`), and
- build orchestration/scripts for generating firmware artifacts.

The upstream `nf-interpreter` codebase is fetched during the firmware build; it is not maintained as a first-class directory in this repository.

## Documentation

- [Docs Index](docs/README.md)
- [Phase A Baseline](../../docs/debug/PHASE_A_BASELINE.md)
- [Testing Guide](../../docs/debug/TESTING_GUIDE.md)
- [MQTT API](../../docs/software/MQTT_API.md)
- [Architecture](../../docs/software/ARCHITECTURE.md)
- [Diagnostics Mailbox](../../docs/debug/DIAGNOSTICS_MAILBOX.md)
