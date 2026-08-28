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
| Response from device | `<prefix>/<hostname>/response` | `id=<id> OK`, `id=<id> Fail: ...`, or requested query output | 1 | No |
| LNB asynchronous transition | `<prefix>/<hostname>/event/lnb` | Schema-1 LNB event fields | 1 | No |
| Current LNB state | `<prefix>/<hostname>/state/lnb` | Schema-1 LNB state fields | 1 | Yes |
| DiSEqC motion transition | `<prefix>/<hostname>/event/diseqc` | Schema-1 motion event fields | 1 | No |
| Current DiSEqC state | `<prefix>/<hostname>/state/diseqc` | Schema-1 motion state fields | 1 | Yes |
| Device availability | `<prefix>/<hostname>/availability` | `online` or `offline` | 1 | Yes |

The broker receives a retained `online` message after connection. The configured
last will is retained `offline` at QoS 1.

## Commands And Results

Publish a command marked as MQTT-supported in
[INTERFACE_COMMAND_MAP_V1.md](INTERFACE_COMMAND_MAP_V1.md) to
`<prefix>/<hostname>/command`. Prefix the command with a requester-assigned decimal
16-bit ID and one space. State-changing and action commands publish one terminal
`id=<id> OK` response on success or one `id=<id> Fail: ...` response on failure.
Queries may publish requested output lines before their terminal response. Each
published line carries the same ID.

Detailed current condition is owned by retained `state/<subsystem>` topics, while
asynchronous transitions are owned by `event/<subsystem>`. The compact response
only acknowledges whether the command completed successfully; it does not repeat
state or health fields.

Examples:

```bash
mosquitto_sub -t 'diseqc/+/response' -t 'diseqc/+/event/+' -t 'diseqc/+/state/+' -t 'diseqc/+/availability' -v
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

`event/diseqc` reports motion start and completion transitions. The retained
`state/diseqc` snapshot reports `stat`, `motion_id`, `operation`, `remaining_ms`,
`completion`, `timeout_ms`, and DiSEqC path fields (`channel`, `output_enabled`,
`extm`, and `ten`) so prerequisite visibility is explicit. Successful goto, step,
and drive commands set the state busy. Further movement and raw transmit commands
fail as busy until Halt, timeout, or `diseqc complete <motion_id>` releases the
lock. The ID check prevents a stale external completion message from releasing a
newer movement. `timeout_ms` reflects the configured motion watchdog auto-stop
duration, adjustable from the USB console with `diseqc timeout <5..300>` (seconds,
default 90); it is not yet exposed as an MQTT command.

The compact schema uses `sub`, `comp`, `stat`, and `comm` for subsystem,
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

No JSON envelope or per-command topic contract is defined for the current MQTT
transport. TLS, certificate management, and encrypted credential storage are also
deferred.