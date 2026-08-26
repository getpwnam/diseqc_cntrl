# Configuration Reference

## Current Architecture

CubleyControl has two persisted configuration domains:

- IPv4, DHCP, and DNS use the standard nanoFramework network configuration block.
- MQTT uses the portable 512-byte Cubley application record described in
  [CONFIGURATION_STORAGE.md](CONFIGURATION_STORAGE.md).

The active MQTT backend is STM32 internal flash. FRAM is not initialized or
probed on the current development board. The application record is deliberately
backend-neutral so the same bytes can move to dual FRAM slots later.

## MQTT Defaults

| Setting | Default |
|---|---|
| Enabled | `off` |
| Broker | unset |
| Port | `1883` |
| Client ID | automatic from the final three MAC bytes |
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
cubley> configure
cubley(config)# network mode dhcp|static
cubley(config)# network address <ipv4>
cubley(config)# network mask <mask>
cubley(config)# network gateway <ipv4>
cubley(config)# network dns auto
cubley(config)# network dns static <dns1> [dns2]
cubley(config)# mqtt enabled on|off
cubley(config)# mqtt broker <host|clear>
cubley(config)# mqtt port <1..65535>
cubley(config)# mqtt client-id <id|auto>
cubley(config)# mqtt username <value|clear>
cubley(config)# mqtt password <value|clear>
cubley(config)# mqtt topic-prefix <prefix>
cubley(config)# mqtt keepalive <15..3600>
cubley(config)# mqtt reconnect <1..60>
cubley(config)# show storage
cubley(config)# show candidate-config
cubley(config)# show config diff
cubley(config)# debug on|off
cubley(config)# commit
cubley(config)# exit
```

The prompt changes to `cubley(config*)#` while the candidate differs from the
running configuration. `commit` or `discard` returns it to `cubley(config)#`.
`exit`, `end`, and `Ctrl+D` leave configuration mode only when the candidate is
clean. With uncommitted changes they remain in configuration mode and direct the
operator to commit or discard explicitly.

Values are case-preserving, printable non-space ASCII tokens. The broker is
required before an enabled configuration can be committed. The topic prefix cannot
start or end with `/` or contain MQTT wildcards (`#` or `+`).

`show candidate-config` renders the complete candidate except for secret material.
`show running-config` and `show startup-config` are available from either USB mode.
`show storage` reports configuration backend and load status separately from live
network and MQTT service state. Successful setters are silent by default;
`debug on` enables result details for the current USB session and `debug off`
restores quiet output.
`show mqtt` reports service state, connection state, reconnect attempts, and a
sanitized last error. MQTT command messages cannot enter configuration mode or
inspect configuration.

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

Schema v1 stores MQTT credentials as cleartext in the application record. Password
values are redacted from command debug logs and are never returned by configuration
commands. TLS and encrypted-at-rest credentials are outside the v1 scope.

## Related Documents

- [CONFIGURATION_STORAGE.md](CONFIGURATION_STORAGE.md)
- [MQTT_API.md](MQTT_API.md)
- [INTERFACE_COMMAND_MAP_V1.md](INTERFACE_COMMAND_MAP_V1.md)