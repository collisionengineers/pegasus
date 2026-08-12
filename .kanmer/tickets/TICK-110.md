---
id: TICK-110
type: ticket
title: Reconcile local azd state against the observed production estate
status: todo
area: platform-azure-production-release-estate
priority: medium
assignee: ''
labels:
  - now
  - source-now
  - requires-live-approval
links:
  - TICK-001
archived: false
created: '2026-08-12T15:08:02.263Z'
updated: '2026-08-12T15:08:02.263Z'
---

## What

Reconcile local azd state against the observed production estate.

## Why

This remains an unresolved current-work item in the authoritative `NOW.md`; it is a planning/research unit until taken.

## Approach

- At activation, re-check the exact current source, caller, and evidence state before choosing an implementation path.
- Write the task-level plan first; do not infer authority for live, credential, mailbox, Box, Azure, or other external operations.

## Verification

- [ ] The task plan defines the owned change, failure behavior, tests, and acceptance evidence.
- [ ] Completion is recorded only at the evidence tier actually proved.

## Notes

- Source: `NOW.md` — Next — local azd reconciliation.
- Related capability: OPS-10 ([[TICK-001]]).
- Live-system work requires fresh exact-target approval before any external operation.
