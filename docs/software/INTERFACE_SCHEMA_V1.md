# Unified Interface Schema v1

Status: draft for implementation

## Why JSON and not YAML as source of truth

Use JSON as the canonical machine contract and JSON Schema for validation.

1. MQTT payloads are naturally JSON-friendly.
2. CLI output can emit the exact same JSON objects.
3. JSON Schema tooling is mature for validation, test fixtures, and CI checks.
4. YAML can still be used for authoring examples or operator-facing docs, but it should be generated from or validated against the JSON schema.

## Canonical Schema Files

All v1 schemas live under `docs/software/schema/v1/`.

1. `common.schema.json` shared enums and reusable scalar constraints.
2. `command-args.schema.json` argument contracts for every canonical command ID.
3. `request-envelope.schema.json` transport-agnostic command request envelope.
4. `result-envelope.schema.json` transport-agnostic command/result envelope.
5. `state.schema.json` canonical runtime state snapshot payload.
6. `config.schema.json` effective config snapshot payload.
7. `capabilities.schema.json` capability registry payload.
8. `version.schema.json` version metadata payload.
9. `ack.schema.json` mutation acknowledgement payload.
10. `lnbh26.schema.json` LNBH26 register decode payload for `show lnb` style diagnostics.

Command transport parity mapping is defined in:

1. `docs/software/INTERFACE_COMMAND_MAP_V1.md`

## Transport Binding Rule

Both transports must carry the same payload model.

1. CLI: print one JSON document per command result.
2. MQTT: publish the same JSON document on the mapped topic.

The transport layer may differ in framing and addressing, but payload shape must remain schema-compatible.

## CLI Example

Command:

```text
show state json
```

Request payload emitted by parser before execution (valid `request-envelope.schema.json`):

```json
{
  "command": "diseqc.rotor.goto_angle",
  "domain": "diseqc",
  "req_id": "cli-00043",
  "args": {
    "angle_deg": 28.2
  }
}
```

Response payload (valid `result-envelope.schema.json` with `state.schema.json` in `data`):

```json
{
  "ok": true,
  "code": "ok",
  "msg": "state snapshot",
  "ts": "2026-07-16T12:00:00Z",
  "req_id": "cli-00042",
  "command": "system.state.get",
  "domain": "system",
  "data": {
    "schema": "cubley/v1/state",
    "availability": "online",
    "busy": false,
    "rotor": {
      "state": "idle",
      "position": {
        "angle_deg": 19.2,
        "satellite": "astra_19.2e"
      }
    },
    "lnb": {
      "voltage": 13,
      "polarization": "vertical",
      "tone": "off",
      "band": "low"
    },
    "config": {
      "saved": true,
      "persisted": true,
      "reload_source": "fram",
      "updated_key": "mqtt.broker"
    },
    "error": ""
  }
}
```

## MQTT Example

Topic:

```text
cubley/v1/system/state/snapshot
```

Payload:

```json
{
  "ok": true,
  "code": "ok",
  "msg": "state snapshot",
  "ts": "2026-07-16T12:00:00Z",
  "req_id": "mqtt-a8e1b",
  "command": "system.state.get",
  "domain": "system",
  "data": {
    "schema": "cubley/v1/state",
    "availability": "online",
    "busy": false,
    "rotor": {
      "state": "idle",
      "position": {
        "angle_deg": 19.2,
        "satellite": "astra_19.2e"
      }
    },
    "lnb": {
      "voltage": 13,
      "polarization": "vertical",
      "tone": "off",
      "band": "low"
    },
    "config": {
      "saved": true,
      "persisted": true,
      "reload_source": "fram",
      "updated_key": "mqtt.broker"
    },
    "error": ""
  }
}
```

## Notes

1. Additive fields are allowed for forward compatibility.
2. Required core fields in the envelope remain stable in v1.
3. Canonical key style is `snake_case` for payload data fields.