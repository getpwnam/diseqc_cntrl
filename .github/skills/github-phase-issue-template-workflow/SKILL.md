---
name: github-phase-issue-template-workflow
description: "Use when drafting phase work-package issues and sub-issues with consistent templates, dependency chains, and physical verification gates. Keywords: issue body template, phase issue, acceptance criteria, physical verification checkpoints, dependency map."
---

# GitHub Phase Issue Template Workflow

## Purpose

Generate consistent, execution-ready issue bodies for phase-level work and child work packages, including dependency chains and hardware verification checkpoints.

## Primary Targets

- Parent phase issue bodies (for example: Phase D2)
- Child issue bodies (for example: Phase D2.1, D2.2, D2.3)
- Parent checklist + dependency consistency across parent/children

## When To Use

- User asks to start a phase and wants issues created quickly.
- User asks for sub-issues under a phase parent.
- User wants explicit acceptance gates and physical validation points.

## Workflow

1. Capture scope from the user request.
- Identify phase objective, first target subsystem, and constraints.
- Convert vague goals into measurable acceptance checks.

2. Draft parent issue first.
- Include Context, Scope, Acceptance Criteria, Dependencies, Sub-Issues, Physical Verification Gate, and Notes.
- Keep acceptance checks deterministic and binary where possible.

3. Draft child issues as executable units.
- One concrete capability per child issue.
- Each child has its own Dependencies and Physical Verification Checkpoints.

4. Wire dependency chain.
- Child dependencies should point to upstream child prerequisites.
- Parent dependency block lists all required children.
- Avoid circular dependency relationships.

5. Validate consistency before publishing.
- Every acceptance criterion maps to at least one child issue.
- Every physical gate appears in at least one child checkpoint.
- Parent Sub-Issues checklist matches actual created issue numbers.

## Parent Issue Template

```markdown
## Context
<Phase transition statement + why this phase matters now.>

## Scope
- <Constraint 1>
- <Constraint 2>
- <Constraint 3>

## Acceptance Criteria
- <Deterministic criterion 1>
- <Deterministic criterion 2>
- <Deterministic criterion 3>

## Dependencies
Blocked by: #<child1>, #<child2>, #<child3>

## Sub-Issues
- [ ] #<child1>
- [ ] #<child2>
- [ ] #<child3>

## Physical Verification Gate
- [ ] <Speed measurement checkpoint>
- [ ] <Duplex/state measurement checkpoint>
- [ ] <Transition stability checkpoint>
- [ ] <Power or signal integrity checkpoint>

## Notes
<Promotion policy / out-of-scope constraints / references>
```

## Child Issue Template

```markdown
## Context
<Why this specific capability is needed.>

## Scope
- <Implementation or validation scope 1>
- <Implementation or validation scope 2>

## Acceptance Criteria
- <Measurable pass criterion 1>
- <Measurable pass criterion 2>

## Dependencies
Blocked by: #<upstream issue or none>

## Physical Verification Checkpoints
- [ ] <Measurement checkpoint 1>
- [ ] <Measurement checkpoint 2>

## Notes
<Evidence source and constraints>
```

## Physical Verification Patterns

Use concrete, observable checks (not generic wording):
- Link state transitions: cable connect/disconnect and recovery timing.
- Negotiated speed: record value for each validated link-up scenario.
- Duplex mode: record and confirm expected mode per network profile.
- Signal behavior: SPI clock/chip-select activity before and after trigger.
- Rail stability: capture supply behavior across reset/relink windows.

## Dependency Patterns

Use this sequence for subsystem stabilization:
1. Baseline activation path (no hidden startup side effects).
2. Hardware state determinism (version/PHY/link transitions).
3. Runtime lifecycle determinism (connect/send/recv/close paths).
4. Contract lock and documentation freeze.

## Output Contract

When done, report:
- Parent issue number + URL.
- Child issue numbers + URLs.
- Dependency chain summary.
- Physical verification checklist summary.
- Confirmation that parent checklist matches child list.

## Guardrails

- Do not leave acceptance criteria as qualitative statements.
- Do not omit physical checkpoints when hardware behavior is in scope.
- Do not create children with overlapping ownership or duplicate scope.
- Keep issue text factual, concise, and test-oriented.
