# Application Configuration Storage

## Purpose

Define the portable Cubley application configuration record and its active
physical backend. Network addressing and DNS remain in the standard
nanoFramework network configuration block.

The application record contains only the device hostname and DiSEqC positioning
calibration.

## Schema 4 Record

The record is exactly 512 bytes. Header integers are little-endian.

| Offset | Size | Field | Description |
|---:|---:|---|---|
| `0x000` | 4 | Magic | ASCII `CCFG` |
| `0x004` | 1 | Schema version | `4` |
| `0x005` | 1 | Flags | Reserved; `0` |
| `0x006` | 2 | Payload length | Used bytes in the payload area |
| `0x008` | 4 | Generation | Monotonic save generation |
| `0x00C` | 4 | CRC32 | CRC32 of used payload bytes |
| `0x010` | 496 | Payload | ASCII `key=value` lines followed by erased padding |

The CRC uses reflected polynomial `0xEDB88320`, initial value `0xFFFFFFFF`, and
final inversion.

The payload contains exactly these keys:

```text
hostname=<configured hostname or empty for automatic>
de_lim=<east limit in microdegrees>
dw_lim=<west limit in microdegrees>
de_step=<east step size in microdegrees>
dw_step=<west step size in microdegrees>
d_offset=<signed GoToX offset in microdegrees>
```

Unknown keys, malformed values, invalid field combinations, a different schema
version, and invalid magic, length, or CRC cause the record to be rejected. The
application then uses schema-4 defaults.

## Active Internal Flash Backend

The STM32 configuration sector spans `0x0800C000` through `0x0800FFFF`. The
final 512 bytes, `0x0800FE00` through `0x0800FFFF`, are reserved for the
application record. Standard nanoFramework configuration data must remain below
`0x0800FE00`.

An update copies the complete 16 KiB sector to RAM, replaces the application
record, erases the sector, writes the complete image, and verifies the record.
This preserves the nanoFramework network configuration block.

Sector erase makes this backend non-atomic across power loss. CRC validation
detects an incomplete record. Writes occur only after an explicit USB CDC
configuration commit.

## Future Backend

The record format is backend-neutral. A future FRAM implementation may store the
same record in generation-selected slots, but FRAM is not initialized or probed
on the current board.