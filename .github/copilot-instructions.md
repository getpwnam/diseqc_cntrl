# Copilot Instructions (Workspace Axioms)

These are hard rules for this repository. Apply them unless a user explicitly overrides.

## Development Plan
- There are no public users of this software, only the code you see in this repo. Compatibility shims are not required.
- The priority is functional, stable and secure firmware. Everything else is secondary.


## Interop Management
- The interop surface should change as little as possible but may change if it improves the firmware's functional stability or security.
- When the interop surface changes, the change must be clearly documented and time stamped in the commit message. The commit message must also include a brief rationale for the change.
- The interop guard script must validate any changes.


## Firmware Source of Truth

- Edit firmware target files in `firmware/targets-local/CUBLEY_F407_0_5/`.
- `firmware/nf-interpreter/targets-community/ChibiOS/CUBLEY_F407_0_5/` is populated during build flow and is not the canonical edit location.
- When debugging runtime behavior, verify the built artifacts are sourced from targets-local inputs.

## Transport Separation

- `/dev/ttyUSB0` is wire protocol transport for `nanoff` and deployment operations.
- User interactive CLI commands (`show`, `get`, `set`, etc.) are validated on USB CDC console (PuTTY path), not wire protocol UART.
- USB CDC console is not accessible from this container environment. The user must issue commands and report back results.

## LNB Diagnostics Expectation

- Prefer native firmware fixes for LNBH26 issues.
- Avoid managed fallbacks as long-term behavior; they are temporary diagnostics only.
- Keep managed console output user-clean (`OK`/`Fail`) and put detailed diagnostics in debug logs.
