# DiSEqC Satellite Dish Controller

**Automated satellite dish positioning and LNB control via MQTT**

## Getting Started

Use the debug bring-up guides as the canonical starting path:

- [Managed Deployment](../../docs/debug/MANAGED_DEPLOYMENT.md)
- [Testing Guide](../../docs/debug/TESTING_GUIDE.md)

## Build Components (Build Separately)

This project has two independent build components. Run them separately.

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

- Cubley stable profile (default): `./toolchain/build-native.sh build --profile cubley-stable`
- Cubley W5500-native profile: `./toolchain/build-native.sh build --profile cubley-uart`
- Cubley USB bring-up profile (no-VBUS-sense default): `./toolchain/build-native.sh build --profile cubley-usb`
- Cubley hardalive profile (PA2 + PB10 hard toggle, no RTOS/CLR): `./toolchain/build-native.sh build --profile cubley-hardalive`
- Bring-up smoke diagnostic profile (PA2 blink + USART3 heartbeat): `./toolchain/build-native.sh build --profile bringup-smoke`
- Core-only diagnostic profile: `./toolchain/build-native.sh build --profile core-only`

Deprecated profile quarantine:

- `network` now maps to `legacy-network` and is blocked by default.
- To run it intentionally: `NF_ALLOW_DEPRECATED_PROFILE=1 ./toolchain/build-native.sh build --profile legacy-network`

### Firmware Profile Matrix

| Profile | Status | Primary Purpose | Key Traits |
|---|---|---|---|
| `cubley-stable` | stable | Default daily firmware | Non-network, config block on, RTC on, UART wire protocol |
| `cubley-uart` | scaffold | Native W5500 bring-up | Non-network, config block off, SPI/GPIO/I2C on |
| `cubley-usb` | experimental | USB-first transport bring-up | OTG1 + USB serial enabled; VBUS-sense mode selectable |
| `cubley-hardalive` | experimental | Bare-metal liveness check | No RTOS/CLR startup; hard pin toggles |
| `bringup-smoke` | experimental | Fast smoke diagnostics | PA2 blink + USART3 heartbeat |
| `core-only` | experimental | Fast firmware iteration | Smallest managed/API footprint |
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

- `./toolchain/bringup_log_append.sh --result PASS|FAIL|INFO --conclusion "one-line conclusion"`

Use `--help` for optional fields (`--commands`, `--artifact`, `--breakpoints`, `--note`).

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

## Host Prerequisites (Linux)

Before running managed build scripts on Linux, ensure these host tools are available:

- Mono/MSBuild toolchain:
	- `sudo apt update && sudo apt install -y mono-complete`
- `nanoff` CLI:
	- `dotnet tool install -g nanoff` (or `dotnet tool update -g nanoff`)
- Compatibility shim for build flows that still invoke `xbuild`:
	- `sudo ln -sf "$(command -v msbuild)" /usr/local/bin/xbuild`

Sanity check:

- `msbuild --version`
- `xbuild --version`
- `nanoff --help`

Troubleshooting:

- If you see `xbuild: command not found`, recreate the shim:
	- `sudo ln -sf "$(command -v msbuild)" /usr/local/bin/xbuild`

## Scope

This folder contains:
- managed application code (`DiSEqC_Control/`),
- native integration code (`nf-native/`), and
- build orchestration/scripts for generating firmware artifacts.

The upstream `nf-interpreter` codebase is fetched during Docker build; it is not maintained as a first-class directory in this repository.

## Documentation

- [Docs Index](docs/README.md)
- [Testing Guide](../../docs/debug/TESTING_GUIDE.md)
- [MQTT API](../../docs/software/MQTT_API.md)
- [Architecture](../../docs/software/ARCHITECTURE.md)
- [Diagnostics Mailbox](../../docs/debug/DIAGNOSTICS_MAILBOX.md)
