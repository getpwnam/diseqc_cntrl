# Configuration Reference

## Current Architecture

CubleyControl has two persisted configuration domains:

- IPv4, DHCP, and DNS use the standard nanoFramework network configuration block.
- The device hostname and MQTT settings use the portable 512-byte Cubley
  application record described in
  [CONFIGURATION_STORAGE.md](CONFIGURATION_STORAGE.md).

The active MQTT backend is STM32 internal flash. FRAM is not initialized or
probed on the current development board. The application record is deliberately
backend-neutral so the same bytes can move to dual FRAM slots later.

## Application Defaults

| Setting | Default |
|---|---|
| Hostname | `cubley-xxxxxx`, automatic from a 24-bit hash of the STM32 unique device ID |
| Enabled | `off` |
| Broker | unset |
| Port | `1883` |
| Client ID | effective hostname when set to `auto` |
| Username/password | unset |
| Topic prefix | `diseqc` |
| Keepalive | 60 seconds |
| Reconnect delay | 5 seconds |

MQTT starts only when enabled, the configuration validates, the network link is
available, and the interface has a non-zero IPv4 address.

## USB Configuration Mode

Network and MQTT configuration is available only through the USB CDC console.
Enter configuration mode with `configure`; edits are staged in RAM until the
complete candidate is committed.

```text
cubley-a1b2c3> configure
cubley-a1b2c3(config)# hostname <name|auto>
cubley-a1b2c3(config)# network mode dhcp|static
cubley-a1b2c3(config)# network address <ipv4>
cubley-a1b2c3(config)# network mask <mask>
cubley-a1b2c3(config)# network gateway <ipv4>
cubley-a1b2c3(config)# network dns auto
cubley-a1b2c3(config)# network dns static <dns1> [dns2]
cubley-a1b2c3(config)# mqtt enabled on|off
cubley-a1b2c3(config)# mqtt broker <host|clear>
cubley-a1b2c3(config)# mqtt port <1..65535>
cubley-a1b2c3(config)# mqtt client-id <id|auto>
cubley-a1b2c3(config)# mqtt username <value|clear>
cubley-a1b2c3(config)# mqtt password <value|clear>
cubley-a1b2c3(config)# mqtt topic-prefix <prefix>
cubley-a1b2c3(config)# mqtt keepalive <15..3600>
cubley-a1b2c3(config)# mqtt reconnect <1..60>
cubley-a1b2c3(config)# show storage
cubley-a1b2c3(config)# show candidate-config
cubley-a1b2c3(config)# show config diff
cubley-a1b2c3(config)# debug on|off
cubley-a1b2c3(config)# commit
cubley-a1b2c3(config)# exit
```

The prompt uses the committed effective hostname and changes to
`<hostname>(config*)#` while the candidate differs from the running
configuration. A staged hostname appears in the prompt only after `commit`;
`commit` or `discard` returns the prompt to `<hostname>(config)#`.
`exit`, `end`, and `Ctrl+D` leave configuration mode only when the candidate is
clean. With uncommitted changes they remain in configuration mode and direct the
operator to commit or discard explicitly.

Most values are case-preserving, printable non-space ASCII tokens. Hostname input
is normalized to lowercase and must be a DNS label of at most 63 characters
containing only `a-z`, `0-9`, and internal hyphens. The broker is required before
an enabled configuration can be committed. The topic prefix cannot start or end
with `/` or contain MQTT wildcards (`#` or `+`).

`show candidate-config` renders the complete candidate except for secret material.
`show running-config` and `show startup-config` are available from either USB mode.
`show storage` reports network and application configuration backend status
separately from live network and MQTT service state. `show network` reports the
active interface plus the network configuration source and load status.
Successful setters are silent by default; `debug on` enables result details for
the current USB session and `debug off` restores quiet output. `show mqtt` reports
service state, connection state, effective hostname, client ID and topic root,
reconnect attempts, and a sanitized last error. MQTT command messages cannot enter
configuration mode or inspect configuration.

## Save And Recovery

MQTT defaults are not written automatically. `commit` validates the complete
network and MQTT candidate before writing changed domains. The MQTT write updates
internal flash and verifies a complete readback. Invalid, blank, or CRC-failed
records cause startup to use disabled defaults.

If a domain write fails, the application attempts to restore the previously
committed values. Successful recovery returns `persist_failed`; failed recovery
returns `persist_partial` and exposes degraded configuration status. Uncommitted
changes are discarded when USB disconnects.

The internal flash update preserves the standard nanoFramework network block but
is not power-fail atomic because the STM32 sector must be erased. A future FRAM
backend uses two generation-selected slots to provide atomic record replacement.

## Credentials

Schema v2 stores MQTT credentials as cleartext in the application record. Password
values are redacted from command debug logs and are never returned by configuration
commands. TLS and encrypted-at-rest credentials are outside the v1 scope.

## Related Documents

- [CONFIGURATION_STORAGE.md](CONFIGURATION_STORAGE.md)
- [MQTT_API.md](MQTT_API.md)
- [INTERFACE_COMMAND_MAP_V1.md](INTERFACE_COMMAND_MAP_V1.md)
