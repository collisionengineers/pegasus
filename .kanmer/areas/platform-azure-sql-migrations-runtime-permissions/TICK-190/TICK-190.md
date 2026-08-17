---
id: TICK-190
type: ticket
title: Prove template-database backup and restore against external SQL Server
status: backlog
area: platform-azure-sql-migrations-runtime-permissions
assignee: ''
profile: custom
requires: {}
labels:
  - now
  - source-now
links:
  - TICK-028
archived: true
created: '2026-08-12T15:08:04.623Z'
updated: '2026-08-17T04:09:41.037Z'
---

## What

Prove template-database backup and restore against external SQL Server.

## Why

This remains an unresolved current-work item in the canonical Kanmer board; it is a planning/research unit until taken.

## Approach

- At activation, re-check the exact current source, caller, and evidence state before choosing an implementation path.
- Write the task-level plan first; do not infer authority for live, credential, mailbox, Box, Azure, or other external operations.

## Verification

- [ ] The task plan defines the owned change, failure behavior, tests, and acceptance evidence.
- [ ] Completion is recorded only at the evidence tier actually proved.

## Notes

- Source: the retired pre-Kanmer tracker — Next — template database backup/restore.

## Migrated validation

This standalone proof/validation ticket was consolidated into [[TICK-028]]. Its pending checks and approval boundaries now live in that work ticket's `checklist.md`; actual results belong in the owner's `proof.md`. Archived rather than deleted to preserve history.


## Tracker migration

Authority references were retargeted by [[KANMER-001]] after the legacy tracker was retired.
