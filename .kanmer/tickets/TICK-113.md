---
id: TICK-113
type: ticket
title: Re-drive allocation for receipts stranded before QDOS Principal setup
status: todo
area: case-allocation-principals-references
priority: medium
assignee: ''
labels:
  - now
  - source-now
  - requires-live-approval
  - blocked
links:
  - TICK-012
  - TICK-112
archived: false
created: '2026-08-12T15:08:02.331Z'
updated: '2026-08-12T15:09:20.321Z'
---

## What

Re-drive allocation for receipts stranded before QDOS Principal setup.

## Why

This remains an unresolved current-work item in the authoritative `NOW.md`; it is a planning/research unit until taken.

## Approach

- At activation, re-check the exact current source, caller, and evidence state before choosing an implementation path.
- Write the task-level plan first; do not infer authority for live, credential, mailbox, Box, Azure, or other external operations.

## Verification

- [ ] The task plan defines the owned change, failure behavior, tests, and acceptance evidence.
- [ ] Completion is recorded only at the evidence tier actually proved.

## Notes

- Source: `NOW.md` — Next — QDOS principal absence.
- Related capability: INT-25 ([[TICK-012]]).
- Blocked by: Establish the QDOS Organisation and Principal in production.
- Live-system work requires fresh exact-target approval before any external operation.
- Blocked by: [[TICK-112]] — Allocation cannot be re-driven until the active QDOS Principal exists.
