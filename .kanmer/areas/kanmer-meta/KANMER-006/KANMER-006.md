---
id: KANMER-006
type: ticket
title: Reconcile the current Kanmer setup drift
status: backlog
area: kanmer-meta
assignee: ''
profile: chore
labels:
  - kanmer
  - setup
  - board-groom-follow-up
links:
  - KANMER-003
  - TICK-222
archived: false
created: '2026-08-25T06:35:41.710Z'
updated: '2026-08-25T06:35:41.710Z'
---

## What

Reconcile the repository's current Kanmer-managed setup after [[TICK-222]] and release 28.

## Why

Live `get_status` reports that `.claude/skills/kanmer-setup` differs from packaged Kanmer 0.3.3 and `.claude/skills` has no ownership/version stamp. The missing `questions-resolved` text in `board.yml` is runtime-compensated and is not a defect.

## Approach

- Use the `kanmer-setup` skill after re-reading live status.
- Reconcile only the reported behind/unstamped artefacts.
- Preserve the board worktree and the MCP path corrections delivered by [[TICK-222]].

## Verification

- [ ] `get_status.repo.upToDate` is true, or every remaining entry is explicitly informational/compensated.

## Outcome
