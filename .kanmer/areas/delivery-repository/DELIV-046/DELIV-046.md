---
id: DELIV-046
type: ticket
title: Restore main as an ancestor of dev
status: implementing
area: delivery-repository
assignee: codex-root
profile: chore
stageEntered:
  preparing: '2026-09-04T11:59:53.345Z'
taken_at: '2026-09-04T12:01:22.290Z'
branch: DELIV-046-restore-main-ancestry
worktree: .worktrees/deliv-046
claim_expires_at: '2026-09-04T12:31:22.290Z'
claim_controller: codex-root
lease_id: f68aa36e-600d-485f-9ccc-5b78dd369fb9
lease_revision: 1
lease_workspace: 'worktree:/home/pguser/projects/pegasus/.worktrees/deliv-046'
lease_phase: implementing
lease_heartbeat_at: '2026-09-04T12:01:22.290Z'
labels:
  - git
  - release
  - urgent
groups:
  - EPIC-013
links: []
blocks:
  - PLAT-073
archived: false
created: '2026-09-04T11:58:34.764Z'
updated: '2026-09-04T12:01:22.290Z'
---

## What

Merge the authorised main-only commits into dev through a reviewed task PR while preserving exact ancestry.

## Why

The documented exact-SHA promotion route currently fails because main and dev have diverged.

## Verification

- [ ] origin/main is an ancestor of origin/dev and both main-only test artifacts and dev history remain reachable.

## Outcome
