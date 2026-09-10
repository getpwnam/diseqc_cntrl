# Configuration Reference

## Configuration Domains

CubleyControl has two persisted configuration domains:

- **Network**: IPv4 assignment and DNS in the standard nanoFramework network
  configuration block.
- **Application**: hostname and DiSEqC positioning calibration in the portable
  512-byte schema-4 application record described in
  [CONFIGURATION_STORAGE.md](CONFIGURATION_STORAGE.md).

Configuration is changed only through the USB CDC console. The REST API does
not expose configuration operations.

## Defaults

### Network

| Setting | Default |
|---|---|
| Address mode | DHCP |
| Static address, mask, gateway | `0.0.0.0` |
| DNS mode | Automatic |
| Static DNS servers | `0.0.0.0` |

### Application Schema 4

| Setting | Default | Valid values |
|---|---|---|
| Hostname | Automatic | Empty/`auto`, or a lowercase DNS label up to 63 characters using `a-z`, `0-9`, and internal hyphens |
| East angle limit | Disabled | `0`, or greater than 0 through 180 degrees |
| West angle limit | Disabled | `0`, or greater than 0 through 180 degrees |
| East step calibration | Disabled | `0`, or greater than 0 through 180 degrees per step |
| West step calibration | Disabled | `0`, or greater than 0 through 180 degrees per step |
| Signed GoToX offset | `0` | West 180 degrees through east 180 degrees |

East/west limits and east/west step calibration are each configured as a pair:
both values must be zero or both must be positive. Values are stored as integer
microdegrees. An automatic hostname resolves to `cubley-xxxxxx` from the STM32
unique device ID, or `cubley` if the ID is unavailable.

## USB CDC Configuration Mode

Enter configuration mode with `configure`, `config`, or `conf`; an optional
`terminal`/`t` token is accepted. Changes are staged in RAM until `commit`.

```text
cubley-a1b2c3> configure
cubley-a1b2c3(config)# hostname <name|auto>
cubley-a1b2c3(config)# network mode <dhcp|static>
cubley-a1b2c3(config)# network address <ipv4>
cubley-a1b2c3(config)# network mask <mask>
cubley-a1b2c3(config)# network gateway <ipv4>
cubley-a1b2c3(config)# network dns auto
cubley-a1b2c3(config)# network dns static <dns1> [dns2]
cubley-a1b2c3(config)# diseqc angle-limits <east> <west>|off
cubley-a1b2c3(config)# diseqc step-calibration <east> <west>|off
cubley-a1b2c3(config)# diseqc fixed-offset <east|west> <degrees>|off
cubley-a1b2c3(config)# show candidate-config [network|application|diseqc|all]
cubley-a1b2c3(config)# show running-config [network|application|diseqc|all]
cubley-a1b2c3(config)# show startup-config [network|application|diseqc|all]
cubley-a1b2c3(config)# show config diff
cubley-a1b2c3(config)# show storage
cubley-a1b2c3(config)# debug <on|off>
cubley-a1b2c3(config)# commit
cubley-a1b2c3(config)# discard
cubley-a1b2c3(config)# exit
```

`network defaults`, `diseqc defaults`, `load defaults [domain]`, and
`defaults [domain]` stage defaults. Supported domains are `network`,
`application`, `diseqc`, and `all`.

The prompt becomes `<hostname>(config*)#` while the candidate differs from the
running configuration. It continues to use the committed hostname until commit
succeeds. `exit`, `end`, and empty-line `Ctrl+D` refuse to leave configuration
mode while changes are pending. Use `commit` or `discard` explicitly.

`show running-config` omits default-valued lines. Startup and candidate views
render complete selected domains under a `! cubley-config v4 <source>` header.
`show storage` reports network and application backend/load status. `debug on`
shows successful setter details for the current USB session; failures are always
shown.

## Runtime Overrides

Operational `diseqc angle-limits <east> <west>` and `diseqc angle-limits off`
write the application record immediately and survive reboot. The runtime limits
change only after the write is verified. Operational `diseqc step-calibration`
commands remain RAM-only, and the fixed GoToX offset is changed only in
configuration mode.

The signed offset is applied before direction-specific limit checking and GoToX
encoding. For example, `diseqc fixed-offset west 3.38` changes a requested
36.6 degrees east to an effective 33.22 degrees east before protocol rounding.

## Commit And Recovery

`commit` validates the complete network and application candidates before
writing changed domains. The application write updates internal flash and
verifies readback. Network changes are applied through nanoFramework's network
configuration API.

If a write fails, the device attempts to restore both previous snapshots. A
successful rollback reports `persist_failed`; failed rollback reports
`persist_partial`. The candidate remains available for inspection. USB
disconnect or console timeout discards uncommitted changes.

The internal-flash application update is not power-fail atomic because the STM32
sector must be erased. CRC validation rejects incomplete or corrupt records and
the application starts with defaults.

## Related Documents

- [CONFIGURATION_STORAGE.md](CONFIGURATION_STORAGE.md)
- [INTERFACE_COMMAND_MAP_V1.md](INTERFACE_COMMAND_MAP_V1.md)
- [DEVICE_API_V2.md](DEVICE_API_V2.md)
