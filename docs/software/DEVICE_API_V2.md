# Device API v2 — JSON Command Envelope And Positioner Jobs

Status: **draft contract**, implemented on the device but not yet exercised on
hardware. REST is the only network interface.

## Why this exists

The previous machine command envelope was
`<decimal id> <console command line>`: the human console grammar used as a wire
format, capped at 64 bytes, with a 16-bit requester-assigned ID as its first
token. Two problems followed from that:

1. The machine API and the operator CLI shared one grammar, so neither could
   change without breaking the other.
2. Long-running positioner movement had no representation. `diseqc goto 12`
   returned `OK` when the *frame was transmitted*, and the only ways to learn
   that motion had ended were a watchdog deadline or the client telling the
   device it had finished (`diseqc complete <motion_id>`) — an inversion of
   who knows what.

v2 separates the two layers. The USB CDC console keeps its Cisco IOS-style
grammar unchanged and remains the primary operator and bring-up interface. The
network transport carries JSON. Both drive the same hardware paths.

## Layering

```
USB CDC  ──  IOS-style CLI  ──┐
                              ├──>  operation  ──>  DiSEqC / LNB hardware
REST     ──  JSON envelope  ──┘
```

Nothing in this document changes the console. An operator types
`diseqc goto 12` at the serial prompt exactly as before.

## Transport

Send commands with `POST /api/v2/commands` over HTTP. The request and response
content type is `application/json`. A command result is returned in the HTTP
response body.

Read current device state with:

- `GET /api/v2/health`
- `GET /api/v2/state/positioner`
- `GET /api/v2/state/lnb`
- `GET /api/v2/jobs/{job}`

### Payload constraints

The device parses a deliberately small JSON subset. This is a hard contract,
not an implementation detail — it is what makes the parser bounded on a
192 KB-RAM target.

- The payload MUST be a single JSON object.
- Values MUST be string, integer, boolean, or null. **No nested objects, no
  arrays, no floating point.** A fractional or exponent number is rejected.
- At most 12 members; keys at most 24 characters; string values at most 128
  characters.
- The whole payload MUST be at most 256 bytes of ASCII.
- Unknown members are rejected rather than ignored, so a typo fails loudly
  instead of silently doing the wrong thing to a motor.

Byte strings (raw DiSEqC frames) are carried as uppercase hex strings rather
than arrays, e.g. `"frame":"E01038F0"`.

## Command envelope

```json
{"v":2,"id":"01J8ZK4M7Q","op":"positioner.goto","position":12}
```

| Member | Type | Required | Meaning |
|---|---|---|---|
| `v` | int | no (default 2) | Contract version. Only `2` is accepted. |
| `id` | string | **yes** | Requester-assigned idempotency key, 1–32 chars of `A-Za-z0-9._:-`. |
| `op` | string | **yes** | Operation name, 1–40 chars of `a-z0-9._`. |
| … | | | Operation-specific parameters. |

### `id` is an idempotency key, not a correlation tag

HTTP clients can retry after a timeout without knowing whether the device
executed the first request. The requester-assigned `id` prevents a retry of a
non-idempotent operation such as `positioner.step` from executing twice.

### Deduplication

The device remembers the 8 most recent `{id, raw payload, response}`
transactions for **120 seconds**.

- Same `id`, byte-identical payload, within the window → the original response
  is returned verbatim with an added `"replayed":true` member, and the
  command is **not** executed again. `ok` and `code` stay as they originally
  were, so a replay is never mistaken for a fresh execution.
- Same `id`, different payload, within the window → `id_conflict`, nothing
  executes.
- Any `id` older than the window, or evicted by the 8-entry ring → treated as
  new and executed.

The TTL is what makes this predictable: under the v1 slot-count-only scheme,
whether a reused ID replayed or conflicted depended on how many unrelated
commands had happened since. Requesters should still use unique IDs (a ULID or
UUID is ideal, and 32 chars is enough for either).

