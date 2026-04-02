# Test Assets

This folder contains managed test applications, host unit tests, and MQTT smoke scripts.

## 1) Managed Test App: BlinkBringup

Purpose:

- Minimal managed deployment target for first wire-protocol validation.
- Toggles PA2 continuously with no serial output.

Build:

```bash
cd software/nanoFramework
./toolchain/compile-blink-test.sh
```

Output:

- `tests/BlinkBringup/bin/Release/BlinkBringup.pe`

Deploy:

- Deploy with `nanoff` to managed region `0x080C0000` after flashing `nanoBooter.bin` + `nanoCLR.bin`.

## 2) Host Unit Tests (Managed Logic)

Run from Linux/WSL host:

```bash
cd software/nanoFramework/tests/DiSEqC_Control.Tests
dotnet test -v minimal
```

Current coverage focus includes:

- MQTT command routing (`MqttCommandRouterTests.cs`)
- MQTT config command processing (`MqttConfigCommandProcessorTests.cs`)
- Runtime config and helper utility behavior

## 3) MQTT Smoke Scripts

These scripts validate MQTT behavior without requiring new DiSEqC hardware revisions.

### 3.1) Broker Loopback Self-Test

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

### 3.2) Device Config Topic Smoke Test

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

### 3.3) Topic Watcher

Continuously prints MQTT traffic for a topic filter.

```bash
cd software/nanoFramework/tests
chmod +x mqtt_topic_watch.sh
./mqtt_topic_watch.sh mqtt.ebnx.net 1883 'diseqc/#'
```
