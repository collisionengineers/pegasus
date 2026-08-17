---
id: TICK-215
type: ticket
title: Decide where report rendering executes in production
status: backlog
area: reports-renderer-lifecycle-correspondence
assignee: ''
profile: feature
labels:
  - now
  - source-now
  - decision-required
links:
  - SIMPLI-015
archived: false
created: '2026-08-12T15:08:05.967Z'
updated: '2026-08-17T04:13:44.799Z'
---

## What

Decide where report rendering executes in production.

## Why

This remains an unresolved current-work item in the canonical Kanmer board; it is a planning/research unit until taken.

## Approach

- At activation, re-check the exact current source, caller, and evidence state before choosing an implementation path.
- Write the task-level plan first; do not infer authority for live, credential, mailbox, Box, Azure, or other external operations.

## Verification

- [ ] The task plan defines the owned change, failure behavior, tests, and acceptance evidence.
- [ ] Completion is recorded only at the evidence tier actually proved.

## Notes

- Source: the retired pre-Kanmer tracker — Waiting — renderer relocation.


## Tracker migration

Authority references were retargeted by [[KANMER-001]] after the legacy tracker was retired.
