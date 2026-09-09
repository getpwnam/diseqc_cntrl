# CubleyControl Interface Map

Status: implemented interfaces as of 2026-09-09

## Purpose

Document the USB CDC command grammar and the separate REST v2 machine interface
implemented by `software/nanoFramework/CubleyControl`. USB commands are
case-insensitive, leading and trailing whitespace is ignored, and repeated spaces
are collapsed. REST uses JSON operations rather than console command text.

## USB CDC Transport

Opening the USB CDC transport displays the product banner followed by `Console
inactive. Press Enter to activate.` Press Enter to acquire the interactive console
lease and display the prompt. There is no `cubley v1` CLI prefix.

Only one USB console may hold the lease at a time. The lease ends when the owner
runs `quit` or `logout`, disconnects, or remains inactive for ten minutes. The
console warns after nine minutes of inactivity. Only operator input refreshes the
lease; periodic `watch` output does not.

In operational mode, empty-line `Ctrl+D` also releases the console. In
configuration mode, `Ctrl+D` retains its existing meaning of leaving configuration
mode when the candidate is clean. `quit` and `logout` release the console from
either mode only when the candidate is clean. Disconnect and inactivity timeout
discard an uncommitted candidate before releasing ownership.

USB `show` commands render labeled, human-readable text. Successful setter and
action commands are silent by default; the changed prompt or subsequent `show`
output provides confirmation.

Failures are framed as:

```text
Fail: <reason>
```

In configuration mode, `debug on` enables the legacy successful-command result
framing for the current USB session. `debug off` restores quiet setters. Failures
remain visible in either mode, and detailed diagnostics are always written to the
firmware debug log.

`help` and `?` display an aligned command-and-description list for the current
mode. `help <command>` displays the corresponding subcommands and usage.

## Command Organization

The USB CDC interface has two responsibilities:

1. Operational control for normal device use.
2. Administrative configuration of network and application settings.

REST provides machine control and read-only state resources. It does not expose
the interactive console mode or configuration commands.

`Ctrl+D` releases a clean operational session. In configuration mode it behaves
like `exit`. Blank lines and lines beginning with `!` are ignored.

### Interface Matrix

| Interface | Control | Read state | Configuration |
|---|---:|---:|---:|
| USB CDC | Console commands | Human-readable `show` commands | Configuration mode |
| REST v2 | `POST /api/v2/commands` | HTTP GET resources | Not exposed |

Configuration mode limits accidental changes; it is not an authentication or
authorization boundary.

### Operational Mode

After console activation, the initial USB prompt is `<hostname>> `. Operational
mode contains normal LNB and DiSEqC control, runtime status, and local diagnostics.
Read-only administrative views remain available at this prompt; entering
configuration mode is required only to mutate configuration.

On connection, the console emits `Cubley Rotation Control v<VERSION>`. Wrapper
builds encode the three-part product version from `AssemblyVersion`, the first
eight Git commit characters, and a `.dirty` suffix when the worktree differs from
that commit, for example `1.0.0+g1a2b3c4d.dirty`. Direct project builds use
`1.0.0+unknown`. `show version` reports the same build version and Git commit.

| Command family | Purpose |
|---|---|
| `show`, `status`, `capabilities`, `version` | Inspect local runtime and build state. |
| `show lnb`, `lnb ...` | Inspect or change LNB outputs. |
| `show diseqc`, `diseqc ...` | Inspect or perform DiSEqC operations. |
| `show network`, `dns lookup` | Inspect network state or run DNS diagnostics. |
| `show running-config`, `show startup-config` | Inspect active or persisted configuration. |
| `watch`, `led`, `pulse` | Run USB or status LED diagnostics. |
| `configure` | Enter configuration mode. |
| `quit`, `logout` | Release the USB console lease. |

### Configuration Mode

`configure` takes a snapshot of the committed network and application
configuration and changes the USB prompt to `<hostname>(config)#`. Configuration
commands update only that candidate until `commit` succeeds. The prompt continues
to use the committed hostname while a different hostname is staged.

#### Device Identity

| Command | Candidate change |
|---|---|
| `hostname <name\|auto>` | Set a DNS-label hostname or derive it from a 24-bit hash of the STM32 unique device ID. |

#### Network Configuration

| Command | Candidate change |
|---|---|
| `network mode <dhcp\|static>` | Select IPv4 address assignment. |
| `network address <ipv4>` | Set the static IPv4 address. |
| `network mask <mask>` | Set the static subnet mask. |
| `network gateway <ipv4>` | Set the static gateway. |
| `network dns auto` | Obtain DNS servers automatically. |
| `network dns static <dns1> [dns2]` | Set one or two static DNS servers. |
| `network defaults` | Stage default DHCP and automatic DNS settings. |


