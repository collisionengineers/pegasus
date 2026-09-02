---
id: TICK-097
type: ticket
title: >-
  RPT-02 — Assessment rendering covers four outcome variants and emits the fee
  note plus itemised repair-specification breakdown
status: preparing
area: documents-reports
order: 190
assignee: ''
profile: feature
stageEntered:
  preparing: '2026-08-19T09:05:50.417Z'
labels:
  - capability
  - RPT-02
  - now
groups:
  - EPIC-004
links:
  - TICK-092
  - TICK-093
  - TICK-094
blocks:
  - TICK-081
refs:
  - docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md
  - docs/adr/0025-integrate-renderer-and-extractor-into-the-application.md
archived: false
created: '2026-08-12T15:06:02.661Z'
updated: '2026-09-01T14:50:16.800Z'
---

## What

Plan and research **RPT-02**: Assessment rendering covers four outcome variants and emits the fee note plus itemised repair-specification breakdown.

## Why

This capability is allocated to **Now / 0.1.0-alpha.1** in `docs/capabilities.md`. The report-draft entry point is delivered, but readiness correctly remains closed while Pegasus has no accepted repair-cost rate-card or paint-materials formula. No value may be fabricated to bypass that boundary.

## Approach

- Keep the current preparation documents as the task-level implementation record.
- Resolve the accepted repair-cost formula and structured data prerequisites before enabling a complete outcome render.
- Reuse the existing Assessment entry point and renderer snapshot; do not create a parallel route.

## Verification

- [ ] All four outcome variants use accepted structured case and engineering data.
- [ ] The fee note and itemised repair-specification breakdown are produced from accepted values.
- [ ] Readiness names missing repair-cost figures and keeps generation closed until they are derivable.
- [ ] No caller supplies or fabricates a value owned by Core policy.

## Notes

- Source: `docs/capabilities.md` — RPT-02.
- Related prerequisites: [[TICK-092]], [[TICK-093]], and [[TICK-094]].
- The existing plan and checklist are not rewritten by this allocation correction.
