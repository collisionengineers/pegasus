---
id: TICK-200
type: ticket
title: Reduce remaining GitHub Actions wall-clock time
status: done
area: delivery-repository
assignee: codex-mcp-client
profile: feature
stageEntered:
  preparing: '2026-08-17T05:19:09.158Z'
  review: '2026-08-17T05:25:08.863Z'
  verifying: '2026-08-17T06:07:48.798Z'
  done: '2026-08-18T12:22:48.385Z'
taken_at: '2026-08-17T05:21:02.383Z'
branch: task/reduce-actions-wall-clock
worktree: 'C:/Users/PC/Documents/GitHub/pegasus-worktrees/reduce-actions-wall-clock'
labels:
  - now
  - source-now
groups:
  - EPIC-001
links: []
docs_todo: true
commits:
  - 0ea9c0af
  - 8a29c1f8
  - b9b3470c
  - '30933616'
  - 2db2b0ea
  - 28c10422
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/381'
deployment: n/a
archived: false
created: '2026-08-12T15:08:04.974Z'
updated: '2026-08-18T12:26:03.496Z'
---

## What

Reduce remaining GitHub Actions wall-clock time.

## Why

This remains an unresolved current-work item in the canonical Kanmer board; it is a planning/research unit until taken.

## Approach

- At activation, re-check the exact current source, caller, and evidence state before choosing an implementation path.
- Write the task-level plan first; do not infer authority for live, credential, mailbox, Box, Azure, or other external operations.

## Verification

- [x] The task plan defines the owned change, failure behavior, tests, and acceptance evidence.
- [x] Completion is recorded only at the evidence tier actually proved.

## Notes

- Source: the retired pre-Kanmer tracker — Next — CI wall-clock.

## Tracker migration

Authority references were retargeted by [[KANMER-001]] after the legacy tracker was retired.

## Outcome

Snake-dealt whole-class SQL shards and the fast `changes`-job shard regression shipped via PR #381 (merged 2026-08-17T06:07:40Z, `28c10422`; on `main` since #394). On the release-9 SHA a full `repository-check` completes in ~9 minutes (shards 8m05s / 6m46s / 7m09s); `Test-TestShard.ps1` and `Test-CiChangeFlags.ps1` pass on `main`. Hosted-runner LocalDB variance remains (one shard-1 timeout on PR #402, green on re-run). Worktree cleanup owed on workstation `PC`; the remote branch was deleted. Closed out 2026-08-18.
