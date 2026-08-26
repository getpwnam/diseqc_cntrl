# CubleyControl Command Map

Status: implemented command set as of 2026-08-25

## Purpose

Document the command grammar implemented by `software/nanoFramework/CubleyControl`.
USB CDC and MQTT pass command text to the same parser. Commands are case-insensitive,
leading and trailing whitespace is ignored, and repeated spaces are collapsed.

The canonical IDs declared in `Contracts/CommandIds.cs` are not all executable. Only
the canonical forms listed below are currently handled by the parser.

## USB CDC Transport

Enter commands directly at the USB CDC prompt. There is no `cubley v1` CLI prefix.

Normal command results are framed as:

```text
OK
kv <key=value fields>
```

Failures are framed as:

```text
Fail: <reason>
```

Some `show` commands emit their data directly instead of using the normal result
framing. Detailed command diagnostics are written to the firmware debug log.

## Operator Commands

| Command | Aliases | Behavior |
|---|---|---|
| `help` | `h` | List command groups. |
| `help <lnb\|show\|get\|set\|diseqc\|mqtt>` | `help l` for LNB help | Show brief command-specific usage. |
| `status` | `st` | Show USB CDC and status LED health. |
| `watch [on\|off]` | `w`, values `1\|0`; omitted value means `on` | Enable or disable the periodic serial status line. |
| `capabilities` | `caps` | Show the current capability summary. |
| `version` | `ver` | Show the interface and shell version identifiers. |
| `led on` | none | Drive the status LED high. |
| `led off` | none | Drive the status LED low. |
| `pulse` | none | Pulse the status LED for 100 ms. |

## LNB Commands

Logical channels are `a` and `b`. The short `lnb` command family operates on
channel A only.

### Show

| Command | Behavior |
|---|---|
| `show` | Emit system, channel-A LNB, and placeholder DiSEqC summary lines. |
| `show lnb [a\|b]` | Emit a one-line LNB summary; channel A is the default. |
| `show lnb [a\|b] detail` | Emit an LNBH26 register snapshot as JSON. |
| `show diseqc` | Emit the current placeholder DiSEqC summary. |

### Channel-A Short Forms

| Command | Aliases | Accepted values |
|---|---|---|
| `lnb get polarization` | `lnb get pol`, `lnb get p`, `l g p` | none |
| `lnb get band` | `lnb get b`, `l g b` | none |
| `lnb get status` | `lnb get s`, `l g s` | none |
| `lnb set enable <value>` | `lnb set e`, `l s e` | `on\|off`, `1\|0`, `true\|false` |
| `lnb set polarization <value>` | `lnb set pol`, `lnb set p`, `l s p` | `vertical\|horizontal`, `v\|h` |
| `lnb set band <value>` | `lnb set b`, `l s b` | `low\|high`, `l\|h` |

### Channel-Aware Get and Set

| Command | Accepted values or result |
|---|---|
| `get lnb.<a\|b>.band` | Returns `low` or `high`. |
| `get lnb.<a\|b>.polarization` | `polarization` may be shortened to `pol`; returns `vertical` or `horizontal`. |
| `get lnb.<a\|b>.status` | Returns both LNBH26 status registers. |
| `get lnb.<a\|b>.enabled` | Recognized but currently returns `unsupported` because there is no native getter. |
| `get lnb.<a\|b>.iset` | Returns the current ISET range. |
| `get lnb.<a\|b>.isw` | Returns the current ISW limit. |
| `set lnb.<a\|b>.band <value>` | `low\|high`, `l\|h` |
| `set lnb.<a\|b>.polarization <value>` | `polarization` may be shortened to `pol`; `vertical\|horizontal`, `v\|h` |
| `set lnb.<a\|b>.enabled <value>` | `enabled` may be shortened to `enable`; `on\|off`, `1\|0`, `true\|false` |
| `set lnb.<a\|b>.iset <value>` | Default range: `default\|normal\|high\|0`; reduced range: `low\|reduced\|1`. |
| `set lnb.<a\|b>.isw <value>` | 4 A: `4a\|4\|default\|high\|0`; 2.5 A: `2.5a\|2p5a\|2_5a\|low\|reduced\|1`. |

Enabling either logical channel calls the current global native enable operation.

## Network And MQTT Configuration

Network addressing is persisted by nanoFramework. MQTT edits are staged and are
written to the portable application configuration record only by `save` or `apply`.

