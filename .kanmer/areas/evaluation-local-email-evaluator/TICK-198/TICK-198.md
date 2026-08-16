---
id: TICK-198
type: ticket
title: >-
  Define the tracked build and verification boundary for
  scripts/email-eval-desktop
status: backlog
area: evaluation-local-email-evaluator
assignee: ''
profile: feature
labels:
  - now
  - source-now
  - decision-required
links: []
archived: false
created: '2026-08-12T15:08:04.922Z'
updated: '2026-08-12T15:08:04.922Z'
---

## What

Define the tracked build and verification boundary for scripts/email-eval-desktop.

## Why

This remains an unresolved current-work item in the authoritative `NOW.md`; it is a planning/research unit until taken.

## Approach

- At activation, re-check the exact current source, caller, and evidence state before choosing an implementation path.
- Write the task-level plan first; do not infer authority for live, credential, mailbox, Box, Azure, or other external operations.

## Verification

- [ ] The task plan defines the owned change, failure behavior, tests, and acceptance evidence.
- [ ] Completion is recorded only at the evidence tier actually proved.

## Notes

- Source: `NOW.md` — Next — repository hygiene.
