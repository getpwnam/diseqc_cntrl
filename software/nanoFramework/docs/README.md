# nanoFramework Documentation Index

This folder is the canonical documentation root for the `software/nanoFramework` domain.

## Documentation Conventions

- Keep build/run guidance aligned with `../toolchain/build-native.sh` and `../../../docs/debug/TESTING_GUIDE.md`.
- Mark speculative or future features explicitly as optional.
- Keep hardware manufacturing details out of this tree (place them under `../../hardware/`).
- Prefer concise, task-oriented sections: Purpose, Prerequisites, Steps, Validation, Troubleshooting.

Top-level docs taxonomy used by this repo:

- `docs/software`: architecture, configuration, and API contracts
- `docs/debug`: bring-up procedures, validation workflows, and incident logs
- `docs/hardware`: board-specific hardware references and CAD notes

## Core Workflow

- Build + flash quick path: `../../../docs/debug/MANAGED_DEPLOYMENT.md`
- Functional/system testing: `../../../docs/debug/TESTING_GUIDE.md`

## User Guides

- `../../../docs/software/MANUAL_MOTOR_CONTROL.md`
- `../../../docs/software/LNB_CONTROL_GUIDE.md`

## Reference

- `../../../docs/software/ARCHITECTURE.md`
- `../../../docs/software/MQTT_API.md`
- `../../../docs/software/CONFIGURATION.md`

## Debug and Bring-up

- `../../../docs/debug/DIAGNOSTICS_MAILBOX.md`
- `../../../docs/debug/BRINGUP_TEST_LOG.md`
- `../../../docs/debug/LNB_I2C_TESTING.md`
- `../../../docs/debug/W5500_LINK_BRINGUP_CHECKLIST.md`
- `../../../docs/debug/POWER_SUPPLY_OSCILLOSCOPE_TESTS.md`

## Hardware Context

- `../../../docs/hardware/KICAD-MISSING-SYMBOLS.md`

## Scope Boundary

- This documentation covers the `software/nanoFramework` domain only.
- Board design/manufacturing docs belong in `../../hardware/`.
- Upstream `nf-interpreter` content is external and fetched during build; this repo documents only the integration/build profile used here.
