---
id: TICK-119
type: ticket
title: Prove operator EVA drag-and-drop handoff from a live case
status: todo
area: engineering-eva-export-handoff
priority: medium
assignee: ''
labels:
  - now
  - source-now
  - requires-live-approval
  - blocked
links:
  - TICK-022
  - TICK-116
archived: false
created: '2026-08-12T15:08:02.458Z'
updated: '2026-08-12T15:09:20.390Z'
---

## What

Prove operator EVA drag-and-drop handoff from a live case.

## Why

This remains an unresolved current-work item in the authoritative `NOW.md`; it is a planning/research unit until taken.

## Approach

- At activation, re-check the exact current source, caller, and evidence state before choosing an implementation path.
- Write the task-level plan first; do not infer authority for live, credential, mailbox, Box, Azure, or other external operations.

## Verification

- [ ] The task plan defines the owned change, failure behavior, tests, and acceptance evidence.
- [ ] Completion is recorded only at the evidence tier actually proved.

## Notes

- Source: `NOW.md` — Path 5 — EVA bundle handoff.
- Related capability: EXT-03 ([[TICK-022]]).
- Live-system work requires fresh exact-target approval before any external operation.
- Blocked by: [[TICK-116]] — EVA handoff needs an accepted live Case/PO.
