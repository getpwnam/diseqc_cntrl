# DiSEqC Satellite Dish Controller

**Automated satellite dish positioning and LNB control via MQTT**

## Quick Start

See [QUICK_START.md](QUICK_START.md) for build instructions.

## Build Components (Build Separately)

This project has two independent build components. Run them separately.

### 1) Managed Application Build (C# project)

Builds the managed application in `DiSEqC_Control/` and validates C# compile health.

Command:

- `./toolchain/compile-managed.sh`

Use this as the local pre-commit gate for managed code changes.

#### Managed Unit Tests (host-only)

Run host-only unit tests for pure managed logic (currently `RuntimeConfiguration` and `ParityHelper`):

- `cd tests/DiSEqC_Control.Tests && dotnet test -v minimal`

Recommended local sequence before commit:

1. `./toolchain/compile-managed.sh`
2. `cd tests/DiSEqC_Control.Tests && dotnet test -v minimal`

#### Managed Build + Optional Deploy (CLI helper)

Use the helper script when you want a single command path for full managed build (`/t:Build`) and optional deploy via `nanoff`:

- Build only:
	- `./toolchain/build-managed-cli.sh`
- Build and deploy:
	- `./toolchain/build-managed-cli.sh --deploy --serialport /dev/ttyUSB0 --address 0x080C0000`

Notes:

- Deployment requires a valid target deployment address for your firmware layout.
- Use `./toolchain/build-managed-cli.sh --help` for all options.

Windows PowerShell (outside WSL):

- Build only:
	- `powershell -ExecutionPolicy Bypass -File .\toolchain\build-managed-cli.ps1`
- Build and deploy:
	- `powershell -ExecutionPolicy Bypass -File .\toolchain\build-managed-cli.ps1 -Deploy -SerialPort COM5 -Address 0x080C0000`

PowerShell script auto-detects `NanoFrameworkProjectSystemPath` from the installed VS Code extension, or you can override with `-NanoPsPath`.
If `nuget.exe` is not in PATH, the script automatically falls back to MSBuild restore. It resolves MSBuild in this order: `msbuild` from PATH, Visual Studio Build Tools via `vswhere`, then `dotnet msbuild`.

If `dotnet msbuild` fails with the known `System.Drawing.Common` metadata processor error while running from a `\\wsl.localhost\...` workspace path, the script automatically retries managed build through WSL using `toolchain/build-managed-cli.sh`. If that retry fails, it performs a second retry through `toolchain/build-chain.sh` (which includes the metadata-processor workaround used on Linux).

If VS Code extension is installed in WSL (remote) rather than Windows, pass it explicitly, for example:

- `powershell -ExecutionPolicy Bypass -File .\toolchain\build-managed-cli.ps1 -NanoPsPath "\\wsl.localhost\Debian\home\cp\.vscode-server\extensions\nanoframework.vscode-nanoframework-1.0.189\dist\utils\nanoFramework\v1.0"`

### 2) Firmware Build (nf-interpreter / nanoCLR)

Builds firmware artifacts by fetching/using `nf-interpreter` inside Docker and compiling the `M0DMF_DISEQC_F407` target.

Commands (recommended wrapper):

- Minimal profile: `./toolchain/build.sh minimal`
- W5500-native profile: `./toolchain/build.sh w5500-native`
- Bring-up smoke profile (PA2 blink + USART3 heartbeat): `./toolchain/build.sh bringup-smoke`
- Bring-up hardalive profile (PA2 + PB10 hard toggle, no RTOS/CLR): `./toolchain/build.sh bringup-hardalive`
- USB no-VBUS-sense profile (current board revision): `./toolchain/build.sh usb-no-vbus-sense`
- Network profile (deprecated transitional path): `./toolchain/build.sh network`

The wrapper auto-runs inside Docker when invoked from host Linux/WSL, and also works when already inside the container.

Expected firmware outputs in `build/`:

- `nanoCLR.bin`
- `nanoCLR.hex`
- `nanoCLR.elf`

### Managed Test Applications

Current managed test application(s):

- `tests/BlinkBringup/`:
	- Purpose: first wire-protocol deployment target that only blinks PA2.
	- Build: `./toolchain/compile-blink-test.sh`
	- Output: prefer `tests/BlinkBringup/bin/Release/BlinkBringup.bin` when produced; otherwise deploy `BlinkBringup.pe`
	- Deploy over USART3 wire protocol with `nanoff` to `0x080C0000`.

This app intentionally avoids serial output so it does not interfere with wire-protocol traffic.

### Bring-up Session Logging

To keep debugging history stable across long sessions and context compaction, append test outcomes to:

- `docs/BRINGUP_TEST_LOG.md`

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
- For full on-device smoke steps, see `docs/guides/TESTING_GUIDE.md` (Step 2.5).

## Optional Local Full Build-Chain Check

This path includes metadata/PE generation and now includes a Linux host workaround for metadata processor dependencies.

Command:

- `./toolchain/build-chain.sh`

Known limitation:

- May still emit assembly remap warnings (`MSB3276`) that are non-blocking for current local build workflow.

Linux metadata processor workaround:

- `toolchain/build-chain.sh` now creates a temporary metadata processor override folder and injects Mono `System.Drawing.dll`, then sets `NF_MDP_MSBUILDTASK_PATH` for the `/t:Build` step.
- This avoids editing the VS Code extension install and keeps the workaround local/scripted.

Tracking:

- See [TODO.md](TODO.md), section **Current Focus: Build Chain Reliability**.

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
- [Docker Build Guide](docs/guides/DOCKER_BUILD_GUIDE.md)
- [Testing Guide](docs/guides/TESTING_GUIDE.md)
- [MQTT API](docs/reference/MQTT_API.md)
- [Architecture](docs/reference/ARCHITECTURE.md)
