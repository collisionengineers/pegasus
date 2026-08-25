---
id: TICK-119
type: ticket
title: Prove operator EVA drag-and-drop handoff from a live case
status: backlog
area: engineering-assessment
assignee: ''
profile: custom
requires: {}
labels:
  - now
  - source-now
  - requires-live-approval
  - blocked
groups:
  - HZN-002
  - HZN-003
  - EPIC-009
links:
  - TICK-022
  - TICK-116
archived: true
created: '2026-08-12T15:08:02.458Z'
updated: '2026-08-25T06:36:48.002Z'
---

## What

Prove operator EVA drag-and-drop handoff from a live case.

## Why

This remains an unresolved current-work item in the canonical Kanmer board; it is a planning/research unit until taken.

## Approach

- At activation, re-check the exact current source, caller, and evidence state before choosing an implementation path.
- Write the task-level plan first; do not infer authority for live, credential, mailbox, Box, Azure, or other external operations.

## Verification

- [ ] The task plan defines the owned change, failure behavior, tests, and acceptance evidence.
- [ ] Completion is recorded only at the evidence tier actually proved.

## Notes

- Source: the retired pre-Kanmer tracker — Path 5 — EVA bundle handoff.
- Related capability: EXT-03 ([[TICK-022]]).
- Live-system work requires fresh exact-target approval before any external operation.
- Blocked by: [[TICK-116]] — EVA handoff needs an accepted live Case/PO.

## Migrated validation

This standalone proof/validation ticket was consolidated into [[TICK-022]]. Its pending checks and approval boundaries now live in that work ticket's `checklist.md`; actual results belong in the owner's `proof.md`. Archived rather than deleted to preserve history.


## Tracker migration

Authority references were retargeted by [[KANMER-001]] after the legacy tracker was retired.
