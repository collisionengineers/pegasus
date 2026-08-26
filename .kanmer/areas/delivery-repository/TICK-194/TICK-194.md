---
id: TICK-194
type: ticket
title: Detect direct or non-merge pushes to main in CI
status: done
area: delivery-repository
order: 670
assignee: codex-mcp-client
profile: feature
stageEntered:
  preparing: '2026-08-17T04:09:58.401Z'
  review: '2026-08-17T04:16:49.729Z'
  verifying: '2026-08-17T05:04:32.212Z'
  done: '2026-08-18T12:22:39.389Z'
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
  - '47086670'
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/377'
deployment: n/a
archived: false
created: '2026-08-12T15:08:04.783Z'
updated: '2026-08-26T14:34:43.731Z'
---

## What

Detect direct or non-merge pushes to main in CI.

## Why

This remains an unresolved current-work item in the canonical Kanmer board; it is a planning/research unit until taken.

## Approach

- At activation, re-check the exact current source, caller, and evidence state before choosing an implementation path.
- Write the task-level plan first; do not infer authority for live, credential, mailbox, Box, Azure, or other external operations.

## Verification

- [x] The task plan defines the owned change, failure behavior, tests, and acceptance evidence.
- [x] Completion is recorded only at the evidence tier actually proved.

## Notes

- Source: the retired pre-Kanmer tracker — Next — main guard.

## Tracker migration

Authority references were retargeted by [[KANMER-001]] after the legacy tracker was retired.

## Outcome

Main-push history guard shipped via PR #377 (merged 2026-08-17T05:04:26Z, `47086670`; on `main` since #394). Its predicate was later revised by [[DELIV-002]] to the fast-forward policy (append-only + contained in `dev`); the guard passed on the first real promotion (release 9, run 32133221206). Guard tests 8/8 on `main`. Worktree cleanup for this ticket is owed on workstation `PC` (`C:/Users/PC/Documents/GitHub/pegasus-worktrees/main-branch-history-guard`); the remote branch was deleted. Closed out 2026-08-18.
