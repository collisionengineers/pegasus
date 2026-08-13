---
id: TICK-205
type: ticket
title: >-
  Resolve the canonical repair-specification versus dual-Audit-specification
  conflict
status: todo
area: reports-renderer-lifecycle-correspondence
priority: medium
assignee: ''
labels:
  - now
  - source-now
  - decision-required
links:
  - TICK-093
archived: false
created: '2026-08-12T15:08:05.306Z'
updated: '2026-08-12T15:08:05.306Z'
---

## What

Resolve the canonical repair-specification versus dual-Audit-specification conflict.

## Why

This remains an unresolved current-work item in the authoritative `NOW.md`; it is a planning/research unit until taken.

## Approach

- At activation, re-check the exact current source, caller, and evidence state before choosing an implementation path.
- Write the task-level plan first; do not infer authority for live, credential, mailbox, Box, Azure, or other external operations.

## Verification

- [ ] The task plan defines the owned change, failure behavior, tests, and acceptance evidence.
- [ ] Completion is recorded only at the evidence tier actually proved.

## Notes

- Source: `NOW.md` — Next — renderer capability questions.
- Related capability: ENG-01 ([[TICK-093]]).
