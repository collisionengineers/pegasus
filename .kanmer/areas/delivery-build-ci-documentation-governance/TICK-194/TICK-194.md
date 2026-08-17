---
id: TICK-194
type: ticket
title: Detect direct or non-merge pushes to main in CI
status: review
area: delivery-build-ci-documentation-governance
assignee: codex-mcp-client
profile: feature
stageEntered:
  preparing: '2026-08-17T04:09:58.401Z'
  review: '2026-08-17T04:16:49.729Z'
taken_at: '2026-08-17T04:10:19.599Z'
branch: task/main-branch-history-guard
worktree: 'C:/Users/PC/Documents/GitHub/pegasus-worktrees/main-branch-history-guard'
labels:
  - now
  - source-now
groups:
  - EPIC-001
links: []
docs_todo: true
commits:
  - 5599899c43086c46586eb60edc7372098f80e374
  - 740425144f73197371c7532034f951602898cbef
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/377'
archived: false
created: '2026-08-12T15:08:04.783Z'
updated: '2026-08-17T04:53:14.960Z'
---

## What

Detect direct or non-merge pushes to main in CI.

## Why

This remains an unresolved current-work item in the canonical Kanmer board; it is a planning/research unit until taken.

## Approach

- At activation, re-check the exact current source, caller, and evidence state before choosing an implementation path.
- Write the task-level plan first; do not infer authority for live, credential, mailbox, Box, Azure, or other external operations.

## Verification

- [ ] The task plan defines the owned change, failure behavior, tests, and acceptance evidence.
- [ ] Completion is recorded only at the evidence tier actually proved.

## Notes

- Source: the retired pre-Kanmer tracker — Next — main guard.


## Tracker migration

Authority references were retargeted by [[KANMER-001]] after the legacy tracker was retired.
