# CubleyControl Command Map

Status: implemented command set as of 2026-08-26

## Purpose

Document the command grammar implemented by `software/nanoFramework/CubleyControl`.
USB CDC and MQTT pass command text to the same parser after transport policy is
applied. Commands are case-insensitive, leading and trailing whitespace is ignored,
and repeated spaces are collapsed.

The canonical IDs declared in `Contracts/CommandIds.cs` are internal contract
identifiers and are not accepted as external command text.

## USB CDC Transport

Enter commands directly at the USB CDC prompt. There is no `cubley v1` CLI prefix.

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

### Transport And Mode Matrix

| Interface | Operational mode | Configuration mode | Notes |
|---|---:|---:|---|
| USB CDC | yes | yes | Initial administrative interface. |
| MQTT | yes | no | One complete operational command per non-retained message. |
| Telnet or SSH | future | future | Each connection must own an independent mode and candidate configuration. |

Configuration mode limits accidental changes; it is not an authentication or
authorization boundary.

### Operational Mode

The initial USB prompt is `cubley>`. Operational mode contains normal LNB and
DiSEqC control, runtime status, and local diagnostics. Read-only administrative
views remain available at this prompt; entering configuration mode is required
only to mutate configuration.

| Command family | USB CDC | MQTT | Purpose |
|---|---:|---:|---|
| `show`, `show status` | yes | yes | Show overall runtime health. |
| `show capabilities` | yes | yes | Show supported operational capabilities. |
| `show version` | yes | yes | Show firmware and interface versions. |
| `show lnb [a\|b] [detail]` | yes | yes | Inspect both channels or one selected channel. |
| `lnb <a\|b> <field> <value>` | yes | yes | Perform one LNB state assignment. |
| `show diseqc`, `diseqc ...` | yes | yes | Inspect or perform one DiSEqC operation. |
| `help [topic]` | yes | no | Show context-sensitive console help. |
| `watch [on\|off]` | yes | no | Control the USB periodic status display. |
| `show network`, `show mqtt` | yes | no | Inspect local service health. |
| `show running-config [network\|mqtt]` | yes | no | Render active configuration with secrets redacted. |
| `show startup-config [network\|mqtt]` | yes | no | Render persisted configuration with secrets redacted. |
| `dns lookup <hostname>` | yes | no | Run a local DNS diagnostic. |
| `led on`, `led off`, `pulse` | yes | no | Run local status LED diagnostics. |
| `configure` | yes | no | Enter configuration mode. |

The `MQTT` column above is the complete command allowlist for that transport.
The MQTT dispatcher must reject every other command as `unsupported` before it
reaches the shared parser. MQTT does not retain a mode, candidate, or command
transaction between messages.

### Configuration Mode

`configure` takes a snapshot of the committed network and MQTT configuration and
changes the USB prompt to `cubley(config)#`. Configuration commands update only
that candidate until `commit` succeeds.

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
| `mqtt client-id <id\|auto>` | Set an explicit or MAC-derived client ID. |
| `mqtt username <value\|clear>` | Set or clear the username. |
| `mqtt password <value\|clear>` | Set or clear the password without echoing it. |
| `mqtt topic-prefix <prefix>` | Set the topic root. |
| `mqtt keepalive <15..3600>` | Set the keepalive interval in seconds. |
| `mqtt reconnect <1..60>` | Set the reconnect interval in seconds. |
| `mqtt defaults` | Stage disabled MQTT defaults. |

#### Candidate Lifecycle

| Command | Behavior |
|---|---|
| `show storage` | Show the network and MQTT configuration backends and load status. |
| `show candidate-config [network\|mqtt]` | Render the candidate with secrets redacted. |
| `show config diff` | Show canonical lines added, removed, or changed relative to the committed configuration. |
| `debug <on\|off>` | Show or suppress successful setter results for the current USB session. |
| `commit` | Validate the complete candidate, persist changed domains, and activate them. |
| `discard` | Replace the candidate with the committed configuration. |
| `load defaults [network\|mqtt\|all]` | Stage defaults without committing them. |
| `exit` | Return to operational mode when clean; `end` and empty-line `Ctrl+D` are equivalent. |

