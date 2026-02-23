# MQTT API Reference

## Purpose

Define topic names and payload contracts for command/status integration.

## Current Build Profile Note

Networking is disabled in the currently validated firmware profile. This API remains the canonical contract for when networking is enabled.

## Topic Namespace

Root prefix: `diseqc/`

- Commands to device: `diseqc/command/...`
- Status from device: `diseqc/status/...`
- Availability (LWT): `diseqc/availability`

## Commands

### Rotor Positioning

- `diseqc/command/goto/angle`
  - payload: float string in degrees (range typically `-80..80`)
  - example: `45.5`

- `diseqc/command/goto/satellite`
  - payload: satellite identifier string
  - example: `astra_19.2e`

- `diseqc/command/halt`
  - payload: ignored (empty recommended)

### Manual Rotor Control

- `diseqc/command/manual/step_east`
- `diseqc/command/manual/step_west`
  - payload: integer step count (`1..128`)

- `diseqc/command/manual/drive_east`
- `diseqc/command/manual/drive_west`
  - payload: ignored

### LNB Control

- `diseqc/command/lnb/voltage`
  - payload: `13` or `18`

- `diseqc/command/lnb/polarization`
  - payload: `vertical|horizontal` (`v|h` accepted aliases)

- `diseqc/command/lnb/tone`
  - payload: `on|off` (`true|false|1|0` aliases)

- `diseqc/command/lnb/band`
  - payload: `low|high`

### Configuration/Calibration

- `diseqc/command/config/save`
- `diseqc/command/config/reset`
- `diseqc/command/config/reload`
- `diseqc/command/calibrate/reference`

Payload for these commands is ignored unless otherwise documented by implementation.

## Status Topics

### Device/Position

- `diseqc/status/state` (`idle|moving|error|...`)
- `diseqc/status/busy` (`true|false`)
- `diseqc/status/position/angle` (float string)
- `diseqc/status/position/satellite` (identifier or `unknown`)

### LNB

- `diseqc/status/lnb/voltage` (`13|18`)
- `diseqc/status/lnb/polarization` (`vertical|horizontal`)
- `diseqc/status/lnb/tone` (`on|off`)
- `diseqc/status/lnb/band` (`low|high`)

### Diagnostics

- `diseqc/status/error` (last error or empty)

### Availability

- `diseqc/availability`: `online|offline`
  - published retained; `offline` is typically the MQTT LWT payload

## QoS / Retain Guidelines

- Commands: QoS `1`, retained `false`
- State-like status (`availability`, current angle/lnb settings): retained `true`
- Telemetry/heartbeat-like status: retained `false`

## Example CLI Usage

```bash
# Halt
mosquitto_pub -t diseqc/command/halt -m ''

# Move to angle
mosquitto_pub -t diseqc/command/goto/angle -m '19.2'

# Set LNB horizontal + high band
mosquitto_pub -t diseqc/command/lnb/polarization -m 'horizontal'
mosquitto_pub -t diseqc/command/lnb/band -m 'high'

# Observe status
mosquitto_sub -t 'diseqc/status/#' -v
```

## Compatibility Notes

- Consumers should tolerate unknown topics for forward compatibility.
- Publishers should keep payloads simple (plain text scalars) unless explicitly specified otherwise.
