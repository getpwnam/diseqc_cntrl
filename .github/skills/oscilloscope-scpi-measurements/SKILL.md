---
name: oscilloscope-scpi-measurements
description: "Use when taking oscilloscope measurements over SCPI (Siglent SDS814X HD on TCP 5024), validating instrument connectivity, configuring channels/timebase/trigger, and collecting repeatable numeric results. Keywords: oscilloscope, SCPI, Siglent, SDS814X HD, TCP 5024, C1:PAVA?, VPP, RMS, frequency, remote measurement."
---

# Oscilloscope SCPI Measurements

## Purpose

Run deterministic oscilloscope measurements over SCPI and return evidence-backed values while coordinating required human hardware setup.

## Primary Targets

- Scope host: `172.17.129.61`
- SCPI port: `5024`
- Related docs:
  - `docs/debug/POWER_SUPPLY_OSCILLOSCOPE_TESTS.md`
  - `docs/debug/TESTING_GUIDE.md`
  - `docs/debug/BRINGUP_TEST_LOG.md`

## Human-in-the-Loop Requirement

Before any acquisition or measurement command, request human confirmation of wiring and probe setup.

Required confirmations:

1. Probe tip connected to the intended test node.
2. Probe ground connected to the correct ground reference.
3. Probe attenuation set correctly on both probe and scope (for example 10X).
4. Voltage expected at the node is within probe/scope limits.
5. Channel selection is correct (for example CH1 for `C1:*` commands).

If this confirmation is missing, stop and ask for it instead of issuing measurement commands.

## When To Use

- User asks to take direct measurements from the Siglent scope.
- Reproducible numeric values are required (VPP, RMS, frequency, period, mean).
- You need to verify a waveform condition during bring-up without manual front-panel operation.

## Workflow

1. Open TCP session and consume the banner line.
2. Verify identity and readiness:
   - `*IDN?`
   - `*OPC?`
3. Confirm or set acquisition state (RUN/STOP) per test intent.
4. Configure minimally required measurement context:
   - channel (`C1`, `C2`, etc.)
   - vertical scale and offset if requested
   - timebase and trigger source/level/mode if requested
5. Query measurement values using explicit SCPI commands.
6. Repeat samples when requested (for example N reads over T seconds).
7. Return values plus command evidence and any instrument status notes.
8. Append factual log entry to `docs/debug/BRINGUP_TEST_LOG.md` when requested.

## Canonical SCPI Query Pattern (Siglent)

- On connect, read first line banner.
- Identity:
  - `*IDN?`
- Readiness:
  - `*OPC?`
- Example direct measurements on CH1:
  - `C1:PAVA? PKPK`
  - `C1:PAVA? RMS`
  - `C1:PAVA? FREQ`
  - `C1:PAVA? PERI`
  - `C1:PAVA? MEAN`

Notes:

- The instrument may prefix responses with `>>`.
- `****` typically means no valid measurement under current signal/trigger context.

## Output Format

- Connection:
  - scope host/port
  - `*IDN?` response
- Human setup confirmation:
  - confirmed yes/no
  - channel and probe ratio
- Measurement results:
  - metric name
  - raw response
  - parsed numeric value (if parseable)
  - units
- Reliability notes:
  - trigger state
  - invalid markers (`****`) and likely cause
- Optional next action:
  - one concrete adjustment (trigger, scale, coupling, wiring)

## Guardrails

- Never assume probe wiring; always require human confirmation first.
- Prefer non-destructive SCPI queries before any configuration writes.
- If writing scope configuration, state each write command explicitly before executing.
- Keep retries bounded and report partial data rather than hanging.
- If responses are invalid (`****`), suggest one specific setup correction and retry once.
- Do not claim physical measurements were taken unless SCPI responses were actually received in-session.
