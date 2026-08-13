---
id: TICK-028
type: ticket
title: 'Establish database backup, restore, RPO, and RTO capability'
status: todo
area: operations-backup-restore-recovery
priority: medium
assignee: ''
labels:
  - capability
  - OPS-09
  - now
  - requires-live-approval
links: []
archived: false
created: '2026-08-12T15:03:53.347Z'
updated: '2026-08-13T14:40:07.630Z'
---

## What

Establish the database recovery capability that can create a supported backup, restore it into a new database, verify the restored result, and measure the outcome against the 15-minute RPO and four-hour RTO objectives.

## Why

A written procedure or isolated backup artifact is insufficient. The capability requires an executable, recoverable route with retained evidence and safe reclamation behavior.

## Approach

- Use the runbook-owned production recovery contract and supported SQL Server boundary.
- Implement or correct any missing backup, restore, verification, and cleanup behavior.
- Exercise the migrated validation checks in this ticket's `checklist.md`.
- Preserve exact target enumeration, recovery-source checks, and approval gates before destructive or live operations.

## Verification

- [ ] A backup can be produced through the supported route.
- [ ] The backup restores into a new database and the result is verified.
- [ ] Measured RPO/RTO evidence and limitations are recorded.
- [ ] Abandoned LocalDB and backup-file reclamation has a safe, observed outcome.

## Notes

- Source capability: OPS-09.
- Canonical procedure: `docs/runbook.md#production-recovery`.
- The capability is non-blocking for the alpha release but remains real work, not a standalone proof ticket.