Only one configuration session may own the candidate. `exit` refuses dirty state
to prevent an intentional console action from silently losing work. The prompt is
`cubley(config)#` when clean and `cubley(config*)#` when the candidate differs from
the running configuration. A dirty exit remains in configuration mode and tells
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
! cubley-config v1
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
| `show version` | `version`, `ver`, `show ver` | Show the interface and shell version identifiers. |
| `led on` | none | Drive the status LED high. |
| `led off` | none | Drive the status LED low. |
| `pulse` | none | Pulse the status LED for 100 ms. |
| `configure` | `config`, `conf`, `configure terminal`, `config terminal`, `conf t` | Enter USB configuration mode. |

## LNB Commands

Logical channels are `a` and `b`. Mutations always require a channel. `l` is an
alias for the `lnb` command family.

### Show

| Command | Behavior |
|---|---|
| `show` | Emit system, both LNB channels, and DiSEqC summary lines. |
| `show lnb` | Emit one summary line for each LNB channel. |
| `show lnb <a\|b>` | Emit one selected channel summary. |
| `show lnb [a\|b] detail` | Emit LNBH26 register JSON for both or one selected channel. |
| `show diseqc` | Emit routing preset, tone, carrier settings, and transmit-busy state. |

Each LNB summary includes polarization, band, ISET range, ISW limit, voltage,
tone, low-power mode, external DiSEqC input, and fault registers.

### State Assignments

| Command | Aliases | Accepted values |
|---|---|---|
| `lnb <a\|b> enable <value>` | `enabled`, `e`; root `l` | `on\|off`, `1\|0`, `true\|false` |
| `lnb <a\|b> polarization <value>` | `pol`, `p`; root `l` | `vertical\|horizontal`, `v\|h` |
| `lnb <a\|b> band <value>` | `b`; root `l` | `low\|high`, `l\|h` |
| `lnb <a\|b> iset <value>` | root `l` | `default\|normal\|high\|0` or `low\|reduced\|1` |
| `lnb <a\|b> isw <value>` | root `l` | `4a\|4\|default\|high\|0` or `2.5a\|2p5a\|2_5a\|low\|reduced\|1` |

Enabling either logical channel calls the current global native enable operation.
An LNB command without a value is an error; all reads begin with `show`.

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
| `diseqc goto <position>` | Go to stored position `0..255`. This is not an angle command. |
| `diseqc step <east\|west> <steps>` | Move `1..128` steps. |
| `diseqc drive <east\|west>` | Start continuous movement. |
| `diseqc stop` | Transmit the positioner halt command. |
| `diseqc preset <off\|direct\|aa\|ab\|ba\|bb>` | Select the routing prefix applied to positioner commands. |
| `diseqc preset status` | Show the selected routing preset. |
| `diseqc tx <hex_byte> <hex_byte> [hex_byte ...]` | Transmit 2 to 7 hexadecimal bytes. |
| `diseqc tone on [frequency_hz] [duty_percent]` | Start the carrier; defaults to 22000 Hz and 50%. Frequency range is 1000..100000 Hz and duty range is 1..99%. |
| `diseqc tone off` | Stop the carrier. |
| `diseqc tone status` | Show carrier state and settings. |
| `diseqc listen <on\|off>` | Enable or disable the channel-A LNBH26 external DiSEqC input; boolean aliases are accepted. |

The selected preset prefixes `goto`, `step`, `drive`, and `stop`. Raw `diseqc tx`
frames are transmitted unchanged.

## Canonical Command IDs

Dotted IDs such as `system.version.get` and `diseqc.lnb.set.band` remain available
to internal contracts but are not executable USB or MQTT command forms. External
commands use the operational grammar documented above.

## MQTT Transport

MQTT uses the active LAN8742A IPv4/DHCP/DNS implementation. It starts only after
MQTT is enabled in saved configuration and the interface has a usable IPv4 address.

When enabled, the current binding is:

| Direction | Topic | Payload |
|---|---|---|
| Command to device | `<prefix>/command` | One non-retained command from the MQTT operational allowlist. |
| Result from device | `<prefix>/status` | One published message for each output line. |
| Device availability | `<prefix>/availability` | Retained `online` or last-will `offline`. |

Retained, empty, and greater-than-64-byte command payloads are rejected. The topic
prefix defaults to `diseqc` and is configurable from USB configuration mode with
`mqtt topic-prefix`.

The per-command `cubley/v1/...` topics and JSON request/result envelopes described
by the interface schema files are design contracts and are not implemented by the
current MQTT transport.