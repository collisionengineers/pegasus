---
id: SIMPLI-008
type: ticket
title: Show queued receipt processing status to staff
status: verifying
area: intake-processing
order: 140
assignee: claude-code
profile: feature
stageEntered:
  review: '2026-08-17T10:19:44.061Z'
  verifying: '2026-08-17T11:16:23.469Z'
taken_at: '2026-08-17T10:00:02.519Z'
branch: task/simpli-009
worktree: ../pegasus-worktrees/simpli-009
labels: []
groups:
  - EPIC-002
  - HZN-003
links: []
blocks: []
commits:
  - 195154f9
  - e9f27fe7
  - caad05e8
  - 8bf0a3e6
  - fc144848
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/385'
archived: false
created: '2026-08-13T12:12:48.862Z'
updated: '2026-08-17T11:16:30.605Z'
---

## What

Provide a receipt-keyed staff view for queued intake processing.

## Why

After upload, staff need a visible outcome rather than an unidentifiable queued response.

## Approach

- Show Received, Processing, Complete, or Failed.
- Link to the resulting case or recovery view.

## Verification

- [ ] A queued upload exposes its current state and destination to staff.
