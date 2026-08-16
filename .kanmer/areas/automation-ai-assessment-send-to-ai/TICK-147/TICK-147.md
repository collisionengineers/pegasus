---
id: TICK-147
type: ticket
title: Make Send-to-AI dispatch idempotent under concurrent requests
status: backlog
area: automation-ai-assessment-send-to-ai
assignee: ''
profile: feature
labels:
  - now
  - source-now
links:
  - TICK-102
archived: false
created: '2026-08-12T15:08:03.416Z'
updated: '2026-08-12T15:08:03.416Z'
---

## What

Make Send-to-AI dispatch idempotent under concurrent requests.

## Why

This remains an unresolved current-work item in the authoritative `NOW.md`; it is a planning/research unit until taken.

## Approach

- At activation, re-check the exact current source, caller, and evidence state before choosing an implementation path.
- Write the task-level plan first; do not infer authority for live, credential, mailbox, Box, Azure, or other external operations.

## Verification

- [ ] The task plan defines the owned change, failure behavior, tests, and acceptance evidence.
- [ ] Completion is recorded only at the evidence tier actually proved.

## Notes

- Source: `NOW.md` — Next — Send-to-AI integrity.
- Related capability: AI-09 ([[TICK-102]]).
