# Configuration Reference

## Purpose

Define configuration domains and expected runtime behavior for settings management.

## Current Build Profile Note

- `NF_FEATURE_HAS_CONFIG_BLOCK` is currently disabled in the validated build profile.
- Treat persistence behavior as profile-dependent unless re-enabled and validated.
- Current MVP persists config snapshots to FM24CL16B F-RAM on I2C3 (power-cycle persistent), with RAM snapshot fallback if FRAM is unavailable.

## Implemented MVP Interface

- MQTT commands:
  - `.../command/config/get`
  - `.../command/config/set` with payload `key=value`
  - `.../command/config/save`
  - `.../command/config/reset`
  - `.../command/config/reload`
  - `.../command/config/fram_clear` with payload `ERASE` (guarded destructive operation)
- Serial command interface (same command set):
  - `config get`
  - `config set key=value`
  - `config save`
  - `config reset`
  - `config reload`
  - `config fram-dump [bytes]` (debug: dumps raw FRAM bytes from address `0x0000`)
  - `config fram-clear ERASE` (debug: clears FRAM and resets runtime config to defaults)

## Implemented Runtime Keys (MVP)

- `network.use_dhcp`
- `network.static_ip`
- `network.static_subnet`
- `network.static_gateway`
- `mqtt.broker`
- `mqtt.port`
- `mqtt.client_id`
- `mqtt.username`
- `mqtt.password`
- `mqtt.topic_prefix`
- `mqtt.transport_mode` (`system-net` or `w5500-native`)
- `system.device_name`
- `system.location`

## Persistence Backend (MVP)

- Device: `FM24CL16B` (16 Kb / 2048-byte I2C F-RAM)
- Bus: I2C3
- Format: key-value UTF-8 payload with header (`DCFG` magic, version, length, checksum)
- Save behavior:
  - `config/save` updates RAM snapshot and attempts FRAM persist
  - status `config/persisted` reports `true|false`
- Reload behavior:
  - `config/reload` attempts FRAM load first
  - falls back to RAM snapshot when FRAM read/validation fails
  - status `config/reload_source` reports `fram|ram`

## Configuration Domains

1. **Rotor**
   - logical limits (`east/west`)
   - reference/calibration state
   - movement behavior (timeouts/step defaults)

2. **LNB**
   - voltage/polarization default
   - tone/band default
   - optional current-limit policy

3. **Network/MQTT** (optional when networking enabled)
   - broker host/port/client id
   - topic prefix
   - reconnect behavior

4. **System**
   - device name/location
   - logging/telemetry intervals

## Recommended Data Shape

```json
{
  "system": {
    "device_name": "diseqc-ctrl",
    "location": "default"
  },
  "rotor": {
    "max_angle_east": 80.0,
    "max_angle_west": -80.0,
    "reference_angle": 0.0,
    "calibrated": false
  },
  "lnb": {
    "voltage": 13,
    "tone": false,
    "band": "low"
  },
  "mqtt": {
    "enabled": false,
    "broker": "192.168.1.50",
    "port": 1883,
    "topic_prefix": "diseqc"
  }
}
```

## Validation Rules

- `max_angle_west < max_angle_east`
- Angle values must remain within firmware-supported range
- LNB voltage must be `13` or `18`
- LNB band must be `low` or `high`
- MQTT port must be `1..65535`

Reject invalid updates atomically (all-or-nothing) to avoid partial state drift.

## Lifecycle

1. Load defaults
2. Overlay persisted config (if supported by active profile)
3. Apply runtime overrides (if any)
4. Validate final effective config
5. Start services using effective config

## Runtime Update Strategy

- Apply safe settings live when possible (e.g., telemetry interval)
- Defer disruptive changes to controlled restart points (e.g., full network stack re-init)
- Publish outcome/status for each requested change

## Reset and Recovery

- Provide a “reset to defaults” action
- On config parse/validation failure:
  - log error
  - revert to last-known-good or defaults
  - report degraded state via status channel

## Security/Operational Notes

- Avoid storing plaintext credentials in source control
- Keep device-specific secrets out of examples
- Version config schema if introducing breaking key changes

## Related Documents

- `MQTT_API.md`
- `ARCHITECTURE.md`
- `../guides/TESTING_GUIDE.md`
