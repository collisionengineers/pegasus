---
id: DELIV-046
type: ticket
title: Restore main as an ancestor of dev
status: done
area: delivery-repository
assignee: codex-root
profile: chore
stageEntered:
  preparing: '2026-09-04T11:59:53.345Z'
  review: '2026-09-04T12:23:57.577Z'
  verifying: '2026-09-04T15:08:23.999Z'
  done: '2026-09-04T15:10:04.262Z'
taken_at: '2026-09-04T12:01:22.290Z'
branch: DELIV-046-restore-main-ancestry
worktree: .worktrees/deliv-046
claim_expires_at: '2026-09-04T13:11:02.744Z'
claim_controller: codex-root
lease_id: f68aa36e-600d-485f-9ccc-5b78dd369fb9
lease_revision: 3
lease_worker_run: codex-review-deliv-046
lease_workspace: 'worktree:/home/pguser/projects/pegasus/.worktrees/deliv-046'
lease_provider: codex
lease_phase: review
lease_heartbeat_at: '2026-09-04T12:41:02.744Z'
labels:
  - git
  - release
  - urgent
groups:
  - EPIC-013
links: []
blocks:
  - PLAT-073
commits:
  - 2958ef5b68e51fce99b1c677abfa261a3eabbb46
  - 0174adef1a00b4a29729d3a0ffd714838562d2c8
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/660'
archived: false
created: '2026-09-04T11:58:34.764Z'
updated: '2026-09-04T15:10:04.262Z'
---

## What

Merge the authorised main-only commits into dev through a reviewed task PR while preserving exact ancestry.

## Why

The documented exact-SHA promotion route currently fails because main and dev have diverged.

## Verification

- [ ] origin/main is an ancestor of origin/dev and both main-only test artifacts and dev history remain reachable.

## Outcome
