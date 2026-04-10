---
name: bringup-log-entry
description: "Use when appending factual checkpoints to BRINGUP_TEST_LOG.md after firmware build/flash/deploy/probe runs. Keywords: bringup log, append test result, PASS FAIL INFO entry, command artifact breakpoints conclusion, bringup_log_append.sh."
---

# Bring-up Log Entry

## Purpose

Append concise, factual test-session entries to the repository bring-up log using the existing helper script.

## Primary Targets

- software/nanoFramework/docs/BRINGUP_TEST_LOG.md
- software/nanoFramework/toolchain/bringup_log_append.sh

## When To Use

- After a build/flash/deploy/probe run where outcome should be recorded.
- After transport diagnostics (SWD/UART) to preserve reproducible evidence.
- When converting ad hoc terminal output into a standard PASS/FAIL/INFO entry.

## Workflow

1. Collect run facts from terminal history and artifacts:
   - result category: PASS, FAIL, or INFO
   - one-line conclusion
   - command summary
   - artifact path(s)
   - breakpoint list (if relevant)
   - optional note
2. Validate that conclusion is factual and non-speculative.
3. Call the helper script with explicit flags:
   - ./toolchain/bringup_log_append.sh --result ... --conclusion ...
   - include --commands, --artifact, --breakpoints, and --note when available
4. Confirm the append succeeded and report the timestamped heading added.

## Output Format

- "Log append: SUCCESS" or "Log append: FAILED"
- Entry summary:
  - timestamp
  - result
  - conclusion
  - any missing fields intentionally omitted

## Guardrails

- Never rewrite or reorder prior log history.
- Keep conclusions short and evidence-based.
- Prefer one entry per coherent test run.
- If required fields are missing, ask for only the minimum needed facts (result + conclusion).
