# Unified Interface Plan - Phase 1

Status: complete for review

Canonical issue: https://github.com/getpwnam/diseqc_cntrl/issues/86

## Objective

Build a current-state contract inventory and identify gaps against the canonical target interface.

## Sources Reviewed

- docs/software/MQTT_API.md
- docs/software/CONFIGURATION.md
- docs/software/ARCHITECTURE.md
- software/nanoFramework/CubleySmokeTier2_FRAM_LNBH26/Program.cs

## Current-State Inventory

1. Active managed app in the current workspace is a smoke harness focused on FRAM and LNB checks.
2. No active serial command parser is present in the current managed app.
3. No active MQTT command router is present in the current managed app.
4. Existing documented MQTT contract uses a diseqc root namespace with command and status branches.
5. Existing documented serial surface is configuration-focused and lists config commands.
6. Existing docs describe configuration keys for network, mqtt, and system domains.

## Canonical Target from Issue 86

1. Root namespace is cubley.
2. Canonical order is version before domain.
3. Current domain is cubley/v1/diseqc.
4. Future reserved domain is cubley/v1/azel.
5. Shared platform branch includes cubley/v1/system/state/availability and cubley/v1/system/meta/capabilities.
6. Serial and MQTT must remain aligned feature-for-feature.
7. No backward compatibility is required.

## Gap Map

1. Namespace gap: current docs are diseqc-rooted, canonical target is cubley/v1/domain-rooted.
2. Domain gap: current docs are single-domain, canonical target requires explicit diseqc and reserved azel domains plus shared system branch.
3. Topic-family gap: current docs are command and status oriented, canonical target requires command, state, event, and meta families.
4. Payload/result gap: current docs do not define a shared response envelope across serial and MQTT.
5. Parity gap: current docs do not provide a full mapping matrix proving each external function exists on both serial and MQTT.
6. Serial UX gap: status bar behavior is not yet specified in a normative contract doc.
7. Implementation gap: active code does not yet implement the planned external interface surface.

## Phase 1 Deliverables Produced

1. Baseline inventory of what is documented today.
2. Gap list tied to issue 86 target behavior.
3. List of decisions needed before Phase 2 modeling.

## Decisions Needed Before Phase 2

1. Confirm whether command names under cubley/v1/diseqc should keep existing verb forms from docs or be normalized during redesign.
2. Confirm whether config operations remain in diseqc domain only, or are split into system domain for cross-domain reuse.
3. Confirm minimum required state and event fields for the shared response envelope in v1.
4. Confirm whether serial status bar must include both diseqc and future azel placeholders in v1, or diseqc-only now.

## Exit Criteria Check

1. Inventory captured: yes.
2. Gaps identified: yes.
3. Uncertainties explicitly listed for user decision: yes.
