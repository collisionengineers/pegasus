---
id: TICK-186
type: ticket
title: Assemble the extraction cohort and untouched holdout
status: review
area: platform-operations
assignee: claude-code
profile: feature
stageEntered:
  implementing: '2026-08-20T05:38:03.248Z'
  review: '2026-08-20T05:38:30.987Z'
taken_at: '2026-08-20T05:37:15.697Z'
branch: local-only/tick-186
labels:
  - now
  - source-now
links:
  - TICK-009
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
archived: false
created: '2026-08-12T15:08:04.505Z'
updated: '2026-08-20T05:38:30.987Z'
---

## What

Assemble the extraction cohort and untouched holdout.

## Why

This remains an unresolved current-work item in the canonical Kanmer board; it is a planning/research unit until taken.

## Approach

- At activation, re-check the exact current source, caller, and evidence state before choosing an implementation path.
- Write the task-level plan first; do not infer authority for live, credential, mailbox, Box, Azure, or other external operations.

## Verification

- [ ] The task plan defines the owned change, failure behavior, tests, and acceptance evidence.
- [ ] Completion is recorded only at the evidence tier actually proved.

## Notes

- Source: the retired pre-Kanmer tracker — Next — extraction cohort.
- Related capability: MAIL-21 ([[TICK-009]]).


## Tracker migration

Authority references were retargeted by [[KANMER-001]] after the legacy tracker was retired.
