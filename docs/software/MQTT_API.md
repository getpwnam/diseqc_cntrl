# MQTT API Reference

## Transport

CubleyControl uses MQTT 3.1.1 without TLS. It connects through the STM32F407
Ethernet MAC and LAN8742A PHY after IPv4 and DNS are ready.

The configured topic prefix is `diseqc` by default. All command grammars are
shared with USB CDC; MQTT carries a complete command line rather than using a
separate topic for every operation.

| Direction | Topic | Payload | QoS | Retained |
|---|---|---|---:|---|
| Command to device | `<prefix>/command` | One command line | 1 | Must be false |
| Result from device | `<prefix>/status` | One output line | 0 | No |
| Device availability | `<prefix>/availability` | `online` or `offline` | 0/1 | Yes |

The broker receives a retained `online` message after connection. The configured
last will is retained `offline` at QoS 1.

## Commands And Results

Publish any command documented in
[INTERFACE_COMMAND_MAP_V1.md](INTERFACE_COMMAND_MAP_V1.md) to
`<prefix>/command`. Each parser output line is published separately to
`<prefix>/status`.

Examples:

```bash
mosquitto_sub -t 'diseqc/status' -t 'diseqc/availability' -v
mosquitto_pub -q 1 -t 'diseqc/command' -m 'show lnb a'
mosquitto_pub -q 1 -t 'diseqc/command' -m 'diseqc goto 12'
mosquitto_pub -q 1 -t 'diseqc/command' -m 'system.capabilities.get'
```

Command payloads must be 1 to 64 ASCII bytes. The device rejects messages on an
unexpected topic, empty or oversized payloads, and retained command messages.
This prevents a stale retained command from executing after reconnect or reboot.

## Connection Lifecycle

The MQTT worker remains disabled until a valid saved configuration has
`enabled=on`. It then waits for network availability and a non-`0.0.0.0` IPv4
address before connecting. Broker hostnames use the configured nanoFramework DNS
settings.

An empty client ID is resolved as `cubley-XXXXXX`, where `XXXXXX` is the final
three MAC-address bytes in uppercase hexadecimal. Saved configuration changes
close the active session and reconnect using the new settings.

## Configuration

Use the shared command channel or USB CDC to inspect and stage MQTT settings:

```text
get mqtt
show mqtt
set mqtt broker 192.168.1.50
set mqtt topic-prefix diseqc
set mqtt enabled on
set mqtt save
```

The broker must be set before an enabled configuration can be saved. Credentials
are case-preserving but cannot contain spaces in schema v1. Password commands are
redacted from debug logs, and query output reports only whether credentials exist.

See [CONFIGURATION.md](CONFIGURATION.md) for the complete command list and
[CONFIGURATION_STORAGE.md](CONFIGURATION_STORAGE.md) for the persisted schema.

## Scope

The `cubley/v1/...` topics and JSON envelopes in the interface schema documents
remain design contracts; they are not implemented by this initial transport.
TLS, certificate management, and encrypted credential storage are also deferred.