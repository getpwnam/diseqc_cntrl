# MQTT API Reference

The target structured payload and subsystem ownership rules are defined in
[OBSERVABILITY_CONTRACT_V1.md](OBSERVABILITY_CONTRACT_V1.md). This document
describes the currently implemented MQTT transport; topic migration is tracked in
the observability contract.

## Transport

CubleyControl uses MQTT 3.1.1 without TLS. It connects through the STM32F407
Ethernet MAC and LAN8742A PHY after IPv4 and DNS are ready.

The configured topic prefix is `diseqc` by default. The effective device root is
`<prefix>/<hostname>`. MQTT carries one complete operational command line rather
than using a separate topic for every operation. It shares the operational parser
with USB CDC but has a strict allowlist and does not expose administrative
configuration commands.

| Direction | Topic | Payload | QoS | Retained |
|---|---|---|---:|---|
| Command to device | `<prefix>/<hostname>/command` | `<id> <command line>` | 1 | Must be false |
| Response from device | `<prefix>/<hostname>/response` | `id=<id> <output line>` | 1 | No |
| Asynchronous transition | `<prefix>/<hostname>/event` | Event key/value fields | 1 | No |
| Current device state | `<prefix>/<hostname>/state` | State key/value fields | 1 | Yes |
| Device availability | `<prefix>/<hostname>/availability` | `online` or `offline` | 0/1 | Yes |

The broker receives a retained `online` message after connection. The configured
last will is retained `offline` at QoS 1.

## Commands And Results

Publish a command marked as MQTT-supported in
[INTERFACE_COMMAND_MAP_V1.md](INTERFACE_COMMAND_MAP_V1.md) to
`<prefix>/<hostname>/command`. Prefix the command with a requester-assigned decimal
16-bit ID and one space. Each parser output line is published separately to
`<prefix>/<hostname>/response` and carries the same ID.

Examples:

```bash
mosquitto_sub -t 'diseqc/+/response' -t 'diseqc/+/event' -t 'diseqc/+/state' -t 'diseqc/+/availability' -v
mosquitto_pub -q 1 -t 'diseqc/cubley-a1b2c3/command' -m '41 show lnb a'
mosquitto_pub -q 1 -t 'diseqc/cubley-a1b2c3/command' -m '42 lnb a pol v'
mosquitto_pub -q 1 -t 'diseqc/cubley-a1b2c3/command' -m '43 diseqc goto 12'
mosquitto_pub -q 1 -t 'diseqc/cubley-a1b2c3/command' -m '44 show capabilities'
```

The command line after the ID must be 1 to 64 ASCII bytes. The device rejects
missing or out-of-range IDs, messages on an unexpected topic, empty or oversized
payloads, and retained command messages. This prevents a stale retained command
from executing after reconnect or reboot.

QoS 1 can deliver a command more than once. The device caches the eight most
recent `{id, command, responses}` transactions in RAM. Repeating the same ID and
command replays the cached responses without executing the command again. Reusing
a cached ID for different command text returns an ID-conflict failure. Requesters
must therefore coordinate IDs when more than one publisher controls a device.

## Events And State

`event` reports non-retained asynchronous transitions. LNB fault assertion and
clearing events include `event_id`, `type=lnb_fault`, `active`, and `source` fields.
The GPIO callback only signals a worker; register inspection and MQTT publication
run outside the interrupt callback.

`state` is a retained snapshot published on connection, after each MQTT command,
and after each fault transition. It includes fault state and sequence, LNB monitor
and initialization state, channel polarization and band when available, and the
current DiSEqC preset and carrier state. Consumers should use `event` for live
transitions and `state` to establish or recover current state.

## LNB Health Monitoring

An internal worker reads the LNBH26 status and data registers every 10 seconds.
All command, fault-snapshot, state-read, and health-check LNB access is serialized.
The health check skips a cycle when an LNB operation or DiSEqC transmission is
already active, so monitoring cannot interrupt operational traffic.

The worker maintains health even while MQTT is disconnected. When connected, a
changed health result updates retained `state`; unchanged state is refreshed at
least every 60 seconds. The snapshot includes `lnb_health`, `lnb_comms`,
`health_sequence`, `health_failures`, `health_rc`, and raw `s1`, `s2`, `d1` through
`d4` register values. Communication loss and restoration publish non-retained
`type=lnb_comms` messages to `event`.

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

No JSON envelope or per-command topic contract is defined for the current MQTT
transport. TLS, certificate management, and encrypted credential storage are also
deferred.