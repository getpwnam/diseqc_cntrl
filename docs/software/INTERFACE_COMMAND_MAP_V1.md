# CubleyControl Command Map

Status: implemented command set as of 2026-08-27

## Purpose

Document the command grammar implemented by `software/nanoFramework/CubleyControl`.
USB CDC and MQTT pass command text to the same parser after transport policy is
applied. Commands are case-insensitive, leading and trailing whitespace is ignored,
and repeated spaces are collapsed.

## USB CDC Transport

Opening the USB CDC transport displays the product banner followed by `Console
inactive. Press Enter to activate.` Press Enter to acquire the interactive console
lease and display the prompt. There is no `cubley v1` CLI prefix.

Only one interactive console may hold the lease at a time. A later USB, SSH, or
other interactive transport must wait until the owner runs `quit` or `logout`,
disconnects, or remains inactive for ten minutes. The console warns after nine
minutes of inactivity. Only input received from the operator refreshes the lease;
periodic `watch` output and other asynchronous output do not. MQTT commands are
stateless and do not acquire the interactive console lease.

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
firmware debug log. MQTT retains machine-oriented result framing.

`help` and `?` display an aligned command-and-description list for the current
mode. `help <command>` displays the corresponding subcommands and usage.

## Command Organization

The command interface has two responsibilities:

1. Operational control for normal device use.
2. Administrative configuration of the network and MQTT service.

The USB CDC console supports both responsibilities through separate command modes.
MQTT supports only self-contained operational commands. Configuration mode is
session state and must never be shared between USB CDC and MQTT.

### Console Command Tree

This tree is the compact index of the accepted USB console grammar. Angle brackets
denote required values, square brackets denote optional values, and `|` separates
alternatives. Keep it synchronized with `Program.Commands.cs` and the owning
`Program.Commands.*.cs` handlers whenever command parsing changes.

