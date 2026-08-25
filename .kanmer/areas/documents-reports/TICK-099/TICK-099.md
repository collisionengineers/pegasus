---
id: TICK-099
type: ticket
title: >-
  RPT-04 — Diminution rendering uses accepted original-case data plus the
  Engineer-entered percentage
status: done
area: documents-reports
order: 1050
assignee: codex-mcp-client
profile: feature
stageEntered:
  preparing: '2026-08-19T09:06:16.263Z'
  review: '2026-08-19T09:43:39.464Z'
  verifying: '2026-08-19T09:43:55.850Z'
  done: '2026-08-19T09:44:38.366Z'
labels:
  - capability
  - RPT-04
  - later
groups:
  - EPIC-004
links:
  - TICK-092
  - TICK-093
  - TICK-094
  - TICK-206
  - SIMPLI-014
  - DOCS-003
refs:
  - docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md
  - docs/adr/0025-integrate-renderer-and-extractor-into-the-application.md
deployment: n/a
archived: false
created: '2026-08-12T15:06:02.703Z'
updated: '2026-08-25T06:46:14.502Z'
---

## What

Plan and research **RPT-04**: Diminution rendering uses accepted original-case data plus the Engineer-entered percentage

## Why

This is allocated to **Later / 1.1.0** in `docs/capabilities.md`. It is **not designated until post-alpha** and is blocked from implementation pending its activation decision and evidence.

## Approach

- At activation, define the Core policy owner, caller, contract, failure behavior, and acceptance evidence.
- Re-check the exact activation boundary in `docs/capabilities.md`; allocation alone is not implementation or deployment.

## Verification

- [x] A task-level plan records the unsupported/deferred boundary and the future activation contract required before implementation.
- [x] No activation condition is treated as accepted; RPT-04 remains unavailable and fail closed.

## Notes

- Source: `docs/capabilities.md` — RPT-04.
- Blocked by: [[TICK-092]] — The renderer activation waits for accepted structured case and engineering data.
- Blocked by: [[TICK-093]] — The renderer activation waits for an accepted repair-specification contract.
- Blocked by: [[TICK-094]] — The renderer activation waits for accepted Engineer-owned outcomes and values.


## Outcome

RPT-04 is explicitly **unsupported, unavailable, and deferred**. The Later / 1.1.0 allocation is not activation, and the generic workspace `diminution-rebuttal` preset is not product authority. No diminution template, callable operation, descriptor, feature flag, API, Core contract, Infrastructure adapter, artifact, deployment, or repository change is created by this ticket.

A future linked activation ticket is required before implementation. It must establish accepted original-case identity/version, Engineer-entered percentage meaning and precision, calculation and rounding, wording and layout, human approval, correction/version linkage, a real caller, fail-closed behaviour, and representative evidence. [[TICK-092]], [[TICK-093]], and [[TICK-094]] retain ownership of their upstream case and engineering policy; [[TICK-206]] retains the inactive-catalogue decision; [[SIMPLI-014]] remains assessment/fee-note only.

Prohibited substitutes include exposing or adapting `diminution-rebuttal`, assessment-template cloning, free-form caller content, placeholders, dormant descriptors, disabled-feature implementations, and inferred professional or legal wording. Completion is claimed only at the deferral/closed-boundary tier.
