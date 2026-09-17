# Application Configuration Storage

## Purpose

Define the portable Cubley application configuration record and its active
physical backend. Network addressing and DNS remain in the standard
nanoFramework network configuration block.

The application record contains the device hostname, API token, and DiSEqC
positioning calibration.

## Schema 5 Record

The record is exactly 512 bytes. Header integers are little-endian.

| Offset | Size | Field | Description |
|---:|---:|---|---|
| `0x000` | 4 | Magic | ASCII `CCFG` |
| `0x004` | 1 | Schema version | `5` |
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
api_token=<32 to 64 character API token, or empty>
de_lim=<east limit in microdegrees>
dw_lim=<west limit in microdegrees>
de_step=<east step size in microdegrees>
dw_step=<west step size in microdegrees>
d_offset=<signed GoToX offset in microdegrees>
```

Schema 5 is the current application record version. Schema-4 records are
accepted with an empty API token and are rewritten as schema 5 on the next
application-configuration commit. Earlier versions are rejected.

Unknown keys, malformed retained values, invalid field combinations, an
unsupported schema version, and invalid magic, length, or CRC cause the record
to be rejected. The application then uses schema-5 defaults.

## Active Internal Flash Backend

### Capacity Budget

The longest valid application values are a 63-character hostname, a 64-character
API token, `180000000` for each angle limit and step calibration, and
`-180000000` for the signed offset. Together these serialize to 237 payload
bytes, or 253 meaningful bytes including the header. The complete 512-byte slot
remains reserved and written.

The Ethernet HTTPS configuration estimate is:

| Record | Allocated bytes |
|---|---:|
| nanoFramework Ethernet interface | 117 |
| X.509 device-certificate header | 8 |
| Representative P-256 certificate and private-key PEM bundle | 1,067 |
| Cubley application record reservation | 512 |
| **Total** | **1,704 (10.40%)** |
| **Remaining before the Cubley reservation** | **14,680** |

The representative credential uses a maximum-length DNS label in both the
subject common name and subject alternative name, plus an IPv4 alternative
name. Certificate size varies with encoded subject and extensions, so
provisioning must check the actual combined PEM size.

nanoFramework's generic certificate writer uses `__nanoConfig_end__` and is not
aware of the final 512-byte Cubley reservation. The combined size of all
nanoFramework blocks must therefore be limited to 15,872 bytes. With one
117-byte Ethernet record and one 8-byte device-certificate header, the device
certificate payload must not exceed 15,747 bytes. CA bundles, wireless records,
or additional certificates reduce that limit. This Ethernet target does not
use wireless station, wireless AP, or CA-root records in its HTTPS-server
profile.

The STM32 configuration sector spans `0x0800C000` through `0x0800FFFF`. The
final 512 bytes, `0x0800FE00` through `0x0800FFFF`, are reserved for the
application record. Standard nanoFramework configuration data must remain below
`0x0800FE00`.

An update copies the complete 16 KiB sector to RAM, replaces the application
record, erases the sector, writes the complete image, and verifies the record.
This preserves the nanoFramework network configuration block.

Sector erase makes this backend non-atomic across power loss. CRC validation
detects an incomplete record. Writes occur after an explicit USB CDC
configuration commit or an operational `diseqc angle-limits` update.

## Future Backend

The record format is backend-neutral. A future FRAM implementation may store the
same record in generation-selected slots, but FRAM is not initialized or probed
on the current board.