#### Candidate Lifecycle

| Command | Behavior |
|---|---|
| `show storage` | Show the network and application configuration backends and load status. |
| `show candidate-config [network\|application\|diseqc\|all]` | Render the candidate. |
| `show config diff` | Show canonical lines added, removed, or changed relative to the committed configuration. |
| `debug <on\|off>` | Show or suppress successful setter results for the current USB session. |
| `commit` | Validate the complete candidate, persist changed domains, and activate them. |
| `discard` | Replace the candidate with the committed configuration. |
| `load defaults [network\|application\|diseqc\|all]` | Stage defaults without committing them. |
| `exit` | Return to operational mode when clean; `end` and empty-line `Ctrl+D` are equivalent. |

Only one configuration session may own the candidate. `exit` refuses dirty state
to prevent an intentional console action from silently losing work. The prompt is
`<hostname>(config)#` when clean and `<hostname>(config*)#` when the candidate
differs from the running configuration. A dirty exit remains in configuration mode and tells
the operator to use `commit` or `discard`; a second exit never implies discard. A USB
disconnect discards uncommitted changes and releases configuration mode so stale
changes cannot be committed by a later session.

Network and application settings use different persistence backends. `commit`
validates both domains before writing, but power-fail atomic persistence across
both domains is not guaranteed.
A failed multi-domain write retains the candidate and attempts to restore both
previously committed snapshots before anything is activated. A failed write that
is successfully rolled back returns `persist_failed`; `persist_partial` is reserved
for failed recovery.

#### Configuration Aliases

| Canonical form | Accepted aliases |
|---|---|
| `configure` | `config`, `conf`, `configure terminal`, `config terminal`, `conf t` |
| `network` | `net` |
| `network address` | `network addr`, `network ip` |
| `network gateway` | `network gw` |
| `show running-config` | `show run` |
| `show startup-config` | `show start` |
| `show candidate-config` | `show candidate`, `show cand` |
| `show config diff` | `show diff` |
| `show storage` | `show configuration-storage`, `show config-storage` |
| `commit` | `apply` |
| `discard` | `abort` |
| `exit` | `end`, empty-line `Ctrl+D` |

### Textual Configuration

`show running-config` and `show startup-config` produce deterministic,
line-oriented configuration text that can be pasted into configuration mode.
The output uses canonical commands only, includes explicit defaults, and has a
version header. Blank lines and lines beginning with `!` are ignored on input.

```text
! cubley-config v4 startup
hostname cubley-dish-01
network mode static
network address 192.168.1.40
network mask 255.255.255.0
network gateway 192.168.1.1
network dns static 192.168.1.1 1.1.1.1
diseqc angle-limits 50 50
diseqc step-calibration 0.112658 0.112658
diseqc fixed-offset west 3.38
```

Configuration rendering is generated from the typed configuration objects, not
from either persistence backend's payload. Passwords are accepted on input but
are never emitted in cleartext. Consequently, normal textual output is complete
except for secret material and is not a credential backup. Replaying the redacted
password comment leaves the candidate's existing password unchanged; restoring to
a new device requires entering the password separately before `commit`.

## Operational Command Reference

## Operator Commands

| Command | Aliases | Behavior |
|---|---|---|
| `help` | `h`, `?` | List commands with brief descriptions. |
| `help <lnb\|show\|diseqc\|network\|dns\|configure>` | `help l` for LNB help | Show aligned command-specific usage. |
| `show status` | `status`, `st` | Show USB CDC and status LED health. |
| `watch [on\|off]` | `w`, values `1\|0`; omitted value means `on` | Enable or disable the periodic serial status line. |
| `show capabilities` | `capabilities`, `caps`, `show caps` | Show the current capability summary. |
| `show version` | `version`, `ver`, `show ver` | Show product, build, Git, interface, and shell identifiers. |
| `led on` | none | Drive the status LED high. |
| `led off` | none | Drive the status LED low. |
| `pulse` | none | Pulse the status LED for 100 ms. |
| `configure` | `config`, `conf`, `configure terminal`, `config terminal`, `conf t` | Enter USB configuration mode. |
| `quit` | `logout`; empty-line `Ctrl+D` in operational mode | Release the interactive console lease. |

## LNB Commands

Logical channels are `a` and `b`. Mutations always require a channel. `l` is an
alias for the `lnb` command family.

### Show

| Command | Behavior |
|---|---|
| `show` | Emit system, both LNB channels, and DiSEqC summary lines. |
| `show lnb` | Emit one summary line for each LNB channel. |
| `show lnb <a\|b>` | Emit one selected channel summary. |
| `show diseqc` | Emit routing preset, tone, carrier settings, and transmit-busy state. |

Each LNB summary includes enabled state, polarization, band, ISET range, ISW
limit, voltage, tone, low-power mode, external DiSEqC input, and fault registers.

