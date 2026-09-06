# Device API v2 — JSON Command Envelope And Positioner Jobs

Status: **draft contract**, implemented on the device but not yet exercised on
hardware. Supersedes the positional MQTT envelope described in
[MQTT_API.md](MQTT_API.md) for the `command` and `response` topics, and adds a
positioner job resource to `event/diseqc` and `state/diseqc`.

## Why this exists

The v1 MQTT envelope was `<decimal id> <console command line>` — the human
console grammar used as a wire format, capped at 64 bytes, with a 16-bit
requester-assigned ID as its first token. Two problems followed from that:

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
MQTT     ──  JSON envelope  ──┘
```

Nothing in this document changes the console. An operator types
`diseqc goto 12` at the serial prompt exactly as before.

## Transport

Unchanged from v1: MQTT 3.1.1, no TLS, QoS 1, topic root `<prefix>/<hostname>`.

| Direction | Topic | Payload | Retained |
|---|---|---|---|
| Command to device | `<root>/command` | v2 command object | Must be false |
| Response from device | `<root>/response` | v2 response object, **one per command** | No |
| Positioner job transition | `<root>/event/diseqc` | v2 event object | No |
| Current positioner state | `<root>/state/diseqc` | v2 state object | Yes |
| LNB transition / state | `<root>/event/lnb`, `<root>/state/lnb` | unchanged schema-1 key/value text | see MQTT_API.md |
| Availability | `<root>/availability` | `online` / `offline` | Yes |

LNB event and state topics are **not** migrated by this change and keep their
schema-1 key/value form.

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

QoS 1 is at-least-once: the broker may redeliver a command, and a redelivered
`positioner.step` is byte-identical to a genuine second one. Only the publisher
knows which it is, so the publisher labels it. This is the same role
`Idempotency-Key` plays in an HTTP API, and it would still be required if this
were REST.

It is not carried in MQTT 5 `Correlation Data` because the nanoFramework M2Mqtt
client decodes that property off the wire and then discards it before raising
`MqttMsgPublishReceived` — `MqttMsgPublishEventArgs` exposes only topic,
payload, dup flag, QoS and retain. Until that is fixed upstream, an inbound
correlation token has to live in the payload regardless of protocol version.

### Deduplication

The device remembers the 8 most recent `{id, raw payload, response}`
transactions for **120 seconds**.

- Same `id`, byte-identical payload, within the window → the original response
  is republished verbatim with an added `"replayed":true` member, and the
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

Exactly one response is published per accepted command.

```json
{"v":2,"id":"01J8ZK4M7Q","ok":true,"code":"accepted","job":7,"ts_ms":41231}
```

| Member | Type | Always | Meaning |
|---|---|---|---|
| `v` | int | yes | Always `2`. |
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
| `released` | yes | A client asserted that motion finished. |

**There is deliberately no `succeeded` state.** A DiSEqC 1.2 positioner returns
no arrival indication, so the device cannot know the dish reached its target —
it only knows what it transmitted and how much time has passed. `released`
records a *client's claim* of arrival, not a device observation. Any consumer
that needs true arrival must establish it out of band (signal lock, for
example) and then call `positioner.release`.

`timeout` is the normal terminal state for `positioner.drive`, which has no
intrinsic duration, and the fallback for `goto` and `step`.

### Lifecycle

```
positioner.goto/step/drive ──> running ──┬── positioner.halt ────> halted
                                         ├── watchdog expiry ────> timeout
                                         │                         timeout_halt_failed
                                         └── positioner.release ─> released
