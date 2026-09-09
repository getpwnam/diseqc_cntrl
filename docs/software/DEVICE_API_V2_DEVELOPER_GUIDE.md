# Cubley Device REST API v2 Developer Guide

Status: **draft and subject to change**. The API is implemented but has not yet
been fully exercised on hardware. The canonical contract is
[DEVICE_API_V2.md](DEVICE_API_V2.md).

## Transport

- Base URL: `http://<device-ip>`
- Endpoint: `POST /api/v2/commands`
- Read endpoints: `/api/v2/health`, `/api/v2/state/positioner`,
  `/api/v2/state/lnb`, and `/api/v2/jobs/{job}`
- Content type: `application/json`
- No authentication or TLS; use only on a trusted network.
- HTTP status-code mappings are not yet contractual. Clients should parse the
  JSON response envelope.

## Request Envelope

```json
{
  "v": 2,
  "id": "unique-request-id",
  "op": "positioner.goto",
  "position": 12
}
```

- `v`: Contract version; currently `2`.
- `id`: Required idempotency key, 1-32 characters matching `A-Za-z0-9._:-`.
- `op`: Required operation name.
- Remaining fields depend on the operation.

Generate a new `id` for each logical command. Retrying the exact same payload
with the same ID within 120 seconds does not execute it twice.

## Payload Restrictions

Requests must:

- Be one JSON object, no larger than 256 ASCII bytes.
- Contain at most 12 members.
- Use keys no longer than 24 characters.
- Use string values no longer than 128 characters.
- Contain only strings, integers, booleans, or `null`.
- Not contain arrays, nested objects, floating-point, or exponent numbers.
- Not contain unknown members.

Raw DiSEqC frames use uppercase hex strings, for example `"E01038F0"`.

## Operations

| Operation | Parameters |
|---|---|
| `positioner.goto` | `position`: integer 0-60; position 0 is the motor reference |
| `positioner.goto_angle` | `direction`: `"east"` or `"west"`; `angle`: decimal string from `"0"` through `"180"`, with up to six fractional digits |
| `positioner.step` | `direction`: `"east"` or `"west"`; `count`: integer 1-128 |
| `positioner.drive` | `direction`: `"east"` or `"west"` |
| `positioner.halt` | None |
| `positioner.complete` | `job`: integer; `verification`: `estimated`, `rf_verified`, or `verification_failed` |
| `lnb.enable` | `channel` |
| `lnb.disable` | `channel` |
| `lnb.polarization` | `channel`, `value` |
| `lnb.band` | `channel`, `value` |
| `diseqc.tx` | `frame`: uppercase hex, 1-6 bytes |
| `diseqc.preset` | `value` |
| `diseqc.tone` | `value`: `"on"` or `"off"` |

The accepted values for bridged LNB fields are not fully frozen in the draft.
Coordinate with the firmware team before depending on them.

Configuration operations are available only through the USB console, not REST.

## Response Envelope

```json
{
  "v": 2,
  "boot_id": "d942c94f-16fd-4d8e-b6d5-44201d3caa4c",
  "id": "unique-request-id",
  "ok": true,
  "code": "accepted",
  "job": 7,
  "ts_ms": 41231
}
```

Always present:

- `v`: Contract version.
- `boot_id`: Per-boot identity; treat job IDs from another boot as stale.
- `id`: Request ID, or `"?"` if parsing failed.
- `ok`: Whether the command was accepted or completed.
- `code`: Machine-readable result.
- `ts_ms`: Device uptime, not wall-clock time.

Optional:

- `msg`: Failure detail.
- `job`: Started or blocking positioner job.
- `data`: Structured result.
- `lines`: Newline-separated output from legacy bridged operations.
- `replayed`: The response came from the idempotency cache.

Result codes are `ok`, `accepted`, `validation_error`, `unsupported`, `busy`,
`hw_fault`, `id_conflict`, and `not_found`.

## Positioner Jobs

Motion operations return `accepted` with a job ID and continue asynchronously.
Only one motion job may run at a time; another motion request returns `busy`.

Job states:

- `running`
- `halted`
- `timeout`
- `timeout_halt_failed`
- `completed`

There is no `succeeded` state because DiSEqC does not report arrival. A
controller should detect completion externally, such as through signal lock,
then call:

