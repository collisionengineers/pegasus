---
id: TICK-207
type: ticket
title: Record Audit reuse of the Inspection report template
status: done
area: documents-reports
order: 840
assignee: codex-mcp-client
profile: feature
stageEntered:
  preparing: '2026-08-19T09:04:30.646Z'
  review: '2026-08-19T09:37:36.335Z'
  verifying: '2026-08-19T09:38:07.704Z'
  done: '2026-08-19T09:39:17.327Z'
labels:
  - now
  - source-now
  - decision-required
groups:
  - EPIC-004
links:
  - TICK-098
  - SIMPLI-015
refs:
  - docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md
  - docs/adr/0025-integrate-renderer-and-extractor-into-the-application.md
deployment: n/a
archived: false
created: '2026-08-12T15:08:05.409Z'
updated: '2026-09-03T09:06:46.509Z'
---

## What

Record that Audit uses the same physical report output as Inspection; no separate Audit renderer template is required.

## Why

The operator corrected the earlier premise on 2026-08-19: Collision Engineers' Audit and Inspection processes differ internally, but the physical report they output has no differences. Waiting for or inventing a separate Audit template would create a duplicate presentation policy.

## Approach

Reuse the approved inspection/assessment report template and presentation through the existing Core-owned render contract. Feed it accepted Audit-specific workflow data from the owning Core capabilities, including conservative/maximised specifications and monetary uplift where applicable. Do not create an Audit-only template, dormant descriptor, generic fallback, or separate renderer family.

## Verification

- [x] The operator decision and shared-template boundary are explicit.
- [x] [[TICK-098]], [[TICK-205]], [[SIMPLI-014]], and related renderer tickets record that Audit-specific process data reuses the inspection/assessment physical output.

## Notes

- [[TICK-205]] owns the dual immutable conservative/maximised Audit data decision.
- [[TICK-098]] owns RPT-03 behavior through the shared renderer contract.
- This correction supersedes the former missing-template deferral and any request for a separate representative Audit artifact.

## Tracker migration

Authority references were retargeted by [[KANMER-001]] after the legacy tracker was retired.

## Outcome

The missing-template premise is closed: Audit and Inspection use the same physical report template and presentation. Their process and accepted input data may differ, but Pegasus must not create a second Audit presentation implementation.