### State Changes

| Command | Aliases | Accepted values |
|---|---|---|
| `lnb <a\|b> enable` | root `l` | none |
| `lnb <a\|b> disable` | root `l` | none |
| `lnb <a\|b> polarization <value>` | `pol`, `p`; root `l` | `vertical\|horizontal`, `v\|h` |
| `lnb <a\|b> band <value>` | `b`; root `l` | `low\|high`, `l\|h` |
| `lnb <a\|b> iset <value>` | root `l` | `default\|normal\|high\|0` or `low\|reduced\|1` |
| `lnb <a\|b> isw <value>` | root `l` | `4a\|4\|default\|high\|0` or `2.5a\|2p5a\|2_5a\|low\|reduced\|1` |

Enabling or disabling a logical channel updates that channel's native LNB output
state. Assignment commands require a value; all reads begin with `show`.

## Network And Application Configuration

Network addressing is persisted by nanoFramework. Hostname and DiSEqC
positioning settings are written to the portable schema-4 application record.
All are changed only through USB configuration mode.

| Command | Behavior |
|---|---|
| `show network` | Show active link, MAC, IPv4, and DNS state. |
| `show running-config [network\|application\|diseqc\|all]` | Show active non-default configuration. |
| `show startup-config [network\|application\|diseqc\|all]` | Show persisted configuration. |

Configuration backend and load diagnostics are available separately as
`show storage` from USB configuration mode.

The public operational grammar does not use `get` or `set`. Configuration
mutations are accepted only after entering configuration mode.

Configuration mode accepts persistent `diseqc angle-limits <east> <west>`,
`diseqc step-calibration <east> <west>`, and
`diseqc fixed-offset <east|west> <degrees>` values. `commit` writes them to the
portable application record. A fixed offset is added to the signed USALS motor
angle before direction-specific limit checking and GoToX encoding. For example,
`fixed-offset west 3.38` changes a requested `36.6` degrees east to an effective
`33.22` degrees east, encoded as `33.2` degrees.

## DiSEqC Commands

| Command | Accepted values and behavior |
|---|---|
| `diseqc goto <position>` | Go to TM-2300 stored position `0..60`. Position `0` is the motor reference; this is not an angle command. |
| `diseqc goto-angle <east\|west> <degrees>` | Send positioner command `0x6E` for an explicit motor angle. Decimal degrees are rounded to the nearest tenth, with half-step ties rounded upward, then encoded with the DiSEqC GoToX fractional lookup. A direction-specific software limit must first be configured. |
| `diseqc reference` | Send `0x6B 0x00` to move to the motor's reference position. |
| `diseqc store <position>` | Store the current physical position in motor slot `1..60` with command `0x6A`. |
| `diseqc recalculate` | Send the basic `0x6F 0x00` Set/Recalculate Positions command. For the TM-2300 receiver workflow this re-synchronizes the selected stored position and shifts the others; the DiSEqC specification defines parameter `0x00` as manufacturer-specific, so verify this behavior on the installed motor before relying on it. |
| `diseqc motor-limit <east\|west\|off>` | Send motor-internal limit command `0x66`, `0x67`, or `0x63`. East or west records the motor's current physical position as that limit. This does not configure Cubley's angular safety limits. |
| `diseqc angle-limits <east_degrees> <west_degrees>` | Set positive, direction-specific runtime limits in the protocol range through 180 degrees. Rejected during motion. The operator must choose values strictly inside the motor's physically adjusted hardware stops. |
| `diseqc angle-limits status` | Show whether angular motion is armed and both direction limits. |
| `diseqc angle-limits off` | Temporarily disable angular movement. Persisted limits are loaded again after reboot. Rejected during motion. |
| `diseqc step-calibration <east_deg_per_step> <west_deg_per_step>` | Temporarily set direction-specific step sizes with up to six decimal places. Persisted calibration is loaded again after reboot. |
| `diseqc step-calibration status` | Show step calibration and position-estimate state. |
| `diseqc step-calibration off` | Disable step calibration. |
| `diseqc step <east\|west> <steps>` | Move `1..128` steps. |
| `diseqc drive <east\|west>` | Start continuous movement. |
| `diseqc stop` | Transmit the positioner halt command. |
| `diseqc complete <motion_id>` | Release the matching motion lock after external completion detection. |
| `diseqc preset <off\|direct\|aa\|ab\|ba\|bb>` | Select the routing prefix applied to positioner commands. |
| `diseqc preset status` | Show the selected routing preset. |
| `diseqc timeout <5..300>` | Set the motion watchdog auto-stop timeout, in seconds. Rejected while a motion is in progress. |
| `diseqc timeout status` | Show the configured motion watchdog timeout, in seconds. |
| `diseqc tx <framing> <address> <command> [data_byte ...]` | Transmit 3 to 6 hexadecimal bytes. |
| `diseqc tone on [frequency_hz] [duty_percent]` | Start the carrier; defaults to 22000 Hz and 50%. Frequency range is 1000..100000 Hz and duty range is 1..99%. |
| `diseqc tone off` | Stop the carrier. |
| `diseqc tone status` | Show carrier state and settings. |
| `diseqc listen <on\|off>` | Enable or disable the channel-A LNBH26 external DiSEqC input; boolean aliases are accepted. |

