---
id: TICK-183
type: ticket
title: Decide whether EditLeaseToken may remain plaintext at rest
status: todo
area: case-lifecycle-chasing-edit-leases
priority: medium
assignee: ''
labels:
  - now
  - source-now
  - decision-required
links: []
archived: false
created: '2026-08-12T15:08:04.442Z'
updated: '2026-08-12T15:08:04.442Z'
---

## What

Decide whether EditLeaseToken may remain plaintext at rest.

## Why

This remains an unresolved current-work item in the authoritative `NOW.md`; it is a planning/research unit until taken.

## Approach

- At activation, re-check the exact current source, caller, and evidence state before choosing an implementation path.
- Write the task-level plan first; do not infer authority for live, credential, mailbox, Box, Azure, or other external operations.

## Verification

- [ ] The task plan defines the owned change, failure behavior, tests, and acceptance evidence.
- [ ] Completion is recorded only at the evidence tier actually proved.

## Notes

- Source: `NOW.md` — Next — EditLeaseToken decision.
