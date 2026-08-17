---
id: TICK-112
type: ticket
title: Establish the QDOS Organisation and Principal in production
status: backlog
area: data-provider-principal-repairer-reference
assignee: ''
profile: feature
labels:
  - now
  - source-now
  - requires-live-approval
  - decision-required
groups:
  - HZN-003
links:
  - TICK-012
archived: true
created: '2026-08-12T15:08:02.310Z'
updated: '2026-08-17T06:40:18.585Z'
---

## What

Establish the QDOS Organisation and Principal in production.

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
- Live-system work requires fresh exact-target approval before any external operation.
