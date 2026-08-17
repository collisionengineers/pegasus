---
id: TICK-139
type: ticket
title: Expose fail-closed Blocked intake reasons and staff retry/recovery
status: backlog
area: intake-triage-needs-sorting-blocked
assignee: ''
profile: feature
labels:
  - now
  - source-now
links:
  - TICK-012
archived: true
created: '2026-08-12T15:08:03.182Z'
updated: '2026-08-17T04:09:29.374Z'
---

## What

Expose fail-closed Blocked intake reasons and staff retry/recovery.

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
- Related capability: INT-25 ([[TICK-012]]).