```text
CubleyControl console
|
|-- Operational mode
|   |
|   |-- help|h|? [topic]
|   |     Show command help.
|   |
|   |-- show
|   |   |-- show
|   |   |     Display system, both LNB channels, and DiSEqC summary.
|   |   |-- lnb [a|b]
|   |   |     Display LNB state.
|   |   |-- diseqc
|   |   |     Display routing preset, carrier, transmitter, and motion state.
|   |   |-- network|net
|   |   |     Display live network interface state.
|   |   |-- mqtt
|   |   |     Display live MQTT service state.
|   |   |-- running-config|run [network|net|mqtt|mq]
|   |   |     Display active configuration.
|   |   |-- startup-config|start [network|net|mqtt|mq]
|   |   |     Display persisted configuration.
|   |   |-- status
|   |   |     Display USB console and status LED health.
|   |   |-- capabilities|caps
|   |   |     Display supported transports and configuration capabilities.
|   |   `-- version|ver
|   |         Display product, firmware, Git, interface, and shell versions.
|   |
|   |-- status|st
|   |     Short form of "show status".
|   |-- capabilities|caps
|   |     Short form of "show capabilities".
|   |-- version|ver
|   |     Short form of "show version".
|   |
|   |-- lnb|l <a|b>
|   |   |-- enable
|   |   |     Enable the selected LNB output.
|   |   |-- disable
|   |   |     Disable the selected LNB output.
|   |   |-- polarization|pol|p <vertical|v|horizontal|h>
|   |   |     Set LNB polarization.
|   |   |-- band|b <low|l|high|h>
|   |   |     Set LNB frequency band.
|   |   |-- iset <default|normal|high|0|low|reduced|1>
|   |   |     Set the LNB current range.
|   |   `-- isw <4a|4|default|high|0|2.5a|2p5a|2_5a|low|reduced|1>
|   |         Set the switch current limit.
|   |
|   |-- diseqc
|   |   |-- goto <0..60>
|   |   |     Move to a TM-2300 stored position; position 0 is the reference.
|   |   |-- goto-angle <east|west> <0..180 degrees>
|   |   |     Move to a GoToX angular position within configured software limits.
|   |   |-- reference
|   |   |     Move to motor reference position 0.
|   |   |-- store <1..60>
|   |   |     Store the current physical position in the motor.
|   |   |-- recalculate
|   |   |     Send the motor's basic re-synchronize/position-shift command.
|   |   |-- motor-limit <east|west|off>
|   |   |     Set or disable the motor's internal limits at the current position.
|   |   |-- angle-limits status|off
|   |   |     Inspect or disable the volatile angular software limits.
|   |   |-- angle-limits <east_degrees> <west_degrees>
|   |   |     Arm direction-specific software limits after checking hardware stops.
|   |   |-- step-calibration status|off
|   |   |     Inspect or disable volatile open-loop step calibration.
|   |   |-- step-calibration <east_deg_per_step> <west_deg_per_step>
|   |   |     Set direction-specific step sizes with up to six decimal places.
|   |   |-- step <east|west> <1..128>
|   |   |     Move a fixed number of steps.
|   |   |-- drive <east|west>
|   |   |     Start continuous movement.
|   |   |-- stop
|   |   |     Transmit the positioner halt command.
|   |   |-- complete <motion_id>
|   |   |     Mark the matching active motion complete.
|   |   |-- preset status
|   |   |     Display the selected routing preset.
|   |   |-- preset <off|direct|aa|ab|ba|bb>
|   |   |     Select the routing prefix for positioner commands.
|   |   |-- timeout status
|   |   |     Display the configured motion watchdog timeout.
|   |   |-- timeout <5..300>
|   |   |     Set the motion watchdog timeout in seconds.
|   |   |-- tx <framing> <address> <command> [data_byte ...]
|   |   |     Transmit a raw frame of 3 through 6 bytes.
|   |   |-- tone on [frequency_hz] [duty_percent]
|   |   |     Start the carrier; defaults to 22000 Hz and 50%.
|   |   |-- tone off|status
|   |   |     Stop or inspect the carrier.
|   |   `-- listen <on|off|1|0|true|false>
|   |         Control the channel-A external modulation input.
|   |
|   |-- dns lookup <hostname>
|   |     Resolve a hostname.
|   |-- watch|w [on|off|1|0]
|   |     Control periodic status output; omitted value means on.
|   |-- led <on|off>
|   |     Set the status LED.
|   |-- pulse
|   |     Pulse the status LED for 100 ms.
|   |-- configure|config|conf [terminal|t]
|   |     Enter USB configuration mode.
|   `-- quit|logout
|         Release the console when configuration is clean.
|
`-- Configuration mode
	|
	|-- help|h|? [topic]
	|     Show configuration command help.
	|-- hostname <name|auto>
	|     Set the hostname or derive it from the STM32 unique ID.
	|
	|-- network|net
	|   |-- mode <dhcp|static>
	|   |     Set address assignment mode.
	|   |-- address|addr|ip <ipv4>
	|   |     Set the static IPv4 address.
	|   |-- mask <mask>
	|   |     Set the static subnet mask.
	|   |-- gateway|gw <ipv4>
	|   |     Set the static gateway.
	|   |-- dns auto
	|   |     Use automatic DNS.
	|   |-- dns static <dns1> [dns2]
	|   |     Set static DNS servers.
	|   `-- default|defaults
	|         Stage network defaults.
	|
	|-- mqtt|mq
	|   |-- enabled|enable <on|off|1|0|true|false>
	|   |     Enable or disable MQTT.
	|   |-- broker|host <host|clear>
	|   |     Set or clear the broker address.
	|   |-- port <1..65535>
	|   |     Set the broker port.
	|   |-- client-id|client <id|auto>
	|   |     Set the client identifier.
	|   |-- username|user <value|clear>
	|   |     Set or clear the username.
	|   |-- password|pass <value|clear>
	|   |     Set or clear the password.
	|   |-- topic-prefix|topic <prefix>
	|   |     Set the MQTT base topic prefix.
	|   |-- keepalive|keep-alive <15..3600>
	|   |     Set keepalive seconds.
	|   |-- reconnect <1..60>
	|   |     Set reconnect delay seconds.
	|   `-- default|defaults
	|         Stage MQTT defaults.
	|
	|-- show
	|   |-- candidate-config|candidate|cand [network|net|mqtt|mq]
	|   |     Display the staged candidate.
	|   |-- diff | config diff
	|   |     Compare candidate and running configuration.
	|   |-- running-config|run [network|net|mqtt|mq]
	|   |     Display active configuration.
	|   |-- startup-config|start [network|net|mqtt|mq]
	|   |     Display persisted configuration.
	|   `-- storage|configuration-storage|config-storage
	|         Display storage backend and loading status.
	|
	|-- debug <on|off>
	|     Control successful setter output.
	|-- commit|apply
	|     Validate, persist, and activate changes.
	|-- discard|abort
	|     Abandon candidate changes.
	|-- load defaults [network|net|mqtt|mq|all]
	|     Stage defaults without committing.
	|-- defaults [network|net|mqtt|mq|all]
	|     Short form of "load defaults".
	|-- exit|end
	|     Return to operational mode when the candidate is clean.
	`-- quit|logout
		  Release the console when the candidate is clean.
