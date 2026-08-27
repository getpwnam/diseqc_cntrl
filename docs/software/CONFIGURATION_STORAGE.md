# Application Configuration Storage

## Purpose

Define the portable Cubley application configuration record and its physical
storage backends. The record format is independent of internal flash or FRAM.

Network interface addressing remains in the standard nanoFramework network
configuration block. The application record stores the device hostname and MQTT
settings.

## Portable Record

The record is exactly 512 bytes. Integer fields are little-endian.

| Offset | Size | Field | Description |
|---:|---:|---|---|
| `0x000` | 4 | Magic | ASCII `CCFG` |
| `0x004` | 1 | Schema version | Currently `2` |
| `0x005` | 1 | Flags | Reserved, currently `0` |
| `0x006` | 2 | Payload length | Used bytes from the payload area |
| `0x008` | 4 | Generation | Monotonic save generation |
| `0x00C` | 4 | CRC32 | CRC32 of used payload bytes |
| `0x010` | 496 | Payload | UTF-8 `key=value` lines followed by erased padding |

The CRC polynomial is the reflected `0xEDB88320` form with initial value
`0xFFFFFFFF` and final inversion.

Schema v2 keys are `hostname`, `enabled`, `broker`, `port`, `client_id`,
`username`, `password`, `topic_prefix`, `keepalive_seconds`, and
`reconnect_seconds`. Schema v1 records are not migrated; they are rejected and
the application starts with disabled defaults.

## Active Internal Flash Backend

The STM32 configuration sector spans `0x0800C000` through `0x0800FFFF`.
The final 512 bytes, `0x0800FE00` through `0x0800FFFF`, are reserved for the
portable application record. Standard nanoFramework configuration data must
remain below `0x0800FE00`.

An update copies the complete 16 KB sector to RAM, replaces the application
record, erases the sector, writes the complete image, and verifies the record.
This preserves the standard nanoFramework network configuration block.

STM32 sector erase means this backend is not power-fail atomic. CRC validation
detects an incomplete write and causes managed code to use disabled defaults.
Writes occur only after an explicit save command to limit flash wear.

## Future FRAM Backend

When FRAM hardware is available, the same 512-byte record can be stored in two
generation-selected slots at `0x0400` and `0x0600`. The newer valid record wins.
No schema conversion or MQTT service changes are required when selecting that
backend.

The current development board has no working FRAM access. Production firmware
must not initialize or probe FRAM while the internal flash backend is selected.

## Credential Handling

The v2 password is stored as cleartext inside the record. Commands and debug
logs must redact it, and configuration output may expose only whether a password
is configured. TLS and encrypted-at-rest credentials are outside the v1 scope.