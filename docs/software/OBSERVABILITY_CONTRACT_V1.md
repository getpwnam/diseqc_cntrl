# Observability Contract V1

## Purpose

This contract defines one structured message model for retained state, asynchronous
events, command responses, and `Debug.WriteLine` diagnostics. MQTT and debug output
must be projections of the same canonical payload rather than independently
formatted descriptions of the same operation.

This is the target contract. The current implementation still uses aggregate
`state` and `event` topics and several historical debug prefixes; see
[Migration](#migration).

## Subsystems

Every structured message must contain exactly one `subsystem` value from this
stable set:

| Subsystem | Ownership |
|---|---|
| `main` | Boot, reset cause, heartbeat, worker lifecycle, and process-level health |
| `config` | Configuration parsing, validation, persistence, staging, and application |
| `command` | Shared command parsing, dispatch, correlation, and completion |
| `mqtt` | Broker connection, subscription, receive, publish, and reconnect lifecycle |
| `lnb` | LNBH26 initialization, control, register state, health, and faults |
| `diseqc` | DiSEqC framing, carrier, transmission, switching, and motor operations |
| `network` | Interface state, addressing, DHCP, and DNS |
| `cdc` | USB CDC connection, console input, output, and transport errors |

Transport direction and implementation mechanism are not subsystems. Use fields
such as `component=subscribe`, `operation=publish`, `transport=mqtt`, or
`source=irq` instead of creating `mqtt-sub`, `mqtt-pub`, or interrupt subsystems.

Configuration messages use `subsystem=config` with a `domain` field such as
`domain=mqtt` or `domain=network`. Messages about the live MQTT or network service
remain owned by `subsystem=mqtt` or `subsystem=network`.

## Payload Format

Payloads are space-separated `key=value` fields encoded as ASCII. This avoids a
JSON parser and allocator on the device while remaining readable and easy to
consume on the broker.

Every message begins with these fields in this order:

```text
schema=1 subsystem=<name> component=<name>
```

Additional common fields are:

| Field | Meaning |
|---|---|
| `operation` | Action being attempted or reported |
| `status` | `ok`, `error`, `busy`, `unavailable`, or a domain state |
| `code` | Stable machine-readable result or error code |
| `id` | Requester-assigned 16-bit command ID on responses |
| `event_id` | Device-assigned event sequence |
| `source` | Origin such as `irq`, `poll`, `health`, or `command` |
| `transport` | `mqtt` or `cdc` when transport is relevant |
| `level` | `info`, `warning`, `error`, or `debug` |

Keys and enumerated values use lowercase ASCII with underscores. Boolean values
are `0` or `1`, decimal integers have no leading sign unless negative values are
meaningful, and register bytes use `0xNN`. Free-form text is not part of the
machine contract; use stable `code` values and additional structured fields.
Values that cannot satisfy this token grammar must be sanitized before emission.

Examples:

```text
schema=1 subsystem=lnb component=health status=ok sequence=187 s1=0x00 s2=0x00
schema=1 subsystem=lnb component=fault status=active source=irq event_id=731 fault=overcurrent
schema=1 subsystem=mqtt component=subscribe status=ok qos=1 message_id=14
schema=1 subsystem=config component=storage domain=mqtt operation=load status=ok generation=6
schema=1 subsystem=command component=response transport=mqtt id=42 status=ok code=ok
```

## MQTT Topics

The effective root remains `<prefix>/<hostname>`.

| Purpose | Topic | Retained | QoS |
|---|---|---:|---:|
| Command input | `<root>/command` | No | 1 |
| Correlated response | `<root>/response` | No | 1 |
| Subsystem transition | `<root>/event/<subsystem>` | No | 1 |
| Subsystem snapshot | `<root>/state/<subsystem>` | Yes | 1 |
| Availability | `<root>/availability` | Yes | 0/1 |

`response` remains unified because the command ID is the correlation key. Every
response also identifies the subsystem that owns the result. Parser and routing
failures use `subsystem=command`; successfully routed operations use the owning
domain subsystem such as `lnb`, `diseqc`, or `network`.

Each retained `state/<subsystem>` payload is a complete snapshot owned by that
subsystem. Splitting retained state prevents an LNB update from replacing network
or MQTT state. Events are deltas and must never be used as the sole source of
current state.

## Debug Alignment

Structured state, event, and response payloads are built once. The exact payload
is published to MQTT when applicable and appended unchanged to the debug prefix:

```text
[LNB] schema=1 subsystem=lnb component=health status=ok sequence=187 s1=0x00 s2=0x00
```

The bracketed prefix is for human scanning only. Consumers must parse the payload,
not the prefix. Prefixes use the uppercase subsystem name: `[MAIN]`, `[CONFIG]`,
`[COMMAND]`, `[MQTT]`, `[LNB]`, `[DISEQC]`, `[NETWORK]`, and `[CDC]`.

Messages that are only useful for local diagnostics still use the same schema and
include `level=debug`. A future MQTT verbosity setting may suppress publication of
`level=debug` messages, but it must not change payload shape or suppress retained
state, command responses, warnings, errors, or safety events.

Secrets must be redacted before the canonical payload is passed to either sink.

## Interrupt And Worker Boundary

Interrupt callbacks do not format messages, call `Debug.WriteLine`, read LNBH26
registers, or publish MQTT data. They acknowledge or latch the hardware condition
and signal the owning subsystem worker.

The worker performs register access and emits the canonical message. Interrupt
provenance is retained as a field:

```text
schema=1 subsystem=lnb component=fault source=irq status=active event_id=731
```

Faults discovered by another path use the same component and fields with a
different source, such as `source=health`. This keeps fault ownership in `lnb`
without hiding how the condition was detected.

## Migration

Current debug prefixes map to the contract as follows:

| Current prefix | Target subsystem/component |
|---|---|
| `BOOT`, `HEARTBEAT` | `main` with `component=boot` or `component=heartbeat` |
| `CDC` | `cdc` |
| `CDC-CMD` | `command` with `transport=cdc` or `transport=mqtt` |
| `CDC-LNB`, `LNB-FAULT`, `LNB-HEALTH` | `lnb` with the appropriate component |
| `MQTT`, `MQTT-CMD`, `MQTT-EVENT`, `MQTT-STATE` | `mqtt` for transport lifecycle; owning subsystem for domain payloads |
| `MQTT-CONFIG`, `NETWORK-CONFIG` | `config` with `domain=mqtt` or `domain=network` |
| `NETWORK`, `DNS` | `network` with `component=interface` or `component=dns` |

Migration should introduce one payload formatter/sink boundary first, then convert
subsystems incrementally. During migration, documentation and implementation must
clearly distinguish legacy lines from schema-1 payloads.
