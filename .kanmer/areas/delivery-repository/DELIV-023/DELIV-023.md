---
id: DELIV-023
type: ticket
title: Allow pre-release Worker timer renames through pre-provision validation
status: review
area: delivery-repository
assignee: codex-mcp-client
profile: fix
stageEntered:
  preparing: '2026-08-26T10:40:15.965Z'
  review: '2026-08-26T10:43:25.787Z'
taken_at: '2026-08-26T10:41:48.154Z'
branch: task/deliv-023-pre-release-worker-census
worktree: ../pegasus-worktrees/deliv-023-pre-release-worker-census
labels:
  - release
  - deployment
  - pre-release
links: []
refs:
  - docs/runbook.md
  - docs/engineering.md
  - docs/operations.md
commits:
  - 5e1fb7aa
prs:
  - '554'
deployment: production
archived: false
created: '2026-08-26T10:40:08.668Z'
updated: '2026-08-26T10:43:29.936Z'
---

## What
Make pre-provision validation accept the exact currently deployed Worker activation census when a release intentionally renames one function, while keeping post-deployment smoke strict against the release's new census. Then complete release 32.

## Why
The current check requires the new function name before provisioning can deploy it, making an ordinary pre-release timer rename impossible. This is disproportionate for the small pre-release estate and blocked an otherwise valid release.

## Acceptance
- Pre-provision verifies the Worker is enabled and its disabled-setting values are consistent without requiring the new release's names to already exist.
- Post-deployment smoke still requires the exact nine names declared by the release and the one-minute recovery schedule.
- Tests cover the distinction between pre-provision and post-deployment checks.
- Release 32 completes with exact-SHA smoke and current-state documentation.

## Outcome
