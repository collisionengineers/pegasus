---
id: TICK-203
type: ticket
title: >-
  Reconcile the renderer MCP design against the merged Automation Actor
  inventory
status: backlog
area: reports-renderer-lifecycle-correspondence
assignee: ''
profile: feature
labels:
  - now
  - source-now
links:
  - TICK-027
archived: false
created: '2026-08-12T15:08:05.112Z'
updated: '2026-08-12T15:08:05.112Z'
---

## What

Reconcile the renderer MCP design against the merged Automation Actor inventory.

## Why

This remains an unresolved current-work item in the authoritative `NOW.md`; it is a planning/research unit until taken.

## Approach

- At activation, re-check the exact current source, caller, and evidence state before choosing an implementation path.
- Write the task-level plan first; do not infer authority for live, credential, mailbox, Box, Azure, or other external operations.

## Verification

- [ ] The task plan defines the owned change, failure behavior, tests, and acceptance evidence.
- [ ] Completion is recorded only at the evidence tier actually proved.

## Notes

- Source: `NOW.md` — Next — renderer MCP plan.
- Related capability: MCP-06 ([[TICK-027]]).
