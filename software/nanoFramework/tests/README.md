# Test Assets

This folder contains host unit tests and MQTT smoke scripts.

Managed deployment app source now lives only in `../DiSEqC_Control/`.

## 1) Host Unit Tests (Managed Logic)

Run from Linux/WSL host:

```bash
cd software/nanoFramework/tests/DiSEqC_Control.Tests
dotnet test -v minimal
```

Current coverage focus includes:

- MQTT command routing (`MqttCommandRouterTests.cs`)
- MQTT config command processing (`MqttConfigCommandProcessorTests.cs`)
- Runtime config and helper utility behavior

## 2) MQTT Smoke Scripts

These scripts validate MQTT behavior without requiring new DiSEqC hardware revisions.

### 2.1) Broker Loopback Self-Test

Checks that the MQTT broker can receive and return messages.

```bash
cd software/nanoFramework/tests
chmod +x mqtt_broker_selftest.sh
./mqtt_broker_selftest.sh <broker-host> <broker-port>
```

Examples:

```bash
./mqtt_broker_selftest.sh 192.168.1.60 1883
./mqtt_broker_selftest.sh localhost 1883
```

### 2.2) Device Config Topic Smoke Test

Sends `config/get` and waits for the controller to publish a config status value.

```bash
cd software/nanoFramework/tests
chmod +x mqtt_device_config_smoke.sh
./mqtt_device_config_smoke.sh <broker-host> <broker-port> <topic-prefix>
```

Example:

```bash
./mqtt_device_config_smoke.sh 192.168.1.60 1883 diseqc
```

Notes:

- The second script requires the DiSEqC controller app to be connected to the same broker.
- `mosquitto_pub` and `mosquitto_sub` must be installed (`mosquitto-clients` package).

### 2.3) Topic Watcher

Continuously prints MQTT traffic for a topic filter.

```bash
cd software/nanoFramework/tests
chmod +x mqtt_topic_watch.sh
./mqtt_topic_watch.sh mqtt.ebnx.net 1883 'diseqc/#'
```

## 3) Tier-0 Mailbox Reliability Smoke (Phase C)

Use this script to validate Tier-0 mailbox semantics (`BringupStatus` and
`DiagnosticsMailbox`) across repeated reset/read cycles.

It verifies:

- `g_cubley_diag_boot_probe_status` is latched (non-zero) and remains sticky within each cycle.
- status words decode with valid `0xD5SSRRDD` magic/result format.
- reset-cycle repetition does not break basic Tier-0 mailbox reads.

Command:

```bash
cd software/nanoFramework
chmod +x tests/tier0_mailbox_reliability_smoke.sh
./tests/tier0_mailbox_reliability_smoke.sh --cycles 10 --read-count 4
```

If your default `build/nanoCLR.elf` is stripped, the script auto-selects a
symbolized profile ELF when available.

Optional explicit override:

```bash
./tests/tier0_mailbox_reliability_smoke.sh \
	--cycles 10 \
	--read-count 4 \
	--elf build/nf-interpreter/M0DMF_CUBLEY_F407/nanoCLR.elf
```

Prerequisites:

- `st-flash`, `openocd`, and `arm-none-eabi-gdb` (or `gdb-multiarch`/`gdb`)
- Built `build/nanoCLR.elf`
- Connected ST-Link target

Tip:

- Use `--stop-on-fail` during triage to halt on the first failing cycle.

## 4) CubleySmokeTier0 Managed Harness

`CubleySmokeTier0` is a minimal managed app for firmware-first smoke coverage.
It exercises only Tier-0/Tier-1 interop calls and avoids full `DiSEqC_Control`
runtime complexity.

Project path:

- `software/nanoFramework/CubleySmokeTier0/CubleySmokeTier0.nfproj`

Build and deploy by SWD:

```bash
cd software/nanoFramework
./toolchain/build-managed.sh build \
	--project CubleySmokeTier0/CubleySmokeTier0.nfproj \
	--deploy --swd --address 0x080C0000 --reset
```

Then run Tier-0 reliability smoke:

```bash
cd /workspaces/diseqc_cntrl
./software/nanoFramework/tests/tier0_mailbox_reliability_smoke.sh --cycles 10 --read-count 4 --stop-on-fail
```

Then run the combined Tier-0/Tier-1 reset-cycle gate (Phase C #49):

```bash
cd /workspaces/diseqc_cntrl
./software/nanoFramework/tests/tier0_mailbox_reliability_smoke.sh \
	--cycles 20 \
	--read-count 4 \
	--require-final-pass \
	--stop-on-fail
```

`--require-final-pass` upgrades the smoke from format-only validation to a
blocking gate:

- requires managed final marker semantics (`current_status` stage `0xCF`, result `PASS`, Tier-1 detail bit `0x40` set)
- treats `FAIL`/`EXCEPTION` result words in `current_status` or `clr_status` as blocking failures
- preserves Tier-0 sticky-latch checks across repeated reads in each reset cycle

Harness behaviors:

- Tier-0: `BringupStatus` set/get round-trip and `DiagnosticsMailbox` latch-once checks.
- Tier-1: repeated mixed-order `StatusLed` and `UsbCdcConsole` calls for stable execution verification.
- Tier-1 safety: verifies Tier-1 call sequences do not clobber sticky boot-probe latch.
- Final marker: writes a deterministic status word so SWD readers can confirm run completion.

## 5) CubleySmokeTier2_W5500 Managed Harness

`CubleySmokeTier2_W5500` is a dedicated managed W5500 smoke app for Phase D.
It isolates managed-triggered W5500 activation from full application startup.

Recommended native profile for this harness during Phase D:

- `phase-d-smoke` for component-sliced interop bring-up and managed-triggered W5500 debugging
- `cubley-base` remains the frozen Phase A board baseline and should not be extended with W5500-specific exposure

Project path:

- `software/nanoFramework/CubleySmokeTier2_W5500/CubleySmokeTier2_W5500.nfproj`

Build the native firmware profile first:

```bash
cd software/nanoFramework
NF_ALLOW_REFERENCE_PROFILE=1 ./toolchain/build-native.sh build --profile phase-d-smoke
st-flash write build/nanoBooter.bin 0x08000000
st-flash write build/nanoCLR.bin 0x08004000
st-flash reset
```

Build and deploy by SWD:

```bash
cd software/nanoFramework
./toolchain/build-managed.sh build \
	--project CubleySmokeTier2_W5500/CubleySmokeTier2_W5500.nfproj \
	--deploy --swd --address 0x080C0000 --reset
```

Then read current status + W5500 diagnostics:

```bash
cd /workspaces/diseqc_cntrl/software/nanoFramework
./tests/swd_read_bringup_status.sh
./tests/swd_read_w5500_diag.sh build/nanoCLR.elf
```

Harness behaviors:

- Reads `VERSIONR` and `PHYCFGR` through `Cubley.Interop.W5500Socket`.
- Configures static network parameters through native interop.
- Opens and closes the single socket path deterministically.
- Attempts `NativeConnect` only when PHY link is up; records timeout as warning.
- Emits deterministic stage markers `0xB0..0xB6` and final `0xBF`.
