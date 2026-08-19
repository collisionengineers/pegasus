---
id: TICK-105
type: ticket
title: MI-01 — Per-Engineer throughput and query rate/types
status: backlog
area: platform-operations
assignee: ''
profile: feature
labels:
  - capability
  - MI-01
  - later
  - post-alpha
  - blocked
groups:
  - EPIC-003
links:
  - TICK-205
  - TICK-098
archived: false
created: '2026-08-12T15:06:02.826Z'
updated: '2026-08-19T10:49:44.874Z'
---

## What

Plan and research **MI-01**: Per-Engineer throughput and query rate/types.

## Why

This is allocated to **Later / 1.2.0** in `docs/capabilities.md`. It is not designated until post-alpha and is blocked from implementation pending its activation decision and evidence.

The previous “Audit uplift” metric was based on the same false dual-specification premise corrected by the operator on 2026-08-19. It is removed from this ticket. Audit and Inspection reports are physically identical; Audit differs only in internal workflow/reference identity.

## Approach

- At activation, define the Core policy owner, caller, contract, failure behavior, and acceptance evidence for throughput and query measures.
- Reconcile the stale MI-01 capability wording in `docs/capabilities.md` before implementation.
- Reuse accepted workflow events; do not infer an Audit uplift measure.

## Verification

- [ ] A task-level plan covers the exact throughput/query contract and tests.
- [ ] The governing capability wording no longer claims Audit uplift.
- [ ] All activation conditions are accepted before implementation starts.

## Notes

- Source capability: MI-01, requiring correction before implementation.
- Related correction: [[TICK-205]] and [[TICK-098]].
