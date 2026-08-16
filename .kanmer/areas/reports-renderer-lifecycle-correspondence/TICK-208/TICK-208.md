---
id: TICK-208
type: ticket
title: Preserve final Sent evidence through post-report correction
status: backlog
area: reports-renderer-lifecycle-correspondence
assignee: ''
profile: feature
labels:
  - now
  - source-now
links:
  - TICK-055
archived: false
created: '2026-08-12T15:08:05.482Z'
updated: '2026-08-12T15:08:05.482Z'
---

## What

Preserve final Sent evidence through post-report correction.

## Why

This remains an unresolved current-work item in the authoritative `NOW.md`; it is a planning/research unit until taken.

## Approach

- At activation, re-check the exact current source, caller, and evidence state before choosing an implementation path.
- Write the task-level plan first; do not infer authority for live, credential, mailbox, Box, Azure, or other external operations.

## Verification

- [ ] The task plan defines the owned change, failure behavior, tests, and acceptance evidence.
- [ ] Completion is recorded only at the evidence tier actually proved.

## Notes

- Source: `NOW.md` — Next — renderer lifecycle defect.
- Related capability: CASE-23 ([[TICK-055]]).
