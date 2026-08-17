---
id: TICK-172
type: ticket
title: Correct Reason Dialog Escape and focus-trap behaviour
status: backlog
area: platform-operations
assignee: ''
profile: feature
labels:
  - now
  - source-now
groups:
  - EPIC-003
links: []
archived: true
created: '2026-08-12T15:08:04.115Z'
updated: '2026-08-17T06:42:05.764Z'
---

## What

Correct Reason Dialog Escape and focus-trap behaviour.

## Why

This item was mechanically imported from the retired pre-Kanmer queue and contains no independently actionable scope. It is archived pending a new evidence-backed ticket if the need re-emerges.

## Approach

- At activation, re-check the exact current source, caller, and evidence state before choosing an implementation path.
- Write the task-level plan first; do not infer authority for live, credential, mailbox, Box, Azure, or other external operations.

## Verification

- [ ] The task plan defines the owned change, failure behavior, tests, and acceptance evidence.
- [ ] Completion is recorded only at the evidence tier actually proved.

## Notes

- Migration: archived by [[KANMER-001]] after the retired queue was reconciled.
