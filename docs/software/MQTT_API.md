# MQTT API Reference

> **Superseded for commands, responses and DiSEqC topics.** The `command`,
> `response`, `event/diseqc` and `state/diseqc` topics now carry the JSON
> contract in [DEVICE_API_V2.md](DEVICE_API_V2.md), which also defines the
> positioner job model. This document remains authoritative for the LNB topics,
> connection lifecycle, health monitoring, and configuration, all unchanged.

The target structured payload and subsystem ownership rules are defined in
[OBSERVABILITY_CONTRACT_V1.md](OBSERVABILITY_CONTRACT_V1.md). This document
describes the currently implemented MQTT transport; topic migration is tracked in
the observability contract.

## Transport

CubleyControl uses MQTT 3.1.1 without TLS. It connects through the STM32F407
Ethernet MAC and LAN8742A PHY after IPv4 and DNS are ready.

The configured topic prefix is `diseqc` by default. The effective device root is
`<prefix>/<hostname>`. Commands arrive as JSON objects on a single topic rather
than using a separate topic for every operation. Positioner operations are
dispatched from typed parameters; the remaining operations are still executed by
the console tokenizer behind a strict allowlist, and administrative
configuration commands are not exposed.

| Direction | Topic | Payload | QoS | Retained |
|---|---|---|---:|---|
| Command to device | `<prefix>/<hostname>/command` | v2 JSON command object | 1 | Must be false |
| Response from device | `<prefix>/<hostname>/response` | v2 JSON response object | 1 | No |
| LNB asynchronous transition | `<prefix>/<hostname>/event/lnb` | Schema-1 LNB event fields | 1 | No |
| Current LNB state | `<prefix>/<hostname>/state/lnb` | Schema-1 LNB state fields | 1 | Yes |
| Positioner job transition | `<prefix>/<hostname>/event/diseqc` | v2 JSON job event | 1 | No |
| Current positioner state | `<prefix>/<hostname>/state/diseqc` | v2 JSON state object | 1 | Yes |
| Device availability | `<prefix>/<hostname>/availability` | `online` or `offline` | 1 | Yes |

The broker receives a retained `online` message after connection. The configured
last will is retained `offline` at QoS 1.

LNB and DiSEqC execution does not perform socket I/O. Responses, events, and state
updates are placed in a bounded queue and published only by the MQTT worker. A
broker failure, blocked publish, or full publication queue can lose MQTT output,
but cannot delay or change completion of a hardware command.

## Commands And Results

See [DEVICE_API_V2.md](DEVICE_API_V2.md) for the command envelope, the operation
list, result codes, deduplication rules and the positioner job model. In outline:

```bash
mosquitto_sub -t 'diseqc/+/response' -t 'diseqc/+/event/+' -t 'diseqc/+/state/+' -t 'diseqc/+/availability' -v
mosquitto_pub -q 1 -t 'diseqc/cubley-a1b2c3/command' -m '{"id":"01J8ZK4M7Q","op":"positioner.goto","position":12}'
mosquitto_pub -q 1 -t 'diseqc/cubley-a1b2c3/command' -m '{"id":"01J8ZK4M7R","op":"lnb.polarization","channel":"a","value":"v"}'
mosquitto_pub -q 1 -t 'diseqc/cubley-a1b2c3/command' -m '{"id":"01J8ZK4M7S","op":"positioner.halt"}'
```

Exactly one response is published per command. The device rejects messages on an
unexpected topic, empty or oversized payloads, and retained command messages,
which prevents a stale retained command from executing after reconnect or
reboot.

## Events And State

`event/lnb` reports non-retained asynchronous LNB transitions. LNB fault assertion
and clearing events include `schema=1 sub=lnb comp=fault`, `event_id`, `stat`, and
`source` fields. `event_id` provides event ordering; a second fault sequence is not
published.
The GPIO callback only signals a worker; register inspection and MQTT publication
run outside the interrupt callback.

`state/lnb` is a retained snapshot published on connection, after each MQTT
command, and after each fault transition. It begins with
`schema=1 sub=lnb comp=state` and includes health, communication,
fault, monitor and initialization state, register values, and channel polarization
and band when available. Consumers should use `event/lnb` for live transitions
and `state/lnb` to establish or recover current state.

`event/diseqc` and `state/diseqc` carry the JSON job contract; see
[DEVICE_API_V2.md](DEVICE_API_V2.md). The motion watchdog duration is still
adjustable only from the USB console with `diseqc timeout <5..300>` (seconds,
default 90); it is reported as `timeout_ms` in positioner state.

The LNB schema uses `sub`, `comp`, `stat`, and `comm` for subsystem,
component, status, and communication condition. Local diagnostic sequences use
`seq`. Retained state omits health and fault sequence counters because they do not
describe current condition.

The exact LNB schema-1 payload is also emitted through `Debug.WriteLine` with an
`[LNB]` prefix. Debug output remains available while MQTT is disconnected; MQTT
publication is conditional on an active connection.

## LNB Health Monitoring

An internal worker reads the LNBH26 status and data registers every 10 seconds.
All command, fault-snapshot, state-read, and health-check LNB access is serialized.
The health check skips a cycle when an LNB operation or DiSEqC transmission is
already active, so monitoring cannot interrupt operational traffic.

The worker maintains health even while MQTT is disconnected. When connected, a
changed health result updates retained `state/lnb`; unchanged state is refreshed
at least every 60 seconds. The snapshot includes `stat`, `comm`,
`health_failures`, `health_rc`, and raw `s1`, `s2`, `d1` through
`d4` register values. Communication loss and restoration publish non-retained
`comp=health operation=comms` messages with health-check `seq` to `event/lnb`.

After failed register access, checks back off from 10 to 20, 40, and at most 60
seconds. A successful check restores the normal 10-second interval and clears the
consecutive failure count.

## Connection Lifecycle

The MQTT worker remains disabled until a valid saved configuration has
`enabled=on`. It then waits for network availability and a non-`0.0.0.0` IPv4
address before connecting. Broker hostnames use the configured nanoFramework DNS
settings.

An automatic hostname is resolved as `cubley-xxxxxx`, where `xxxxxx` is the low
24 bits of a 32-bit FNV-1a hash over the full 96-bit STM32 unique device ID,
encoded as six lowercase hexadecimal characters. An empty client ID resolves to
the effective hostname. Explicit hostname and client-ID settings remain
independent, and an explicit client ID always wins. Saved configuration changes
close the active session and reconnect using the new settings.

## Configuration

MQTT service state and redacted configuration can be inspected from USB operational
mode with `show mqtt` and `show running-config mqtt`. MQTT and network settings can
be changed only through USB CDC configuration mode:

```text
cubley-a1b2c3> configure
cubley-a1b2c3(config)# hostname dish-east
cubley-a1b2c3(config*)# mqtt broker 192.168.1.50
cubley-a1b2c3(config*)# mqtt topic-prefix diseqc
cubley-a1b2c3(config*)# mqtt enabled on
cubley-a1b2c3(config*)# commit
```

The broker must be set before an enabled configuration can be committed. Credentials
are case-preserving but cannot contain spaces in schema v2. Password commands are
redacted from debug logs and configuration output. MQTT messages containing
configuration commands are rejected as unsupported.

See [CONFIGURATION.md](CONFIGURATION.md) for the complete command list and
[CONFIGURATION_STORAGE.md](CONFIGURATION_STORAGE.md) for the persisted schema.

## Scope

No per-command topic contract is defined. TLS, certificate management, and
encrypted credential storage remain deferred, as does any authentication or
authorization of MQTT commands.