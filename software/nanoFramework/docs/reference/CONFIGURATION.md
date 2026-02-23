# Configuration Reference

## Purpose

Define configuration domains and expected runtime behavior for settings management.

## Current Build Profile Note

- `NF_FEATURE_HAS_CONFIG_BLOCK` is currently disabled in the validated build profile.
- Treat persistence behavior as profile-dependent unless re-enabled and validated.

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
