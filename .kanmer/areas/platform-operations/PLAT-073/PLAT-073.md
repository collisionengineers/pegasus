---
id: PLAT-073
type: ticket
title: Provision and document the Linux-native WSL toolchain
status: done
area: platform-operations
assignee: codex-root
profile: chore
stageEntered:
  preparing: '2026-09-04T15:11:36.637Z'
  review: '2026-09-04T16:57:17.882Z'
  verifying: '2026-09-04T18:03:12.916Z'
  done: '2026-09-04T18:08:23.229Z'
taken_at: '2026-09-04T15:15:10.556Z'
branch: PLAT-073-wsl-toolchain
worktree: .worktrees/plat-073
claim_expires_at: '2026-09-04T18:24:31.629Z'
claim_controller: codex-root
lease_id: b367fe67-fd5d-4e8e-9918-740ddd0d9974
lease_revision: 4
lease_workspace: 'worktree:/home/pguser/projects/pegasus/.worktrees/plat-073'
lease_provider: codex
lease_phase: review
lease_heartbeat_at: '2026-09-04T17:54:31.629Z'
labels:
  - wsl
  - linux
  - tooling
groups:
  - EPIC-013
links: []
blocks:
  - PLAT-074
  - UIIMP-016
commits:
  - ce1248fb4d3be62de0803cd2e542afeb44040360
  - b2f0be13b0f1bf4b4fbc848a238927d16a66df13
  - edb42e3252df48087078cef88ae8198a59ba3a9f
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/661'
archived: false
created: '2026-09-04T11:58:34.774Z'
updated: '2026-09-04T18:08:23.229Z'
---

## What

Install the pinned offline and cloud development tools under Linux, remove Windows PATH dependencies, reconcile Kanmer v0.4.1, and align Doctor/runbook repair guidance.

## Why

The WSL checkout is native but currently resolves Windows tools and lacks most Pegasus prerequisites.

## Verification

- [ ] Both Doctor profiles and the canonical locked restore/build/test commands pass using Linux-native executables.

## Outcome
