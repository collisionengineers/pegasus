---
id: TICK-214
type: ticket
title: Decide the long-term MCPB host and distribution boundary
status: review
area: documents-reports
assignee: ''
profile: feature
stageEntered:
  preparing: '2026-08-19T09:03:59.534Z'
  implementing: '2026-08-25T06:50:06.674Z'
  review: '2026-08-25T06:50:06.999Z'
labels:
  - now
  - source-now
  - decision-required
groups:
  - EPIC-004
links:
  - SIMPLI-015
refs:
  - docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md
  - docs/adr/0025-integrate-renderer-and-extractor-into-the-application.md
archived: false
created: '2026-08-12T15:08:05.885Z'
updated: '2026-08-25T06:50:06.999Z'
---

## What

Decide the long-term MCPB host and distribution boundary.

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

Authority references were retargeted by [[KANMER-001]] after t