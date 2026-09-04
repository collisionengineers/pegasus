---
id: DELIV-047
type: ticket
title: Make Linux the authorised Pegasus release workstation
status: implementing
area: delivery-repository
assignee: codex
profile: chore
stageEntered:
  preparing: '2026-09-04T18:51:30.385Z'
  review: '2026-09-04T20:33:03.557Z'
  implementing: '2026-09-04T20:36:28.926Z'
taken_at: '2026-09-04T18:56:09.501Z'
branch: DELIV-047-linux-release
worktree: .worktrees/deliv-047
claim_expires_at: '2026-09-04T21:06:41.556Z'
claim_controller: codex
review_round: 1
lease_id: f9770d71-438d-4241-a126-a41b0880b5c4
lease_revision: 3
lease_workspace: 'worktree:/home/pguser/projects/pegasus/.worktrees/deliv-047'
lease_phase: running-command
lease_heartbeat_at: '2026-09-04T19:06:41.555Z'
labels:
  - release
  - linux
  - azure
groups:
  - EPIC-013
links:
  - >-
    https://github.com/collisionengineers/pegasus/blob/5375e0f54/docs/adr/0037-linux-authorised-release-workstation.md
blocks:
  - DELIV-048
refs:
  - docs/adr/0007-direct-terminal-azure-deployment.md
commits:
  - 0f15155b8162a3086c8e041617c0c9820065068d
  - 287fc2e46aeee4999c8bab18349ea44f32b40b4d
  - 5375e0f542c1a5ae873ec03e9b6e9778dff3a41a
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/667'
archived: false
created: '2026-09-04T11:58:34.797Z'
updated: '2026-09-04T20:39:35.058Z'
---

## What

Change the authorised release route to Linux-built artifacts and a Linux terminal after local equivalence proof, leaving current releases available until cutover.

## Why

Web and Worker already deploy to Linux and the release scripts partially support a Linux migration bundle, but documentation and defaults remain Windows-bound.

## Verification

- [ ] A Linux exact-SHA release passes artifact, deployment, smoke and current-state documentation gates; Windows-only release commands are retired.

## Outcome
