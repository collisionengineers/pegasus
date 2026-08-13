---
id: TICK-028
type: ticket
title: 'OPS-09 — Database backup, restore proof, 15-minute RPO, and four-hour RTO'
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
updated: '2026-08-12T15:03:53.347Z'
---

## What

Plan and research **OPS-09**: Database backup, restore proof, 15-minute RPO, and four-hour RTO

## Why

The capability inventory allocates this outcome to **Now / 0.1.0-alpha.1**. This is a current allocation with incomplete evidence or activation work; plan the remaining caller, contract, and acceptance proof before implementation.

## Approach

- Establish the current Core policy owner, real caller, persistence/infrastructure boundary, and acceptance evidence before proposing implementation.
- Recover and resolve the stated activation boundary without treating allocation, registration, or a build as deployment or acceptance.

## Verification

- [ ] A task-level plan records the exact feature contract, caller, failure behavior, and required tests.
- [ ] The activation criteria have been satisfied or explicitly accepted before implementation begins.

## Notes

- Source: `docs/capabilities.md` — OPS-09.
- Canonical owner: [Quality and recovery objectives](requirements.md#quality-capacity-security-and-evidence)
- Activation/boundary: Allocated but non-blocking; deferred and gates no release (2026-08-03). The linked requirement owns the objectives and the [runbook](runbook.md#production-recovery) owns the proof procedure.
