---
id: TICK-169
type: ticket
title: Correct Assessment panel state transitions and repeat-send behaviour
status: todo
area: automation-ai-assessment-send-to-ai
priority: medium
assignee: ''
labels:
  - now
  - source-now
links:
  - TICK-102
archived: false
created: '2026-08-12T15:08:03.923Z'
updated: '2026-08-12T15:08:03.923Z'
---

## What

Correct Assessment panel state transitions and repeat-send behaviour.

## Why

This remains an unresolved current-work item in the authoritative `NOW.md`; it is a planning/research unit until taken.

## Approach

- At activation, re-check the exact current source, caller, and evidence state before choosing an implementation path.
- Write the task-level plan first; do not infer authority for live, credential, mailbox, Box, Azure, or other external operations.

## Verification

- [ ] The task plan defines the owned change, failure behavior, tests, and acceptance evidence.
- [ ] Completion is recorded only at the evidence tier actually proved.

## Notes

- Source: `NOW.md` — Next — Assessment surface exposure.
- Related capability: AI-09 ([[TICK-102]]).
