---
id: TICK-157
type: ticket
title: Validate required field pairings against the merged assessment record
status: backlog
area: automation-ai-assessment-send-to-ai
assignee: ''
profile: feature
labels:
  - now
  - source-now
links:
  - TICK-027
archived: false
created: '2026-08-12T15:08:03.631Z'
updated: '2026-08-12T15:08:03.631Z'
---

## What

Validate required field pairings against the merged assessment record.

## Why

This remains an unresolved current-work item in the authoritative `NOW.md`; it is a planning/research unit until taken.

## Approach

- At activation, re-check the exact current source, caller, and evidence state before choosing an implementation path.
- Write the task-level plan first; do not infer authority for live, credential, mailbox, Box, Azure, or other external operations.

## Verification

- [ ] The task plan defines the owned change, failure behavior, tests, and acceptance evidence.
- [ ] Completion is recorded only at the evidence tier actually proved.

## Notes

- Source: `NOW.md` — Next — assessment toolset correctness.
- Related capability: MCP-06 ([[TICK-027]]).
