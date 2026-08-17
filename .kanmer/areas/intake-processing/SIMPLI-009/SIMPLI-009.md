---
id: SIMPLI-009
type: ticket
title: Make Worker the sole processor for queued intake
status: done
area: intake-processing
order: 150
assignee: claude-code
profile: feature
stageEntered:
  review: '2026-08-17T10:19:40.488Z'
  verifying: '2026-08-17T11:16:20.475Z'
  done: '2026-08-17T11:54:59.805Z'
taken_at: '2026-08-17T09:59:59.695Z'
branch: task/simpli-009
worktree: ../pegasus-worktrees/simpli-009
labels: []
groups:
  - EPIC-002
  - HZN-003
links: []
blocks:
  - SIMPLI-010
commits:
  - 195154f9
  - e9f27fe7
  - caad05e8
  - 8bf0a3e6
  - fc144848
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/385'
archived: false
created: '2026-08-13T12:12:48.881Z'
updated: '2026-08-17T11:54:59.805Z'
---

## What

Make the Worker the only component that processes queued intake.

## Why

The current Web and Worker paths compete for ownership, creating permission, durability, and recovery risks.

## Approach

- Stage work as pending and dispatch it through the queue.
- Remove Web inline processing and polling.
- Repair stranded dispatched work and classify unexpected failures explicitly.

## Verification

- [ ] Duplicate delivery, crash-after-stage, lease expiry, poison handling, and Web/Worker permission-boundary tests pass.