## Response envelope

Exactly one response object is returned per HTTP request.

```json
{"v":2,"boot_id":"d942c94f-16fd-4d8e-b6d5-44201d3caa4c","id":"01J8ZK4M7Q","ok":true,"code":"accepted","job":7,"ts_ms":41231}
```

| Member | Type | Always | Meaning |
|---|---|---|---|
| `v` | int | yes | Always `2`. |
| `boot_id` | string | yes | Changes on every boot. Job IDs are meaningful only within this boot. |
| `id` | string | yes | Echo of the command `id`, or `"?"` if the envelope was unparseable. |
| `ok` | bool | yes | Whether the command was accepted and completed. |
| `code` | string | yes | Machine-readable outcome, see below. |
| `ts_ms` | int | yes | Device uptime in milliseconds when the response was formed. Monotonic within a boot; **not** wall clock, and it restarts at reboot. |
| `msg` | string | on failure | Short human-readable detail. |
| `job` | int | positioner ops | Job the command started, or the job that blocked it. |
| `data` | object | structured ops | Operation result. Outbound only, so unlike a command payload it may contain nested objects. |
| `lines` | string | bridged ops | Console output, `\n`-separated. See *Bridged operations*. |

### Result codes

| `code` | `ok` | Meaning |
|---|---|---|
| `ok` | true | Completed. Terminal, nothing further happens. |
| `accepted` | true | A job was started. Motion continues after this response. |
| `validation_error` | false | Malformed envelope, unknown member, bad parameter. |
| `unsupported` | false | Unknown `op`, or an op not permitted on this transport. |
| `busy` | false | A positioner job is already running; `job` names it. |
| `hw_fault` | false | Hardware or transmission failure. |
| `id_conflict` | false | `id` reused within the dedup window with a different payload. |
| `not_found` | false | Referenced job is unknown or has been evicted. |

## Positioner job model

A job is created by any command that starts motion, and it is the only thing
that represents "the dish is moving".

### States

| State | Terminal | Meaning |
|---|---|---|
| `running` | no | Frame transmitted; the positioner is presumed moving. |
| `halted` | yes | `positioner.halt` stopped it, or a `stop` was issued at the console. |
| `timeout` | yes | The motion watchdog expired and the device transmitted Halt. |
| `timeout_halt_failed` | yes | The watchdog expired but the Halt transmission failed. **The positioner may still be moving.** |
| `completed` | yes | A client asserted that motion finished and supplied a verification result. |

**There is deliberately no `succeeded` state.** A DiSEqC 1.2 positioner returns
no arrival indication, so the device cannot know the dish reached its target —
it only knows what it transmitted and how much time has passed. `completed`
records a client's result, not motor feedback from the device. A consumer can
report RF verification obtained out of band when it calls
`positioner.complete`.

`timeout` is the normal terminal state for `positioner.drive`, which has no
intrinsic duration, and the fallback for `goto` and `step`.

### Lifecycle

```
positioner.goto/goto_angle/step/drive ──> running ──┬── positioner.halt ────> halted
                                                    ├── watchdog expiry ────> timeout
                                                    │                         timeout_halt_failed
                                                    └── positioner.complete ─> completed
```

Only one job runs at a time. A motion command while a job is `running` fails
with `busy` and names the active job; it does not queue. The device retains the
 4 most recent job records for state queries and release validation.

### Job object

```json
{"job":7,"op":"goto","state":"running","verification":"pending","remaining_ms":83400,"timeout_ms":90000,"detail":""}
```

`remaining_ms` is 0 for terminal states. `detail` carries a failure reason for
`timeout_halt_failed` and is empty otherwise.

`verification` is `pending` while running, `estimated`, `rf_verified`, or
`verification_failed` after client completion, and `none` for other terminal
paths.

## Operations

### Positioner — structured

