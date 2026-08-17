---
id: TICK-124
type: ticket
title: Add chaser-sweep alerting
status: backlog
area: operations-telemetry-alerts-live-diagnostics
assignee: ''
profile: feature
labels:
  - now
  - source-now
  - requires-live-approval
groups:
  - HZN-003
links: []
archived: true
created: '2026-08-12T15:08:02.555Z'
updated: '2026-08-17T06:40:20.762Z'
---

## What

Add chaser-sweep alerting.

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
- Live-system work requires fresh exact-target approval before any external operation.
