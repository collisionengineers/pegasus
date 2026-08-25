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
  - CASE-002
refs:
  - docs/frd/frd-06-vehicle-and-engineering-evidence.md
archived: false
created: '2026-08-12T15:06:02.826Z'
updated: '2026-08-25T06:38:34.452Z'
---

## What

Plan and research **MI-01**: Per-Engineer throughput and the rate/types of post-report queries raised to each Engineer.

## Why

This is allocated to **Later / 1.2.0** and is not designated until post-alpha.

Operator corrections on 2026-08-19:

- Audit uplift is not a real measure; Audit and Inspection reports are physically identical.
- Engineers do not raise queries. Queries are raised **to** Engineers after a report has been sent.

## Approach

- Consume only accepted workflow events owned by [[CASE-002]]: exact sent report version, query received/source, responsible Engineer, type, response, resolution, and follow-up.
- Define the denominator and reporting period before calculating a query rate; do not imply the Engineer originated the query.
- Reconcile stale MI-01 wording in `docs/capabilities.md` before implementation.
- Define coaching access and privacy/authorization through the governing capability rather than the presentation layer.

## Verification

- [ ] Query measures are explicitly post-report queries raised to an Engineer.
- [ ] Rate denominator, time period, query taxonomy, reassignment treatment, and visibility are accepted.
- [ ] No Audit uplift metric remains in capability wording or implementation.
- [ ] Measures derive from accepted events and preserve report/Engineer/query provenance.

## Notes

- [[CASE-002]] owns the future query workflow.
- [[TICK-098]] and [[TICK-205]] record the Audit correction.