```json
{
  "v": 2,
  "id": "complete-7",
  "op": "positioner.complete",
  "job": 7,
  "verification": "rf_verified"
}
```

`rf_verified` promotes the device's offset-adjusted and protocol-rounded pending
target to `position_confidence=rf_verified`; the client does not supply an
angle. Use `estimated` when motion stopped without independent verification, or
`verification_failed` when RF evidence disproves arrival. Stored-position,
uncalibrated-step, and continuous-drive jobs have no angular pending target and
cannot be marked `rf_verified`.

A watchdog eventually halts incomplete motion.

## State Queries

All read responses include `v`, `boot_id`, `ok`, `code`, `ts_ms`, and `data`.
Poll `GET /api/v2/jobs/{job}` after a motion command. A missing or evicted job
returns HTTP 404 and `code=not_found`. Use `GET /api/v2/state/positioner` to
recover the active job and position confidence after reconnecting, and
`GET /api/v2/state/lnb` for current LNB health and fault state.

`GET /api/v2/health` is the lightweight liveness endpoint. A changed `boot_id`
means cached job IDs and uptime values belong to an earlier boot.

## MQTT Notifications

REST is used for commands and immediate responses. MQTT is used for asynchronous
announcements under `<prefix>/<hostname>`.

Relevant topics:

- `event/diseqc`: Non-retained job transitions.
- `state/diseqc`: Retained current and recent job state.
- `availability`: Retained `online` or `offline` state.

Use state messages to recover after reconnecting and event messages for live
transitions. See [MQTT_API.md](MQTT_API.md) for transport details.

## Example

```bash
curl -X POST "http://<device-ip>/api/v2/commands" \
  -H "Content-Type: application/json" \
  --data '{"v":2,"id":"goto-12-001","op":"positioner.goto","position":12}'
```

## Calibrated GoToX Sequence

The following sequence enables LNB output A, references the motor, and then
moves to the Cheltenham Astra 2 GoToX angle. It requires `jq`, persisted GoToX
offset and angular limits, and a fresh request ID for every logical command.
Only complete each job after observing that the motor has physically stopped.

```bash
BASE_URL="http://<device-ip>/api/v2/commands"
run_id="cal-$(date +%s)"

curl -fsS "$BASE_URL" \
  -H 'Content-Type: application/json' \
  --data "{\"v\":2,\"id\":\"$run_id-lnb\",\"op\":\"lnb.enable\",\"channel\":\"a\"}"

reference_response=$(curl -fsS "$BASE_URL" \
  -H 'Content-Type: application/json' \
  --data "{\"v\":2,\"id\":\"$run_id-ref\",\"op\":\"positioner.goto\",\"position\":0}")
printf '%s\n' "$reference_response" | jq .
reference_job=$(printf '%s\n' "$reference_response" |
  jq -er 'select(.ok and .code == "accepted") | .job')

# Wait for the motor to stop at reference before completing this job.
curl -fsS "$BASE_URL" \
  -H 'Content-Type: application/json' \
  --data "{\"v\":2,\"id\":\"$run_id-ref-complete\",\"op\":\"positioner.complete\",\"job\":$reference_job,\"verification\":\"estimated\"}"

goto_response=$(curl -fsS "$BASE_URL" \
  -H 'Content-Type: application/json' \
  --data "{\"v\":2,\"id\":\"$run_id-astra2\",\"op\":\"positioner.goto_angle\",\"direction\":\"east\",\"angle\":\"36.6\"}")
printf '%s\n' "$goto_response" | jq .
goto_job=$(printf '%s\n' "$goto_response" |
  jq -er 'select(.ok and .code == "accepted") | .job')

# Wait for physical cessation and verify the known Astra 2 signal first.
curl -fsS "$BASE_URL" \
  -H 'Content-Type: application/json' \
  --data "{\"v\":2,\"id\":\"$run_id-astra2-complete\",\"op\":\"positioner.complete\",\"job\":$goto_job,\"verification\":\"rf_verified\"}"
```

Do not complete a job if arrival is uncertain. Use `positioner.halt` instead; a
Halt or watchdog timeout deliberately prevents the pending target from becoming
an estimated position.