```

`Ctrl+D` releases a clean operational session. In configuration mode it behaves
like `exit`. Blank lines and lines beginning with `!` are ignored.

### Transport And Mode Matrix

| Interface | Operational mode | Configuration mode | Notes |
|---|---:|---:|---|
| USB CDC | yes | yes | Initial administrative interface. |
| MQTT | yes | no | One complete operational command per non-retained message. |
| Telnet or SSH | future | future | Each connection must own an independent mode and candidate configuration. |

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

| Command family | USB CDC | MQTT | Purpose |
|---|---:|---:|---|
| `show`, `show status` | yes | yes | Show overall runtime health. |
| `show capabilities` | yes | yes | Show supported operational capabilities. |
| `show version` | yes | yes | Show firmware and interface versions. |
| `show lnb [a\|b]` | yes | yes | Inspect both channels or one selected channel. |
| `lnb <a\|b> <action> [value]` | yes | yes | Perform one LNB state change. |
| `show diseqc`, `diseqc ...` | yes | yes | Inspect or perform one DiSEqC operation. |
| `help [topic]` | yes | no | Show context-sensitive console help. |
| `watch [on\|off]` | yes | no | Control the USB periodic status display. |
| `show network`, `show mqtt` | yes | no | Inspect local service health. |
| `show running-config [network\|mqtt]` | yes | no | Render active non-default configuration with secrets redacted. |
| `show startup-config [network\|mqtt]` | yes | no | Render persisted configuration with secrets redacted. |
| `dns lookup <hostname>` | yes | no | Run a local DNS diagnostic. |
| `led on`, `led off`, `pulse` | yes | no | Run local status LED diagnostics. |
| `configure` | yes | no | Enter configuration mode. |
| `quit`, `logout` | yes | no | Release the interactive console lease. |

The `MQTT` column above is the complete command allowlist for that transport.
The MQTT dispatcher must reject every other command as `unsupported` before it
reaches the shared parser. MQTT does not retain a mode, candidate, or command
transaction between messages.

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

#### MQTT Configuration

| Command | Candidate change |
|---|---|
| `mqtt enabled <on\|off>` | Enable or disable MQTT at the next commit. |
| `mqtt broker <host\|clear>` | Set or clear the broker hostname or IPv4 address. |
| `mqtt port <1..65535>` | Set the broker port. |
| `mqtt client-id <id\|auto>` | Set an explicit client ID or use the effective hostname. |
| `mqtt username <value\|clear>` | Set or clear the username. |
| `mqtt password <value\|clear>` | Set or clear the password without echoing it. |
| `mqtt topic-prefix <prefix>` | Set the base topic prefix. |
| `mqtt keepalive <15..3600>` | Set the keepalive interval in seconds. |
| `mqtt reconnect <1..60>` | Set the reconnect interval in seconds. |
| `mqtt defaults` | Stage disabled MQTT defaults. |

#### Candidate Lifecycle

| Command | Behavior |
|---|---|
| `show storage` | Show the network and application configuration backends and load status. |
| `show candidate-config [network\|mqtt]` | Render the candidate with secrets redacted. |
| `show config diff` | Show canonical lines added, removed, or changed relative to the committed configuration. |
| `debug <on\|off>` | Show or suppress successful setter results for the current USB session. |
| `commit` | Validate the complete candidate, persist changed domains, and activate them. |
| `discard` | Replace the candidate with the committed configuration. |
| `load defaults [network\|mqtt\|all]` | Stage defaults without committing them. |
| `exit` | Return to operational mode when clean; `end` and empty-line `Ctrl+D` are equivalent. |

Only one configuration session may own the candidate. `exit` refuses dirty state
to prevent an intentional console action from silently losing work. The prompt is
`<hostname>(config)#` when clean and `<hostname>(config*)#` when the candidate
differs from the running configuration. A dirty exit remains in configuration mode and tells
the operator to use `commit` or `discard`; a second exit never implies discard. A USB
disconnect discards uncommitted changes and releases configuration mode so stale
changes cannot be committed by a later session. A future network console must use
a per-session candidate and should support confirmed commit with automatic rollback
for changes that can disconnect its own management path.

