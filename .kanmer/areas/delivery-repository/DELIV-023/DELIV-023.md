---
id: DELIV-023
type: ticket
title: Allow pre-release Worker timer renames through pre-provision validation
status: done
area: delivery-repository
assignee: codex-mcp-client
profile: fix
stageEntered:
  preparing: '2026-08-26T10:40:15.965Z'
  review: '2026-08-26T10:43:25.787Z'
  verifying: '2026-08-26T11:05:32.335Z'
  done: '2026-08-26T11:05:38.063Z'
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
  - c2c4bcc4
  - 427d656d
  - eb6899c0
prs:
  - '554'
deployment: production
archived: false
created: '2026-08-26T10:40:08.668Z'
updated: '2026-08-26T11:05:41.516Z'
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
