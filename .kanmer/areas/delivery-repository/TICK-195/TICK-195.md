---
id: TICK-195
type: ticket
title: Validate new Markdown placement in CI
status: done
area: delivery-repository
order: 780
assignee: codex-mcp-client
profile: feature
stageEntered:
  preparing: '2026-08-17T06:11:24.850Z'
  review: '2026-08-17T06:14:55.107Z'
  verifying: '2026-08-17T06:36:03.971Z'
  done: '2026-08-18T12:22:42.184Z'
labels:
  - now
  - source-now
groups:
  - EPIC-001
links: []
docs_todo: true
commits:
  - 4db056bcd0a609bf6900eb913d3ee417ec4feeeb
  - 98cf01f715ab49363caf6ae66724fac76ac6cc9d
  - fdd2aeba723717fb2391b64d7a810339123abc82
  - 562a502f
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/384'
deployment: n/a
archived: false
created: '2026-08-12T15:08:04.833Z'
updated: '2026-09-03T09:06:46.197Z'
---

## What

Validate new Markdown placement in CI.

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

Delivered via PR #384 (merged 2026-08-17T06:35:48Z, `562a502f`) and shipped to `main` in #394; the placement gate ran green on the 17 Aug main push. On 2026-08-18 the operator directed its removal as unnecessary CI policy ([[DELIV-005]], PR #401): the `documentation` job keeps `Test-TestMarkdownPlacement.ps1` and the link check, and `scripts/Test-MarkdownPlacement.ps1` remains in the tree uncalled. Delivered, verified, then rolled back by decision — nothing further owed. Worktree cleanup owed on workstation `PC`; the remote branch was deleted. Closed out 2026-08-18.