| Command | Behavior |
|---|---|
| `get network` | Show configured IPv4 and DNS values. |
| `show network` | Show active link, MAC, IPv4, DNS, and persistence state. |
| `get mqtt` | Show staged MQTT settings; credentials are represented only by set flags. |
| `show mqtt` | Show active MQTT state, endpoint, reconnect attempts, and last error. |
| `set mqtt enabled <on\|off>` | Stage transport enablement. |
| `set mqtt broker <host\|clear>` | Stage the broker IPv4 address or DNS hostname. |
| `set mqtt port <port>` | Stage port `1..65535`. |
| `set mqtt client-id <id\|auto>` | Stage an explicit ID or MAC-derived automatic ID. |
| `set mqtt username <value\|clear>` | Stage a username. |
| `set mqtt password <value\|clear>` | Stage a redacted password. |
| `set mqtt topic-prefix <prefix>` | Stage the topic root without wildcards or edge slashes. |
| `set mqtt keepalive <seconds>` | Stage `15..3600`. |
| `set mqtt reconnect <seconds>` | Stage `1..60`. |
| `set mqtt save` | Validate, persist, and activate staged settings. |
| `set mqtt apply` | Alias of `set mqtt save`. |
| `set mqtt discard` | Restore staged values from active settings. |
| `set mqtt defaults` | Stage disabled MQTT defaults. |

## DiSEqC Commands

| Command | Accepted values and behavior |
|---|---|
| `diseqc goto <position>` | Go to stored position `0..255`. This is not an angle command. |
| `diseqc step <east\|west> <steps>` | Move `1..128` steps. |
| `diseqc drive <east\|west>` | Start continuous movement. |
| `diseqc stop` | Transmit the positioner halt command. |
| `diseqc preset <off\|direct\|aa\|ab\|ba\|bb>` | Select the routing prefix applied to positioner commands. |
| `diseqc preset status` | Show the selected routing preset. |
| `diseqc tx <hex_byte> <hex_byte> [hex_byte ...]` | Transmit 2 to 7 hexadecimal bytes. The current overlength error says `max_bytes=6`, but the parser accepts seven. |
| `diseqc tone on [frequency_hz] [duty_percent]` | Start the carrier; defaults to 22000 Hz and 50%. Frequency range is 1000..100000 Hz and duty range is 1..99%. |
| `diseqc tone off` | Stop the carrier. |
| `diseqc tone status` | Show carrier state and settings. |
| `diseqc listen <on\|off>` | Enable or disable the channel-A LNBH26 external DiSEqC input; boolean aliases are accepted. |

The selected preset prefixes `goto`, `step`, `drive`, and `stop`. Raw `diseqc tx`
frames are transmitted unchanged.

## Executable Canonical Commands

These dotted command forms are accepted directly:

| Canonical command | Argument |
|---|---|
| `system.version.get` | none |
| `system.capabilities.get` | none |
| `diseqc.lnb.get.status` | none; channel A |
| `diseqc.lnb.get.pol` | none; channel A |
| `diseqc.lnb.get.band` | none; channel A |
| `diseqc.lnb.set.enable <value>` | Boolean value; channel A |
| `diseqc.lnb.set.pol <value>` | Polarization value; channel A |
| `diseqc.lnb.set.band <value>` | Band value; channel A |

Other IDs declared in `Contracts/CommandIds.cs`, including `system.config.*`, rotor
angle/satellite commands, calibration, LNB voltage, and LNB tone IDs, are not
currently executable.

## MQTT Transport

MQTT uses the active LAN8742A IPv4/DHCP/DNS implementation. It starts only after
MQTT is enabled in saved configuration and the interface has a usable IPv4 address.

When enabled, the current binding is:

| Direction | Topic | Payload |
|---|---|---|
| Command to device | `<prefix>/command` | One non-retained command line using the same grammar as USB CDC. |
| Result from device | `<prefix>/status` | One published message for each output line. |
| Device availability | `<prefix>/availability` | Retained `online` or last-will `offline`. |

Retained, empty, and greater-than-64-byte command payloads are rejected. The topic
prefix defaults to `diseqc` and is configurable with `set mqtt topic-prefix`.

The per-command `cubley/v1/...` topics and JSON request/result envelopes described
by the interface schema files are design contracts and are not implemented by the
current MQTT transport.