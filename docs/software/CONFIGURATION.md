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

## MQTT Commands

Configuration edits are staged in RAM. `save` and `apply` are equivalent: both
persist the complete application record and publish the new configuration to the
MQTT service. The service disconnects and reconnects when a saved revision changes.

```text
get mqtt
show mqtt
set mqtt enabled on|off
set mqtt broker <host|clear>
set mqtt port <1..65535>
set mqtt client-id <id|auto>
set mqtt username <value|clear>
set mqtt password <value|clear>
set mqtt topic-prefix <prefix>
set mqtt keepalive <15..3600>
set mqtt reconnect <1..60>
set mqtt save
set mqtt apply
set mqtt discard
set mqtt defaults
```

Values are case-preserving, printable non-space ASCII tokens. The broker is
required before an enabled configuration can be saved. The topic prefix cannot
start or end with `/` or contain MQTT wildcards (`#` or `+`).

`get mqtt` reports staged values and exposes only `username_set` and
`password_set`. `show mqtt` reports service state, connection state, reconnect
attempts, and a sanitized last error.

## Save And Recovery

MQTT defaults are not written automatically. An explicit `set mqtt save` or
`set mqtt apply` updates internal flash and verifies a complete readback. Invalid,
blank, or CRC-failed records cause startup to use disabled defaults.

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