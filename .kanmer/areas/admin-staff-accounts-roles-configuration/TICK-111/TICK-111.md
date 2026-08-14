---
id: TICK-111
type: ticket
title: Remove the temporary production verification Administrator before go-live
status: todo
area: admin-staff-accounts-roles-configuration
priority: medium
assignee: ''
labels:
  - now
  - source-now
  - requires-live-approval
links:
  - TICK-032
archived: false
created: '2026-08-12T15:08:02.286Z'
updated: '2026-08-12T15:08:02.286Z'
---

## What

Remove the temporary production verification Administrator before go-live.

## Why

This remains an unresolved current-work item in the authoritative `NOW.md`; it is a planning/research unit until taken.

## Approach

- At activation, re-check the exact current source, caller, and evidence state before choosing an implementation path.
- Write the task-level plan first; do not infer authority for live, credential, mailbox, Box, Azure, or other external operations.

## Verification

- [ ] The task plan defines the owned change, failure behavior, tests, and acceptance evidence.
- [ ] Completion is recorded only at the evidence tier actually proved.

## Notes

- Source: `NOW.md` — Production state — verification account.
- Related capability: OPS-25 ([[TICK-032]]).
- Live-system work requires fresh exact-target approval before any external operation.