```

Only one job runs at a time. A motion command while a job is `running` fails
with `busy` and names the active job; it does not queue. The device retains the
4 most recent job records, so a terminal job stays queryable via
`positioner.job` until evicted.

### Job object

```json
{"job":7,"op":"goto","state":"running","remaining_ms":83400,"timeout_ms":90000,"detail":""}
```

`remaining_ms` is 0 for terminal states. `detail` carries a failure reason for
`timeout_halt_failed` and is empty otherwise.

## Operations

### Positioner — structured

| `op` | Parameters | Response |
|---|---|---|
| `positioner.goto` | `position` int 0–255 | `accepted` + `job`, or `busy` |
| `positioner.step` | `direction` `"east"`\|`"west"`, `count` int 1–128 | `accepted` + `job`, or `busy` |
| `positioner.drive` | `direction` `"east"`\|`"west"` | `accepted` + `job`, or `busy` |
| `positioner.halt` | — | `ok`; terminates the active job as `halted` |
| `positioner.release` | `job` int | `ok`; terminates that job as `released` |
| `positioner.job` | `job` int | `ok` + `data` = job object, or `not_found` |
| `positioner.show` | — | `ok` + `data` = current state object |

`positioner.release` checks the job id, so a late release for a superseded job
is rejected rather than ending a newer movement.

### Bridged operations

These ops are accepted on the JSON transport but are still executed by the
legacy console tokenizer, and return console text in `lines` rather than a
structured `data` object:

| `op` | Parameters | Console equivalent |
|---|---|---|
| `system.status` | — | `status` |
| `system.version` | — | `version` |
| `system.capabilities` | — | `capabilities` |
| `lnb.show` | `channel` `"a"`\|`"b"` (optional) | `show lnb [a\|b]` |
| `lnb.enable` / `lnb.disable` | `channel` | `lnb <ch> enable\|disable` |
| `lnb.polarization` | `channel`, `value` | `lnb <ch> polarization <value>` |
| `lnb.band` | `channel`, `value` | `lnb <ch> band <value>` |
| `diseqc.tx` | `frame` hex string, 1–6 bytes | `diseqc tx <bytes>` |
| `diseqc.preset` | `value` | `diseqc preset <value>` |
| `diseqc.tone` | `value` `"on"`\|`"off"` | `diseqc tone <value>` |
| `diseqc.show` | — | `show diseqc` |

The bridge is a migration stage, not the destination. It exists so the wire
contract could be finalised in one change without rewriting every handler
untested. Moving an op from `lines` to `data` is a **breaking change and
requires a contract version bump** — v2 freezes the shapes above.

Configuration commands remain unavailable on MQTT, as in v1. Configuration is
USB-only.

## Events

`event/diseqc`, non-retained, one per job transition:

```json
{"v":2,"event_id":14,"sub":"diseqc","comp":"job","transition":"start",
 "job":7,"op":"goto","state":"running","remaining_ms":89750}
```

`transition` is `start` or `end`. `event_id` is a monotonic counter for
ordering within a boot.

## State

`state/diseqc`, retained, republished on connect, after each command, and on
each job transition:

```json
{"v":2,"sub":"diseqc","comp":"state","busy":true,"timeout_ms":90000,
 "active":{"job":7,...},"last":{"job":6,...}}
```

`active` is the running job or null; `last` is the most recent terminal job or
null. Consumers should use `event/diseqc` for live transitions and
`state/diseqc` to establish or recover current state after a reconnect.

Because `active` and `last` are nested objects, `state/diseqc` is **outbound
only** — it is written by the device, and is outside the flat-object subset the
inbound parser accepts. Nothing needs to parse it back on the device.

## Versioning

`v` is the contract version and is checked on every inbound command. Additive,
non-breaking changes (a new op, a new optional response member) keep `v` at 2.
Anything that changes the meaning or shape of an existing member — including
migrating an op from `lines` to `data` — increments it.

## Known gaps

- No authentication or authorization on the MQTT transport. Anyone who can
  publish to `<root>/command` can move the motor. Access control is expected to
  live in the client that fronts this device, not on the MCU.
- No TLS. Unchanged from v1 and still deferred.
- `ts_ms` is an uptime tick, not wall clock. There is no RTC.
- LNB event and state topics are still schema-1 key/value text.
