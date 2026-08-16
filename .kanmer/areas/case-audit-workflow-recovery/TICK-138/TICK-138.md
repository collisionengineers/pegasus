---
id: TICK-138
type: ticket
title: Restore end-to-end standalone-Audit case creation
status: backlog
area: case-audit-workflow-recovery
assignee: ''
profile: feature
labels:
  - now
  - source-now
links:
  - TICK-012
archived: false
created: '2026-08-12T15:08:03.159Z'
updated: '2026-08-12T15:08:03.159Z'
---

## What

Restore end-to-end standalone-Audit case creation.

## Why

This remains an unresolved current-work item in the authoritative `NOW.md`; it is a planning/research unit until taken.

## Approach

- At activation, re-check the exact current source, caller, and evidence state before choosing an implementation path.
- Write the task-level plan first; do not infer authority for live, credential, mailbox, Box, Azure, or other external operations.

## Verification

- [ ] The task plan defines the owned change, failure behavior, tests, and acceptance evidence.
- [ ] Completion is recorded only at the evidence tier actually proved.

## Notes

- Source: `NOW.md` — Next — standalone Audit recovery.
- Related capability: INT-25 ([[TICK-012]]).