Network and MQTT currently use different persistence backends. `commit` can
validate both domains before writing either one, but power-fail atomic persistence
across both domains is not yet guaranteed and must not be claimed by the result.
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
| `mqtt` | `mq` |
| `mqtt enabled` | `mqtt enable` |
| `mqtt broker` | `mqtt host` |
| `mqtt client-id` | `mqtt client` |
| `mqtt username` | `mqtt user` |
| `mqtt password` | `mqtt pass` |
| `mqtt topic-prefix` | `mqtt topic` |
| `mqtt keepalive` | `mqtt keep-alive` |
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
! cubley-config v2
hostname cubley-dish-01
network mode static
network address 192.168.1.40
network mask 255.255.255.0
network gateway 192.168.1.1
network dns static 192.168.1.1 1.1.1.1
mqtt enabled on
mqtt broker broker.example.net
mqtt port 1883
mqtt client-id auto
mqtt username cubley
! mqtt password configured
mqtt topic-prefix dishes/site-a
mqtt keepalive 60
mqtt reconnect 5
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
| `help <lnb\|show\|diseqc\|network\|mqtt>` | `help l` for LNB help | Show aligned command-specific usage. |
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

## Network And MQTT Configuration

Network addressing is persisted by nanoFramework. MQTT settings are written to the
portable application configuration record. Both are changed only through the USB
configuration mode described above.

| Command | Behavior |
|---|---|
| `show network` | Show active link, MAC, IPv4, and DNS state. |
| `show mqtt` | Show active MQTT state, endpoint, reconnect attempts, and last error. |
| `show running-config [network\|mqtt]` | Show active configuration with passwords redacted. |
| `show startup-config [network\|mqtt]` | Show persisted configuration with passwords redacted. |

Configuration backend and load diagnostics are available separately as
`show storage` from USB configuration mode.

The public operational grammar does not use `get` or `set`. Network and MQTT
mutations are accepted only after entering configuration mode.

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
| `diseqc angle-limits off` | Disable angular movement. This is the power-on default and is rejected during motion. |
| `diseqc step-calibration <east_deg_per_step> <west_deg_per_step>` | Set volatile direction-specific step sizes with up to six decimal places. Calibration starts disabled after boot. |
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

Angular software limits are deliberately volatile and start disabled after every
boot. This fail-closed behavior requires the operator or future station
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
to internal contracts but are not executable USB or MQTT command forms. External
commands use the operational grammar documented above.

## MQTT Transport

MQTT uses the active LAN8742A IPv4/DHCP/DNS implementation. It starts only after
MQTT is enabled in saved configuration and the interface has a usable IPv4 address.
The target subsystem-owned message schema and state/event subtopics are specified
in [OBSERVABILITY_CONTRACT_V1.md](OBSERVABILITY_CONTRACT_V1.md); the table below
records the currently implemented binding.

When enabled, the current binding is:

| Direction | Topic | Payload |
|---|---|---|
| Command to device | `<prefix>/<hostname>/command` | `<uint16-id> <command>` from the MQTT operational allowlist. |
| Response from device | `<prefix>/<hostname>/response` | Terminal `id=<id> OK` or `id=<id> Fail: ...`; queries may first emit requested output lines. |
| LNB asynchronous transition | `<prefix>/<hostname>/event/lnb` | Non-retained schema-1 LNB event fields. |
| Current LNB state | `<prefix>/<hostname>/state/lnb` | Retained schema-1 LNB state fields. |
| Device availability | `<prefix>/<hostname>/availability` | Retained `online` or last-will `offline`. |

Retained, empty, malformed-ID, and greater-than-64-byte command lines are rejected.
QoS 1 duplicate commands among the eight most recent IDs replay cached responses
without executing again; reuse of a cached ID with different command text fails.
State and health details are carried by subsystem state and event topics rather
than repeated in successful command acknowledgements.
The topic prefix defaults to `diseqc` and is configurable from USB configuration
mode with `mqtt topic-prefix`.

The effective device root is `<prefix>/<hostname>`. The hostname and MQTT client ID
are configured independently. The per-command `cubley/v1/...` topics and JSON request/result envelopes described
by the interface schema files are design contracts and are not implemented by the
current MQTT transport.