The selected preset prefixes all first-class positioner commands, including
`goto`, `goto-angle`, `reference`, `store`, `recalculate`, `motor-limit`, `step`,
`drive`, and `stop`.
Raw `diseqc tx` frames are transmitted unchanged. Successful `goto`, `goto-angle`,
`step`, and `drive` commands hold a motion lock that rejects further movement and
raw frames. Step commands use a step-derived deadline capped by the configured
motion watchdog timeout; goto and drive use the configured motion watchdog timeout directly. The timeout defaults to
90 seconds and is adjustable from 5 to 300 seconds with `diseqc timeout <seconds>`.
First-class motor movement temporarily selects horizontal polarization so the
positioner receives 18 V for the full motion. The previous polarization is
restored when the motion is completed, halted, or timed out.
When the timeout elapses, the firmware transmits Halt automatically and marks the
motion complete with `completion=timeout`. `diseqc stop` is always accepted and
clears the lock after transmitting Halt. An external completion command must
include the current motion ID so a delayed signal cannot release a newer movement.

`goto-angle` uses the DiSEqC 1.2 five-byte form `E0 31 6E xx xx`.
The two data bytes contain an east (`0xE`) or west (`0xD`) direction nibble and
a whole-degree magnitude followed by the standard fractional-tenth code lookup
`0,2,3,5,6,8,A,B,D,E`. For example, east `36.6` encodes as `E2 4A`.
Input is strict unsigned decimal text with at most six fractional digits; signs,
exponent notation, and locale decimal separators are rejected because direction
is a separate argument. The local estimate remains in microdegrees so calibrated
steps can retain finer resolution than the GoToX command.

Angular software limits start disabled after boot unless persistent values were
explicitly committed. This fail-closed behavior requires the operator or future station
orchestrator to confirm the motor's adjustable hardware stops before arming a
bounded session. A configured software value is not evidence that the physical
hardware limit was measured correctly.

Motion command results and DiSEqC state/events record `command_mode`,
`requested_angle_deg`, `encoded_angle_deg`, `direction`, `movement_voltage_v`,
`position_confidence`, `estimated_angle_deg`, `position_source`,
`pending_target_deg`, `step_calibration_configured`, `east_step_deg`, and
`west_step_deg`. The retained state also records
`angle_limits_configured`, `east_limit_deg`, and `west_limit_deg`. A successful
GoToX transmission records a pending target without changing the previous
estimate. Matching external completion adopts the encoded target as
`position_confidence=estimated`. A calibrated step similarly adopts its pending
target on external completion. Stored-position movement, reference movement,
continuous drive, Halt, uncalibrated step completion, and raw positioner commands
leave the angular estimate `unknown`; watchdog expiry sets `verification_failed`.
No command-only transition is reported as `rf_verified`.

## Canonical Command IDs

Dotted IDs such as `system.version.get` and `diseqc.lnb.set.band` remain available
to internal contracts but are not executable USB command forms. External
commands use the operational grammar documented above.

## REST v2 Interface

REST is the sole network interface. It listens on TCP port 80 after the device has
a usable IPv4 address. There is no authentication or TLS.

| Request | Purpose |
|---|---|
| `POST /api/v2/commands` | Execute one JSON operation and return its result. |
| `GET /api/v2/health` | Read liveness and firmware version. |
| `GET /api/v2/state/positioner` | Read active/last jobs and position estimate. |
| `GET /api/v2/state/lnb` | Read LNB health, faults, registers, polarization, and band. |
| `GET /api/v2/jobs/{job}` | Read one of the four retained positioner jobs. |

Supported operation families are:

- `positioner.goto`, `positioner.goto_angle`, `positioner.step`,
  `positioner.drive`, `positioner.halt`, and `positioner.complete`.
- `lnb.enable`, `lnb.disable`, `lnb.polarization`, and `lnb.band`.
- `diseqc.tx`, `diseqc.preset`, and `diseqc.tone`.

REST does not expose console help, watch output, LED diagnostics, DNS lookup, or
configuration mode. Clients poll job and state GET resources for progress and
recovery. See [DEVICE_API_V2.md](DEVICE_API_V2.md) for envelope, idempotency,
validation, and job semantics.