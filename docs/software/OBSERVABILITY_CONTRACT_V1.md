# Observability Contract V1

## Scope

CubleyControl exposes observability through two independent surfaces:

- Local structured diagnostics written with `Debug.WriteLine`.
- REST v2 snapshots returned by HTTP GET requests.

Debug output is a bring-up and fault-diagnosis interface. It is not forwarded by
the device over the network. REST clients recover state by polling; there is no
network event or subscription interface.

## Local Structured Diagnostics

The standard local diagnostic line is:

```text
[SUBSYSTEM] schema=1 sub=<name> comp=<name> operation=<action> stat=<state> ...
```

The bracketed prefix supports human scanning. The payload carries the structured
fields. Prefix and `sub` normally refer to the same owner.

| Subsystem | Ownership |
|---|---|
| `main` | Boot, reset cause, heartbeat, and process-level health |
| `config` | Network and application configuration load/persistence |
| `command` | USB CDC and REST command parsing, dispatch, deduplication, and completion |
| `lnb` | LNBH26 initialization, control, register reads, health, and faults |
| `diseqc` | DiSEqC control, motor jobs, voltage restoration, and transmission |
| `network` | Interface state, addressing, and DNS |
| `cdc` | USB CDC worker, session, and output lifecycle |
| `rest` | HTTP listener lifecycle |

Common fields:

| Field | Meaning |
|---|---|
| `schema` | Diagnostic schema version; currently `1` |
| `sub` | Owning subsystem |
| `comp` | Component within that subsystem |
| `operation` | Action being attempted or reported |
| `stat` | Outcome or domain state, such as `ok`, `error`, `busy`, or `unavailable` |
| `code` | Stable machine-readable result or error code |
| `transport` | `cdc` or `rest` for command diagnostics |
| `request_id` | Device-local command sequence |
| `id` | REST requester-assigned command ID when applicable |
| `seq` | Subsystem-local sequence |
| `source` | Origin such as `irq`, `health`, or `command` |
| `level` | Diagnostic verbosity, including `debug` |

Keys and enumerated values use lowercase ASCII with underscores. Boolean values
use `0` or `1`; register bytes use `0xNN`. Free text is sanitized into a single
token before emission.

Examples:

```text
[CONFIG] schema=1 sub=config comp=storage domain=application operation=load stat=ok source=internal generation=6
[COMMAND] schema=1 sub=command comp=completion operation=execute stat=ok code=ok transport=rest id=goto-7 request_id=14
[LNB] schema=1 sub=lnb comp=health operation=check stat=ok seq=187 rc=0 failures=0 s1=0x00 s2=0x00 level=debug
[REST] schema=1 sub=rest comp=server operation=listen stat=ok port=80
```

Positioner job transitions are also written locally under `[DISEQC]`, using the
same v2 JSON job-event shape produced by the positioner state builders:

```text
[DISEQC] {"v":2,"sub":"diseqc","comp":"job","transition":"start","job":7,"op":"goto","state":"running","verification":"pending","remaining_ms":89750}
```

These lines remain local diagnostics and do not create a network event contract.

## REST Snapshots

REST is the sole network observability interface:

| Request | Snapshot |
|---|---|
| `GET /api/v2/health` | Liveness and firmware version |
| `GET /api/v2/state/positioner` | Active and last terminal jobs, timeout, position confidence, estimate, source, and pending target |
| `GET /api/v2/state/lnb` | LNB health, communications, faults, registers, polarization, and band |
| `GET /api/v2/jobs/{job}` | One retained positioner job |

Every successful GET response includes `v`, `boot_id`, `ok`, `code`, `ts_ms`,
and `data`. `boot_id` changes at reboot. `ts_ms` is uptime, not wall time.

The device retains four positioner jobs. An unknown or evicted job returns HTTP
404 with `code=not_found`. Controllers must use the positioner snapshot to
recover after reconnecting and must discard cached job IDs when `boot_id`
changes.

Polling frequency is a client policy. Clients should poll job state while motion
is active and read broader snapshots at startup, after reconnect, or when an
operation result requires reconciliation.

See [DEVICE_API_V2.md](DEVICE_API_V2.md) for response shapes and job semantics.

## Interrupt And Worker Boundary

Interrupt callbacks acknowledge or latch hardware state and signal the owning
worker. They do not format diagnostics or read LNBH26 registers. The worker
performs register access and emits records with the original source retained,
for example `source=irq` or `source=health`.

## Data Handling

Diagnostics must not contain secrets. REST has no authentication or TLS, so TCP
port 80 must be restricted to trusted controllers and networks.
