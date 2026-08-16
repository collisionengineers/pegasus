---
id: TICK-133
type: ticket
title: Reject negative vehicle mileage before persistence
status: backlog
area: engineering-vehicle-mot-inspection-evidence
assignee: ''
profile: feature
labels:
  - now
  - source-now
links: []
archived: false
created: '2026-08-12T15:08:03.054Z'
updated: '2026-08-12T15:08:03.054Z'
---

## What

Reject negative vehicle mileage before persistence.

## Why

This remains an unresolved current-work item in the authoritative `NOW.md`; it is a planning/research unit until taken.

## Approach

- At activation, re-check the exact current source, caller, and evidence state before choosing an implementation path.
- Write the task-level plan first; do not infer authority for live, credential, mailbox, Box, Azure, or other external operations.

## Verification

- [ ] The task plan defines the owned change, failure behavior, tests, and acceptance evidence.
- [ ] Completion is recorded only at the evidence tier actually proved.

## Notes

- Source: `NOW.md` — Next — upload/Inbox P2 findings.
