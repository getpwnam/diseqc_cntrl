# Phase A Exit Decision Record (2026-06-07)

> Status update (2026-06-08): This decision record is historical and was
> superseded by the cubley-base program re-baseline decision in
> [getpwnam/diseqc_cntrl#52](https://github.com/getpwnam/diseqc_cntrl/issues/52).
> Use [PHASE_A_BASELINE.md](./PHASE_A_BASELINE.md) as the authoritative current
> baseline reference. Mentions of `cubley-stable` below are legacy context from
> the original 2026-06-07 freeze point.

## Decision

**Phase A is exited and frozen** as of 2026-06-07.

The baseline is accepted as release-ready for downstream Phase B work, with known residual risks tracked below.

## Scope Covered by This Decision

- Deterministic firmware baseline for `M0DMF_CUBLEY_F407`.
- Per-component functional smoke criteria and Phase A exit gate.
- Deterministic 20-cycle flash-reset transport campaign outcome.

## Pinned Baseline

### Repository baseline commit

- **Pinned baseline commit:** `ed920c74ab30cab1d0e2703c1cee758246cb8b26`
- **Merge source:** PR #39 (issue #26 campaign + helper)
- **Rationale:** this commit includes the verified 20/20 deterministic cycle campaign evidence and the reusable deterministic cycle helper.

### Baseline firmware/profile contract

- **Build profile:** `cubley-stable`
- **Flash addresses:**
  - nanoBooter: `0x08000000`
  - nanoCLR: `0x08004000`
  - managed deployment: `0x080C0000`
- **Reference:** [PHASE_A_BASELINE.md](./PHASE_A_BASELINE.md)

### Baseline campaign command (proven)

```bash
./toolchain/run-deterministic-cycles.sh \
  --cycles 20 \
  --serial /dev/ttyUSB0 \
  --baud 115200 \
  --settle-ms 2000
```

## Evidence Links

### Exit criteria and smoke definitions

- [PHASE_A_FUNCTIONAL_SMOKE_CHECKS.md](./PHASE_A_FUNCTIONAL_SMOKE_CHECKS.md)
- [Phase A Exit Gate section](./PHASE_A_FUNCTIONAL_SMOKE_CHECKS.md#phase-a-exit-gate)
- [TESTING_GUIDE.md](./TESTING_GUIDE.md)

### Deterministic campaign evidence

- Issue #26: https://github.com/getpwnam/diseqc_cntrl/issues/26
- PR #39: https://github.com/getpwnam/diseqc_cntrl/pull/39
- Bring-up log PASS entry (`20/20`, zero failures): [BRINGUP_TEST_LOG.md](./BRINGUP_TEST_LOG.md)
- Campaign artifact root recorded in log: `.debug/issue26_campaign_20260607T215707Z`

## Remaining Risks and Phase B Handoff Inputs

| Risk ID | Risk | Owner | Impact | Mitigation / Phase B Input |
|---|---|---|---|---|
| R1 | UART transport path can be host-environment sensitive (e.g., USB/monitor sleep flapping observed during investigation) | Firmware/bring-up owner (`getpwnam`) | Intermittent `nanoff` enumeration failures can invalidate campaign runs if host power policy is unstable | Use deterministic cycle helper with explicit reset + settle; keep host display/USB power policy locked during runs; carry transport stability checks into Phase B test preflight |
| R2 | Temporary diagnostics and startup instrumentation may still exist in build pipeline from bring-up hardening | Firmware/interop owner (`getpwnam`) | Instrumentation drift can create noise in downstream performance or behavior comparisons | Track cleanup explicitly in Phase B backlog and remove temporary instrumentation once interop governance gates are active |
| R3 | Hardware variance between benches can alter bring-up behavior while still passing baseline on primary bench | Hardware + firmware owner (`getpwnam`) | Reproducibility risk across benches | Re-run deterministic cycle helper on secondary bench before Phase B milestone lock |

## Handoff to Phase B

Phase B may assume:

1. `cubley-stable` is the pinned functional baseline profile.
2. Flash map and tooling contract in [PHASE_A_BASELINE.md](./PHASE_A_BASELINE.md) are authoritative.
3. Deterministic campaign helper in `software/nanoFramework/toolchain/run-deterministic-cycles.sh` is the canonical replay mechanism for transport repeatability.
4. Any deviation from baseline must be explicitly marked as non-baseline in [BRINGUP_TEST_LOG.md](./BRINGUP_TEST_LOG.md).

## Issue/Program Traceability

- Parent issue: https://github.com/getpwnam/diseqc_cntrl/issues/12
- This task issue: https://github.com/getpwnam/diseqc_cntrl/issues/29
- Blocking task completed: https://github.com/getpwnam/diseqc_cntrl/issues/26