| `op` | Parameters | Response |
|---|---|---|
| `positioner.goto` | `position` int 0–60 | `accepted` + `job`, or `busy` |
| `positioner.goto_angle` | `direction` `"east"`\|`"west"`, `angle` decimal string from `"0"` through `"180"` with up to six fractional digits | `accepted` + `job`, or `busy` |
| `positioner.step` | `direction` `"east"`\|`"west"`, `count` int 1–128 | `accepted` + `job`, or `busy` |
| `positioner.drive` | `direction` `"east"`\|`"west"` | `accepted` + `job`, or `busy` |
| `positioner.halt` | — | `ok`; terminates the active job as `halted` |
| `positioner.complete` | `job` int, `verification` string | `ok`; terminates that job as `completed` |

`positioner.complete` checks the job id, so a late completion for a superseded
job is rejected rather than ending a newer movement. `verification` accepts:

- `estimated`: adopt a GoToX or calibrated-step pending target with estimated
  confidence.
- `rf_verified`: adopt that pending target with RF-verified confidence. The
  command is rejected if the job has no angular pending target.
- `verification_failed`: invalidate the position estimate.

The device always adopts its offset-adjusted, protocol-rounded pending target;
the client cannot inject an arbitrary angle. Stored-position, uncalibrated-step,
and continuous-drive jobs cannot be marked `rf_verified`.

### REST queries

Read-only responses use this envelope and do not require a request ID:

```json
{"v":2,"boot_id":"d942c94f-16fd-4d8e-b6d5-44201d3caa4c","ok":true,"code":"ok","ts_ms":41500,"data":{}}
```

`GET /api/v2/jobs/{job}` returns a retained job object. The device retains the
four most recent jobs; an unknown or evicted ID returns HTTP 404 with
`code=not_found`. `GET /api/v2/state/positioner` returns the active and most
recent terminal jobs plus the current position confidence, estimated angle,
source, and pending target. `GET /api/v2/state/lnb` returns the current LNB
health, communication, fault, register, polarization, and band snapshot.
`GET /api/v2/health` returns liveness and the firmware version.

`angle` is a string because inbound JSON deliberately rejects floating-point
numbers. The operation applies the persisted signed GoToX offset, enforces the
configured direction-specific angular limit, and uses the same DiSEqC rounding
and motion-lock path as console `diseqc goto-angle`.

### Bridged operations

These ops are accepted on the JSON transport but are still executed by the
legacy console tokenizer, and return console text in `lines` rather than a
structured `data` object:

| `op` | Parameters | Console equivalent |
|---|---|---|
| `lnb.enable` / `lnb.disable` | `channel` | `lnb <ch> enable\|disable` |
| `lnb.polarization` | `channel`, `value` | `lnb <ch> polarization <value>` |
| `lnb.band` | `channel`, `value` | `lnb <ch> band <value>` |
| `diseqc.tx` | `frame` hex string, 1–6 bytes | `diseqc tx <bytes>` |
| `diseqc.preset` | `value` | `diseqc preset <value>` |
| `diseqc.tone` | `value` `"on"`\|`"off"` | `diseqc tone <value>` |

The bridge is a migration stage, not the destination. It exists so the wire
contract could be finalised in one change without rewriting every handler
untested. Moving an op from `lines` to `data` is a **breaking change and
requires a contract version bump** — v2 freezes the shapes above.

Configuration commands remain USB-only and are not exposed by REST.

## Versioning

`v` is the contract version and is checked on every inbound command. Additive,
non-breaking changes (a new op, a new optional response member) keep `v` at 2.
Anything that changes the meaning or shape of an existing member — including
migrating an op from `lines` to `data` — increments it.

## Known gaps

- REST has no authentication or authorization. Network access to TCP port 80
  must be restricted to trusted controllers.
- REST has no TLS.
- `ts_ms` is an uptime tick, not wall clock. There is no RTC.
- REST clients must poll state and job resources; there is no push notification
  interface.
