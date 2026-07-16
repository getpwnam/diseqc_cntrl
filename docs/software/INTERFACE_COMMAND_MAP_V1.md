# Unified Command Map v1

Status: draft for implementation

## Purpose

Define one canonical command contract and bind it to both transports.

1. Canonical command ID is transport-neutral.
2. CLI syntax is operator-friendly.
3. MQTT topic path is machine-friendly.

## Canonical Transport Roots

1. MQTT base root: cubley/v1
2. CLI mode root: cubley v1

## Mapping Rules

1. Each command ID has exactly one CLI command form.
2. Each command ID has exactly one MQTT topic.
3. Request payload must validate against request-envelope.schema.json.
4. Response payload must validate against result-envelope.schema.json.

## Command Matrix

| Canonical Command ID | CLI Command | MQTT Topic | MQTT Payload Notes |
|---|---|---|---|
| diseqc.rotor.goto_angle | rotor goto-angle <angle_deg> | cubley/v1/diseqc/command/rotor/goto_angle | JSON request envelope with args.angle_deg |
| diseqc.rotor.goto_satellite | rotor goto-satellite <satellite> | cubley/v1/diseqc/command/rotor/goto_satellite | JSON request envelope with args.satellite |
| diseqc.rotor.halt | rotor halt | cubley/v1/diseqc/command/rotor/halt | JSON request envelope with empty args |
| diseqc.rotor.step_east | rotor step-east <steps> | cubley/v1/diseqc/command/rotor/step_east | JSON request envelope with args.steps |
| diseqc.rotor.step_west | rotor step-west <steps> | cubley/v1/diseqc/command/rotor/step_west | JSON request envelope with args.steps |
| diseqc.rotor.drive_east | rotor drive-east | cubley/v1/diseqc/command/rotor/drive_east | JSON request envelope with empty args |
| diseqc.rotor.drive_west | rotor drive-west | cubley/v1/diseqc/command/rotor/drive_west | JSON request envelope with empty args |
| diseqc.lnb.set_voltage | lnb set-voltage <13\|18> | cubley/v1/diseqc/command/lnb/set_voltage | JSON request envelope with args.voltage |
| diseqc.lnb.set_polarization | lnb set-polarization <vertical\|horizontal> | cubley/v1/diseqc/command/lnb/set_polarization | JSON request envelope with args.polarization |
| diseqc.lnb.set_tone | lnb set-tone <on\|off> | cubley/v1/diseqc/command/lnb/set_tone | JSON request envelope with args.tone |
| diseqc.lnb.set_band | lnb set-band <low\|high> | cubley/v1/diseqc/command/lnb/set_band | JSON request envelope with args.band |
| diseqc.calibration.set_reference | calibration set-reference | cubley/v1/diseqc/command/calibration/set_reference | JSON request envelope with empty args |
| system.config.get | system config get | cubley/v1/system/command/config/get | JSON request envelope with empty args |
| system.config.set | system config set <key>=<value> | cubley/v1/system/command/config/set | JSON request envelope with args.key and args.value |
| system.config.save | system config save | cubley/v1/system/command/config/save | JSON request envelope with empty args |
| system.config.reset | system config reset | cubley/v1/system/command/config/reset | JSON request envelope with empty args |
| system.config.reload | system config reload | cubley/v1/system/command/config/reload | JSON request envelope with empty args |
| system.config.fram_clear | system config fram-clear ERASE | cubley/v1/system/command/config/fram_clear | JSON request envelope with args.confirm=ERASE |
| system.capabilities.get | system capabilities get | cubley/v1/system/command/capabilities/get | JSON request envelope with empty args |
| system.version.get | system version get | cubley/v1/system/command/version/get | JSON request envelope with empty args |

## Common Result Topics

1. Diseqc command results: cubley/v1/diseqc/result
2. System command results: cubley/v1/system/result
3. State snapshot stream: cubley/v1/system/state/snapshot
4. Capabilities snapshot stream: cubley/v1/system/meta/capabilities

Each result payload uses result-envelope.schema.json.