---
id: TICK-205
type: ticket
title: Record that Audit does not require a dual-specification or uplift model
status: done
area: documents-reports
order: 810
assignee: codex-mcp-client
profile: feature
stageEntered:
  preparing: '2026-08-19T09:02:10.979Z'
  review: '2026-08-19T09:31:01.506Z'
  verifying: '2026-08-19T09:31:14.297Z'
  done: '2026-08-19T09:32:02.301Z'
labels:
  - now
  - source-now
  - decision-required
groups:
  - EPIC-004
links:
  - TICK-093
  - SIMPLI-015
  - TICK-098
  - TICK-207
refs:
  - docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md
  - docs/adr/0025-integrate-renderer-and-extractor-into-the-application.md
  - docs/frd/frd-06-vehicle-and-engineering-evidence.md
deployment: n/a
archived: false
created: '2026-08-12T15:08:05.306Z'
updated: '2026-09-03T09:06:46.352Z'
---

## What

Record the operator correction that Audit reports do not require conservative/maximised specifications or uplift.

## Why

The earlier apparent conflict was based on a false premise. Audit and Inspection reports are physically identical. The only Audit distinction relevant here is internal workflow/reference identity: the normal Case/PO plus `a.{Case/PO}` for repairable or `ap.{Case/PO}` for total loss.

## Outcome

- One canonical accepted repair specification remains the shared rule.
- No Audit-only dual-specification aggregate, role pair, uplift calculation, or presentation is required.
- [[TICK-098]] owns reconciliation of stale RPT-03 governing wording and implementation through the shared Inspection report path.
- [[TICK-207]] records reuse of the Inspection report template.
- This correction supersedes the earlier TICK-205 decision; no implementation or deployment was delivered by this ticket.
