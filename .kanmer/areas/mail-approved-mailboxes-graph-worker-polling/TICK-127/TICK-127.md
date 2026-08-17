---
id: TICK-127
type: ticket
title: Compute retained-mail freshness per approved mailbox
status: backlog
area: mail-approved-mailboxes-graph-worker-polling
assignee: ''
profile: feature
labels:
  - now
  - source-now
links: []
archived: true
created: '2026-08-12T15:08:02.626Z'
updated: '2026-08-17T04:09:24.079Z'
---

## What

Compute retained-mail freshness per approved mailbox.

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
