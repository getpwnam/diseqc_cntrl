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

- `diseqc/command/config/get`
  - payload: ignored
  - action: publish effective runtime config to `diseqc/status/config/effective/...`

- `diseqc/command/config/set`
  - payload: `key=value`
  - example: `mqtt.broker=192.168.1.60`
  - key set (MVP):
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

- `diseqc/command/config/save`
  - payload: ignored
  - action: snapshot current runtime config as last-saved config (in-memory)

- `diseqc/command/config/reset`
  - payload: ignored
  - action: reset runtime config to factory defaults

- `diseqc/command/config/reload`
  - payload: ignored
  - action: restore runtime config from last-saved snapshot (in-memory)

- `diseqc/command/config/fram_clear`
  - payload: `ERASE`
  - action: clears FRAM and resets runtime config defaults
  - safety: ignored unless payload exactly matches `ERASE`

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

### Runtime Configuration

- `diseqc/status/config/saved` (`true` when save command succeeds)
- `diseqc/status/config/reset` (`true` when reset command succeeds)
- `diseqc/status/config/reloaded` (`true` when reload command succeeds)
- `diseqc/status/config/updated` (last updated key)
- `diseqc/status/config/persisted` (`true|false`, FRAM persist result for `config/save`)
- `diseqc/status/config/reload_source` (`fram|ram`)
- `diseqc/status/config/fram_cleared` (`true` when FRAM clear succeeds)
- `diseqc/status/config/effective/...` (effective runtime config snapshot)

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
