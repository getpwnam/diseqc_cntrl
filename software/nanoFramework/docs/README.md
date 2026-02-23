# nanoFramework Documentation Index

This folder is the canonical documentation root for the `software/nanoFramework` domain.

## Documentation Conventions

- Keep build/run guidance aligned with `../toolchain/build.sh` and `../QUICK_START.md`.
- Mark speculative or future features explicitly as optional.
- Keep hardware manufacturing details out of this tree (place them under `../../hardware/`).
- Prefer concise, task-oriented sections: Purpose, Prerequisites, Steps, Validation, Troubleshooting.

## Core Workflow

- Build + flash quick path: `../QUICK_START.md`
- Full Docker build flow: `guides/DOCKER_BUILD_GUIDE.md`
- Functional/system testing: `guides/TESTING_GUIDE.md`

## User Guides

- `guides/MANUAL_MOTOR_CONTROL.md`
- `guides/LNB_CONTROL_GUIDE.md`

## Reference

- `reference/ARCHITECTURE.md`
- `reference/MQTT_API.md`
- `reference/CONFIGURATION.md`

## Hardware-Oriented Notes (software integration)

- `hardware/W5500_ETHERNET.md`
- `hardware/LNB_I2C_TESTING.md`
- `hardware/MOTOR_ENABLE_NOTES.md`

## Scope Boundary

- This documentation covers the `software/nanoFramework` domain only.
- Board design/manufacturing docs belong in `../../hardware/`.
- Upstream `nf-interpreter` content is external and fetched during build; this repo documents only the integration/build profile used here.
