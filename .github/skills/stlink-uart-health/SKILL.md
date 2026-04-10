---
name: stlink-uart-health
description: "Use when diagnosing board connectivity, ST-Link access, SWD mailbox reads, nanoff E2001/E2002 errors, /dev/ttyUSB0 instability, transport contention, or deciding whether failures are wiring, host permissions, firmware mode, or tool timing. Keywords: ST-Link, SWD, UART preflight, nanoff listdevices, devicedetails, bringup transport triage."
---

# ST-Link and UART Health Triage

## Purpose

Run a deterministic, non-destructive transport triage for this board and classify failures quickly.

## Primary Targets

- software/nanoFramework/toolchain/uart-preflight.sh
- software/nanoFramework/tests/swd_read_bringup_status.sh
- software/nanoFramework/toolchain/build.sh
- software/nanoFramework/docs/BRINGUP_TEST_LOG.md

## When To Use

- nanoff listdevices/devicedetails intermittently fail.
- SWD works but UART does not, or vice versa.
- There are E2001/E2002 deployment errors.
- You need a clear next action instead of ad hoc probing.

## Workflow

1. Verify basic transport visibility:
   - serial device presence
   - st-info probe success
2. Run UART preflight:
   - classify timeout vs no-device vs partial success
3. Run SWD mailbox read:
   - confirm target is responsive independently of UART
4. If needed, run reset then retry nanoff listdevices/devicedetails once.
5. Classify into one of these buckets:
   - Host-side access or permission issue
   - Cable/orientation or physical UART path issue
   - Firmware profile not exposing wire protocol
   - Debugger contention or timing race
   - Intermittent transport requiring bounded retries
6. Provide one specific next action for the detected bucket.
7. Append a concise factual log entry to BRINGUP_TEST_LOG.md when requested.

## Output Format

- Transport state: PASS, DEGRADED, or FAIL
- Evidence lines:
  - st-info result
  - nanoff listdevices result
  - nanoff devicedetails result
  - swd mailbox result
- Root-cause bucket
- Next action (single highest-value step)

## Guardrails

- Prefer non-destructive commands first.
- Avoid long unbounded loops.
- Keep retries bounded and explicit.
- Separate transport failures from firmware functional failures.
