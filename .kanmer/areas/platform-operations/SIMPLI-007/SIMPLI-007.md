---
id: SIMPLI-007
type: ticket
title: Move the QDOS alpha acceptance gate out of application composition
status: review
area: platform-operations
order: 130
assignee: claude-code
profile: feature
stageEntered:
  review: '2026-08-17T12:30:24.172Z'
taken_at: '2026-08-17T10:00:08.513Z'
branch: task/simpli-007-acceptance-gate
worktree: ../pegasus-worktrees/simpli-007-acceptance-gate
labels: []
groups:
  - EPIC-002
  - HZN-003
links: []
blocks: []
refs:
  - docs/adr/0013-qdos-alpha-implementation-contract.md
commits:
  - c9e657c3
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/388'
archived: false
created: '2026-08-13T12:12:48.841Z'
updated: '2026-08-17T12:30:24.172Z'
---

## What

Remove the QDOS alpha acceptance gate from Core and Web composition while retaining useful release validation in tooling.

## Why

A test-only manifest checker is currently part of the running application and carries obsolete release requirements.

## Approach

- Move the validator to release tooling.
- Remove the unused application-facing gate and interface.

## Verification

- [ ] Application composition no longer registers the acceptance gate and release validation remains available.
